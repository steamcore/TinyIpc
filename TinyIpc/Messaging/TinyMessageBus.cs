using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using TinyIpc.IO;

namespace TinyIpc.Messaging
{
	public class TinyMessageBus : IDisposable, ITinyMessageBus
	{
		private static readonly Encoding MessageEncoding = Encoding.GetEncoding("UTF-16");

		private readonly long messageOverhead;
		private readonly Guid instanceId = Guid.NewGuid();
		private readonly ConcurrentQueue<Entry> publishQueue = new ConcurrentQueue<Entry>();
		private readonly TimeSpan minMessageAge;
		private readonly object messageReaderLock = new object();
		private readonly object messagePublisherLock = new object();
		private readonly ITinyMemoryMappedFile memoryMappedFile;
		private readonly bool shouldDisposeFile;

		private long lastEntryId;
		private long messagesSent;
		private long messagesReceived;
		private int waitingReaders;
		private int waitingWriters;

		/// <summary>
		/// Called whenever a new message is received
		/// </summary>
		public event EventHandler<TinyMessageReceivedEventArgs> MessageReceived;

		public bool MessagesBeingProcessed { get { return waitingReaders + waitingWriters > 0; } }
		public long MessagesSent { get { return messagesSent; } }
		public long MessagesReceived { get { return messagesReceived; } }

		public TinyMessageBus(string name)
			: this(new TinyMemoryMappedFile(name))
		{
			shouldDisposeFile = true;
		}

		public TinyMessageBus(string name, TimeSpan minMessageAge)
			: this(new TinyMemoryMappedFile(name), minMessageAge)
		{
			shouldDisposeFile = true;
		}

		public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile)
			: this(memoryMappedFile, TimeSpan.FromMilliseconds(500))
		{
		}

		public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, TimeSpan minMessageAge)
		{
			Serializer.PrepareSerializer<Entry>();

			using (var memoryStream = new MemoryStream())
			{
				Serializer.Serialize(memoryStream, new Entry { Id = long.MaxValue, Instance = instanceId, Timestamp = DateTime.UtcNow });
				messageOverhead = memoryStream.Length;
			}

			this.minMessageAge = minMessageAge;
			this.memoryMappedFile = memoryMappedFile;

			var lastEntry = DeserializeLog(memoryMappedFile.Read()).LastOrDefault();
			if (lastEntry != null)
			{
				lastEntryId = lastEntry.Id;
			}

			memoryMappedFile.FileUpdated += HandleIncomingMessages;
		}

		public void Dispose()
		{
			memoryMappedFile.FileUpdated -= HandleIncomingMessages;

			if (shouldDisposeFile && memoryMappedFile is TinyMemoryMappedFile)
			{
				(memoryMappedFile as TinyMemoryMappedFile).Dispose();
			}
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
		public void PublishAsync(string message)
		{
			publishQueue.Enqueue(new Entry { Instance = instanceId, Message = MessageEncoding.GetBytes(message) });

			if (waitingWriters > 0)
				return;

			Task.Factory.StartNew(ProcessPublishQueue);
		}

		private void ProcessPublishQueue()
		{
			Interlocked.Increment(ref waitingWriters);

			lock (messagePublisherLock)
			{
				Interlocked.Decrement(ref waitingWriters);

				if (publishQueue.Count == 0)
					return;

				memoryMappedFile.ReadWrite(
					data =>
					{
						var cutoffPoint = DateTime.UtcNow - minMessageAge;
						var log = DeserializeLog(data).SkipWhile(entry => entry.Timestamp < cutoffPoint).ToList();
						var logSize = log.Select(l => messageOverhead + l.Message.Length).Sum();
						var lastEntry = log.LastOrDefault();
						var nextId = Math.Max(lastEntryId, lastEntry != null ? lastEntry.Id : 0) + 1;

						// Start slot timer after deserializing log so deserialization doesn't starve the slot time
						var slotTimer = Stopwatch.StartNew();
						var batchTime = DateTime.UtcNow;

						// Try to exhaust the publish queue but don't keep a write lock forever
						while (publishQueue.Count > 0 && slotTimer.ElapsedMilliseconds < 25)
						{
							Entry entry;

							if (!publishQueue.TryPeek(out entry) || logSize + messageOverhead + entry.Message.Length > memoryMappedFile.MaxFileSize)
								break;

							if (!publishQueue.TryDequeue(out entry))
								break;

							entry.Id = nextId++;
							entry.Timestamp = batchTime;
							log.Add(entry);
							logSize += messageOverhead + entry.Message.Length;
							Interlocked.Increment(ref messagesSent);
						}

						if (waitingWriters == 0 && publishQueue.Count > 0)
						{
							Task.Factory.StartNew(ProcessPublishQueue);
						}

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
			if (waitingReaders > 0 || MessageReceived == null)
				return;

			Interlocked.Increment(ref waitingReaders);

			lock (messageReaderLock)
			{
				Interlocked.Decrement(ref waitingReaders);

				var data = memoryMappedFile.Read();

				foreach (var entry in DeserializeLog(data).SkipWhile(entry => entry.Id <= lastEntryId))
				{
					lastEntryId = entry.Id;

					if (entry.Instance == instanceId)
						continue;

					if (MessageReceived != null)
						MessageReceived(this, new TinyMessageReceivedEventArgs { Message = MessageEncoding.GetString(entry.Message) });

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
