using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProtoBuf;

namespace TinyIpc
{
	public class TinyMessageBus : IDisposable, ITinyMessageBus
	{
		private readonly object messageReaderLock = new object();
		private readonly object messagePublisherLock = new object();

		private long lastEntryId;

		private readonly bool disposeFileOnExit;
		private readonly Guid instanceId = Guid.NewGuid();
		private readonly ConcurrentQueue<Entry> publishQueue = new ConcurrentQueue<Entry>();
		private readonly TimeSpan maxMessageAge;
		private readonly ITinyMemoryMappedFile memoryMappedFile;

		/// <summary>
		/// Called whenever a new message is received
		/// </summary>
		public event EventHandler<TinyMessageReceivedEventArgs> MessageReceived;

		public long MessagesSent { get; private set; }
		public long MessagesReceived { get; private set; }

		public TinyMessageBus(string name)
			: this(new TinyMemoryMappedFile(name))
		{
			disposeFileOnExit = true;
		}

		public TinyMessageBus(string name, TimeSpan maxMessageAge)
			: this(new TinyMemoryMappedFile(name), maxMessageAge)
		{
			disposeFileOnExit = true;
		}

		public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile)
			: this(memoryMappedFile, TimeSpan.FromMilliseconds(200))
		{
		}

		public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, TimeSpan maxMessageAge)
		{
			Serializer.PrepareSerializer<Entry>();

			this.maxMessageAge = maxMessageAge;
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

			if (disposeFileOnExit && memoryMappedFile is TinyMemoryMappedFile)
			{
				(memoryMappedFile as TinyMemoryMappedFile).Dispose();
			}
		}

		/// <summary>
		/// Resets MessagesSent and MessagesReceived counters
		/// </summary>
		public void ResetMetrics()
		{
			MessagesSent = 0;
			MessagesReceived = 0;
		}

		/// <summary>
		/// Publishes a message to the message bus as soon as possible in an async task
		/// </summary>
		/// <param name="message"></param>
		public Task PublishAsync(string message)
		{
			publishQueue.Enqueue(new Entry { Instance = instanceId, Message = message });

			return Task.Factory.StartNew(ProcessPublishQueue);
		}

		private void ProcessPublishQueue()
		{
			lock (messagePublisherLock)
			{
				if (publishQueue.Count == 0)
					return;

				memoryMappedFile.ReadWrite(
					data =>
					{
						var batchTime = DateTime.UtcNow;
						var log = DeserializeLog(data).SkipWhile(entry => entry.Timestamp + maxMessageAge < DateTime.UtcNow).ToList();
						var lastEntry = log.LastOrDefault();
						var nextId = Math.Max(lastEntryId, lastEntry != null ? lastEntry.Id : 0) + 1;

						while (publishQueue.Count > 0)
						{
							Entry entry;
							if (!publishQueue.TryDequeue(out entry))
								break;
							entry.Id = nextId++;
							entry.Timestamp = batchTime;
							log.Add(entry);
							MessagesSent++;
						}

						return SerializeLog(log);
					});
			}
		}

		private void HandleIncomingMessages(object sender, EventArgs args)
		{
			lock (messageReaderLock)
			{
				var data = memoryMappedFile.Read();

				foreach (var entry in DeserializeLog(data).SkipWhile(entry => entry.Id <= lastEntryId || entry.Timestamp + maxMessageAge < DateTime.UtcNow))
				{
					lastEntryId = entry.Id;

					if (entry.Instance == instanceId)
						continue;

					if (MessageReceived != null)
						MessageReceived(this, new TinyMessageReceivedEventArgs { Message = entry.Message });

					MessagesReceived++;
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

		private static byte[] SerializeLog(List<Entry> log)
		{
			using (var memoryStream = new MemoryStream(log.Count * 128))
			{
				Serializer.Serialize(memoryStream, log);
				return memoryStream.ToArray();
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
			public string Message { get; set; }
		}
	}
}
