using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using TinyIpc.IO;

namespace TinyIpc.Messaging
{
	public class TinyMessageBus : IDisposable, ITinyMessageBus
	{
		private bool disposed;
		private long messageOverhead;
		private readonly Guid instanceId = Guid.NewGuid();
		private readonly ConcurrentQueue<Entry> publishQueue = new ConcurrentQueue<Entry>();
		private readonly TimeSpan minMessageAge;
		private readonly object messageReaderLock = new object();
		private readonly object messagePublisherLock = new object();
		private readonly ITinyMemoryMappedFile memoryMappedFile;
		private readonly bool disposeFile;

		private long lastEntryId;
		private long messagesSent;
		private long messagesReceived;
		private Task[] publishTasks = new Task[0];
		private Task[] readTasks = new Task[0];
		private int waitingReaders;
		private int waitingPublishers;

		/// <summary>
		/// Called whenever a new message is received
		/// </summary>
		public event EventHandler<TinyMessageReceivedEventArgs> MessageReceived;

		public bool MessagesBeingProcessed => waitingReaders + waitingPublishers > 0;
		public long MessagesSent => messagesSent;
		public long MessagesReceived => messagesReceived;

		public static readonly TimeSpan DefaultMinMessageAge = TimeSpan.FromMilliseconds(500);

		public TinyMessageBus(string name)
			: this(new TinyMemoryMappedFile(name), true)
		{
		}

		public TinyMessageBus(string name, TimeSpan minMessageAge)
			: this(new TinyMemoryMappedFile(name), true, minMessageAge)
		{
		}

		public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile)
			: this(memoryMappedFile, disposeFile, DefaultMinMessageAge)
		{
		}

		public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile, TimeSpan minMessageAge)
		{
			this.minMessageAge = minMessageAge;
			this.memoryMappedFile = memoryMappedFile;
			this.disposeFile = disposeFile;

			memoryMappedFile.FileUpdated += HandleIncomingMessages;

			Warmup();
		}

		public void Dispose()
		{
			memoryMappedFile.FileUpdated -= HandleIncomingMessages;

			disposed = true;

			WaitAll();

			if (disposeFile && memoryMappedFile is TinyMemoryMappedFile)
			{
				(memoryMappedFile as TinyMemoryMappedFile).Dispose();
			}
		}

		/// <summary>
		/// Performs a synchonous warmup pass to make sure everything is jitted
		/// </summary>
		private void Warmup()
		{
			Serializer.PrepareSerializer<Entry>();
			using (var memoryStream = new MemoryStream())
			{
				Serializer.Serialize(memoryStream, new Entry { Id = long.MaxValue, Instance = instanceId, Timestamp = DateTime.UtcNow });
				messageOverhead = memoryStream.Length;
			}

			var lastEntry = DeserializeLog(memoryMappedFile.Read()).LastOrDefault();
			if (lastEntry != null)
			{
				lastEntryId = lastEntry.Id;
			}

			publishQueue.Enqueue(new Entry { Instance = instanceId, Message = new byte[0] });
			ProcessPublishQueue();
		}

		/// <summary>
		/// Resets MessagesSent and MessagesReceived counters
		/// </summary>
		public void ResetMetrics()
		{
			messagesSent = 0;
			messagesReceived = 0;
		}

		/// <summary>
		/// Publishes a message to the message bus as soon as possible in a background task
		/// </summary>
		/// <param name="message"></param>
		public void PublishAsync(byte[] message)
		{
			if (disposed)
				throw new ObjectDisposedException("Can not publish messages when diposed");

			if (message == null || message.Length == 0)
				throw new ArgumentException("Message can not be empty", nameof(message));

			publishQueue.Enqueue(new Entry { Instance = instanceId, Message = message });

			if (waitingPublishers > 0)
				return;

			StartPublishTask();
		}

		internal void WaitAll()
		{
			Task.WaitAll(publishTasks);
			Task.WaitAll(readTasks);
		}

		private void StartPublishTask()
		{
			if (disposed)
				return;

			publishTasks = publishTasks.Where(x => !x.IsCompleted)
				.Concat(new[] {Task.Factory.StartNew(ProcessPublishQueue)})
				.ToArray();
		}

		private void StartReadTask()
		{
			if (disposed)
				return;

			readTasks = readTasks.Where(x => !x.IsCompleted)
				.Concat(new[] { Task.Factory.StartNew(ProcessIncomingMessages) })
				.ToArray();
		}

		private void ProcessPublishQueue()
		{
			Interlocked.Increment(ref waitingPublishers);

			lock (messagePublisherLock)
			{
				Interlocked.Decrement(ref waitingPublishers);

				if (publishQueue.Count == 0)
					return;

				memoryMappedFile.ReadWrite(
					data =>
					{
						// Retreive the log but skip messages older than minimum message age
						var cutoffPoint = DateTime.UtcNow - minMessageAge;
						var log = DeserializeLog(data).SkipWhile(entry => entry.Timestamp < cutoffPoint).ToList();
						var logSize = log.Select(l => messageOverhead + l.Message.Length).Sum();
						var lastEntry = log.LastOrDefault();
						var nextId = Math.Max(lastEntryId, lastEntry?.Id ?? 0) + 1;

						// Start slot timer after deserializing log so deserialization doesn't starve the slot time
						var slotTimer = Stopwatch.StartNew();
						var batchTime = DateTime.UtcNow;

						// Try to exhaust the publish queue but don't keep a write lock forever
						while (publishQueue.Count > 0 && slotTimer.ElapsedMilliseconds < 25)
						{
							Entry entry;

							// Check if the next message will fit in the log
							if (!publishQueue.TryPeek(out entry) || logSize + messageOverhead + entry.Message.Length > memoryMappedFile.MaxFileSize)
								break;

							if (!publishQueue.TryDequeue(out entry))
								break;

							// Write the entry to the log even if the message is empty. Never leave the log empty.
							entry.Id = nextId++;
							entry.Timestamp = batchTime;
							log.Add(entry);
							logSize += messageOverhead + entry.Message.Length;

							// Skip counting empty messages though, they are skipped on the receiving end anyway
							if (entry.Message == null || entry.Message.Length == 0)
								continue;

							Interlocked.Increment(ref messagesSent);
						}

						// Start a new publish task if there are messages left in the queue
						if (waitingPublishers == 0 && publishQueue.Count > 0)
						{
							StartPublishTask();
						}

						// Flush the updated log to the memory mapped file
						using (var memoryStream = new MemoryStream((int)logSize))
						{
							Serializer.Serialize(memoryStream, log);
							return memoryStream.ToArray();
						}
					});
			}
		}

		private void HandleIncomingMessages(object sender, EventArgs args)
		{
			if (waitingReaders > 0)
				return;

			StartReadTask();
		}

		internal void ProcessIncomingMessages()
		{
			Interlocked.Increment(ref waitingReaders);

			lock (messageReaderLock)
			{
				Interlocked.Decrement(ref waitingReaders);

				var data = memoryMappedFile.Read();

				foreach (var entry in DeserializeLog(data).SkipWhile(entry => entry.Id <= lastEntryId))
				{
					lastEntryId = entry.Id;

					if (entry.Instance == instanceId || entry.Message == null || entry.Message.Length == 0)
						continue;

					MessageReceived?.Invoke(this, new TinyMessageReceivedEventArgs { Message = entry.Message });

					Interlocked.Increment(ref messagesReceived);
				}
			}
		}

		private static IEnumerable<Entry> DeserializeLog(byte[] data)
		{
			if (data.Length == 0)
				return Enumerable.Empty<Entry>();

			using (var memoryStream = new MemoryStream(data))
			{
				return Serializer.Deserialize<List<Entry>>(memoryStream);
			}
		}

		[ProtoContract]
		private class Entry
		{
			[ProtoMember(1)]
			public long Id { get; set; }

			[ProtoMember(2)]
			public Guid Instance { get; set; }

			[ProtoMember(3)]
			public DateTime Timestamp { get; set; }

			[ProtoMember(4)]
			public byte[] Message { get; set; }
		}
	}
}
