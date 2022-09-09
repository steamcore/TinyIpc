using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
#if NET
using System.Runtime.Versioning;
#endif
using ProtoBuf;
using TinyIpc.IO;

namespace TinyIpc.Messaging;

public class TinyMessageBus : IDisposable, ITinyMessageBus
{
	private readonly bool disposeFile;
	private readonly Guid instanceId = Guid.NewGuid();
	private readonly TimeSpan minMessageAge;
	private readonly ITinyMemoryMappedFile memoryMappedFile;
	private readonly Channel<LogEntry> receiverChannel = Channel.CreateUnbounded<LogEntry>();
	private readonly Task receiverTask;

	private readonly object messageReaderLock = new();

	private bool disposed;
	private long lastEntryId = -1;
	private long messagesPublished;
	private long messagesReceived;
	private int waitingReceivers;

	/// <summary>
	/// Called whenever a new message is received
	/// </summary>
	public event EventHandler<TinyMessageReceivedEventArgs>? MessageReceived;

	public long MessagesPublished => messagesPublished;
	public long MessagesReceived => messagesReceived;

	public static readonly TimeSpan DefaultMinMessageAge = TimeSpan.FromMilliseconds(500);

	static TinyMessageBus()
	{
		Serializer.PrepareSerializer<LogBook>();
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="name">A unique system wide name of this message bus, internal primitives will be prefixed before use</param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Incorrect warning, file is being disposed")]
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyMessageBus(string name)
		: this(new TinyMemoryMappedFile(name), disposeFile: true)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="name">A unique system wide name of this message bus, internal primitives will be prefixed before use</param>
	/// <param name="minMessageAge">The minimum amount of time messages are required to live before removal from the file, default is half a second</param>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Incorrect warning, file is being disposed")]
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyMessageBus(string name, TimeSpan minMessageAge)
		: this(new TinyMemoryMappedFile(name), disposeFile: true, minMessageAge)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="memoryMappedFile">
	/// An instance of a ITinyMemoryMappedFile that will be used to transmit messages.
	/// The file should be larger than the size of all messages that can be expected to be transmitted, including message overhead, per half second.
	/// </param>
	/// <param name="disposeFile">Set to true if the file is to be disposed when this instance is disposed</param>
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile)
		: this(memoryMappedFile, disposeFile, DefaultMinMessageAge)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="memoryMappedFile">
	/// An instance of a ITinyMemoryMappedFile that will be used to transmit messages.
	/// The file should be larger than the size of all messages that can be expected to be transmitted, including message overhead, per minMessageAge.
	/// </param>
	/// <param name="disposeFile">Set to true if the file is to be disposed when this instance is disposed</param>
	/// <param name="minMessageAge">The minimum amount of time messages are required to live before removal from the file, default is half a second</param>
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile, TimeSpan minMessageAge)
	{
		this.memoryMappedFile = memoryMappedFile ?? throw new ArgumentNullException(nameof(memoryMappedFile));
		this.disposeFile = disposeFile;
		this.minMessageAge = minMessageAge;

		memoryMappedFile.FileUpdated += WhenFileUpdated;

		lastEntryId = memoryMappedFile.Read(static stream => DeserializeLogBook(stream).LastId);

		receiverTask = Task.Run(() => ReceiverWorker());
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposed)
			return;

		if (disposing)
		{
			memoryMappedFile.FileUpdated -= WhenFileUpdated;

			disposed = true;

			receiverChannel.Writer.Complete();
			receiverTask.ConfigureAwait(false).GetAwaiter().GetResult();

			lock (messageReaderLock)
			{
				if (disposeFile && memoryMappedFile is IDisposable disposableFile)
				{
					disposableFile.Dispose();
				}
			}
		}
	}

	/// <summary>
	/// Resets MessagesSent and MessagesReceived counters
	/// </summary>
	public void ResetMetrics()
	{
		messagesPublished = 0;
		messagesReceived = 0;
	}

	/// <summary>
	/// Publishes a message to the message bus as soon as possible in a background task
	/// </summary>
	/// <param name="message"></param>
	public Task PublishAsync(byte[] message)
	{
		if (disposed)
			throw new ObjectDisposedException("Can not publish messages when diposed");

		if (message is null || message.Length == 0)
			throw new ArgumentException("Message can not be empty", nameof(message));

		return PublishAsync(new[] { message });
	}

	/// <summary>
	/// Publish a number of messages to the message bus
	/// </summary>
	/// <param name="messages"></param>
	public Task PublishAsync(IReadOnlyList<byte[]> messages)
	{
		if (disposed)
			throw new ObjectDisposedException("Can not publish messages when diposed");

		if (messages is null)
			throw new ArgumentNullException(nameof(messages), "Message list can not be empty");

		var publishQueue = new Queue<LogEntry>(messages.Count);
		for (var i = 0; i < messages.Count; i++)
		{
			publishQueue.Enqueue(new LogEntry { Instance = instanceId, Message = messages[i] });
		}

		return Task.Run(async () =>
		{
			while (publishQueue.Count > 0)
			{
				memoryMappedFile.ReadWrite((readStream, writeStream) => PublishMessages(readStream, writeStream, publishQueue, TimeSpan.FromMilliseconds(100)));

				// Give messages in the published log a chance to expire in case it is full
				if (publishQueue.Count > 0)
				{
					await Task.Delay(50);
				}
			}
		});
	}

	private void PublishMessages(Stream readStream, Stream writeStream, Queue<LogEntry> publishQueue, TimeSpan timeout)
	{
		var logBook = DeserializeLogBook(readStream);
		logBook.TrimStaleEntries(DateTime.UtcNow - minMessageAge);
		var logSize = logBook.CalculateLogSize();

		// Start slot timer after deserializing log so deserialization doesn't starve the slot time
		var slotTimer = Stopwatch.StartNew();
		var batchTime = DateTime.UtcNow;

		// Try to exhaust the publish queue but don't keep a write lock forever
		while (publishQueue.Count > 0 && slotTimer.Elapsed < timeout)
		{
			// Check if the next message will fit in the log
			if (logSize + LogEntry.Overhead + publishQueue.Peek().Message.Length > memoryMappedFile.MaxFileSize)
				break;

			// Write the entry to the log
			var entry = publishQueue.Dequeue();
			entry.Id = ++logBook.LastId;
			entry.Timestamp = batchTime;
			logBook.Entries.Add(entry);

			logSize += LogEntry.Overhead + entry.Message.Length;

			// Skip counting empty messages though, they are skipped on the receiving end anyway
			if (entry.Message.Length == 0)
				continue;

			Interlocked.Increment(ref messagesPublished);
		}

		// Flush the updated log to the memory mapped file
		Serializer.Serialize(writeStream, logBook);
	}

	internal Task ReadAsync()
	{
		return ReceiveMessages();
	}

	private void WhenFileUpdated(object? sender, EventArgs args)
	{
		_ = ReceiveMessages();
	}

	private async Task ReceiveMessages()
	{
		if (waitingReceivers > 0 || disposed)
			return;

		Interlocked.Increment(ref waitingReceivers);

		LogBook logBook;
		long readFrom;

		lock (messageReaderLock)
		{
			Interlocked.Decrement(ref waitingReceivers);

			if (disposed)
				return;

			logBook = memoryMappedFile.Read(static stream => DeserializeLogBook(stream));
			readFrom = lastEntryId;
			lastEntryId = logBook.LastId;
		}

		foreach (var entry in logBook.Entries)
		{
			if (entry.Id <= readFrom || entry.Instance == instanceId || entry.Message.Length == 0)
				continue;

			await receiverChannel.Writer.WriteAsync(entry);
		}
	}

	[SuppressMessage("Roslynator", "RCS1075:Avoid empty catch clause that catches System.Exception.", Justification = "Temporarily suppressed until logging is added")]
	private async Task ReceiverWorker()
	{
		while (await receiverChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
		{
			while (receiverChannel.Reader.TryRead(out var entry))
			{
				Interlocked.Increment(ref messagesReceived);

				try
				{
					MessageReceived?.Invoke(this, new TinyMessageReceivedEventArgs(entry.Message));
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private static LogBook DeserializeLogBook(Stream stream)
	{
		if (stream.Length == 0)
			return new LogBook();

		return Serializer.Deserialize<LogBook>(stream);
	}

	[ProtoContract]
	private class LogBook
	{
		[ProtoMember(1)]
		public long LastId { get; set; }

		[ProtoMember(2)]
		public List<LogEntry> Entries { get; set; } = new();

		public long CalculateLogSize()
		{
			var size = (long)sizeof(long);
			for (var i = 0; i < Entries.Count; i++)
			{
				size += LogEntry.Overhead + Entries[i].Message.Length;
			}
			return size;
		}

		public void TrimStaleEntries(DateTime cutoffPoint)
		{
			var i = 0;
			for (; i < Entries.Count; i++)
			{
				if (Entries[i].Timestamp >= cutoffPoint)
					break;
			}
			Entries.RemoveRange(0, i);
		}
	}

	[ProtoContract]
	private class LogEntry
	{
		public static long Overhead { get; }

		[ProtoMember(1)]
		public long Id { get; set; }

		[ProtoMember(2)]
		public Guid Instance { get; set; }

		[ProtoMember(3)]
		public DateTime Timestamp { get; set; }

		[ProtoMember(4)]
		public byte[] Message { get; set; } = Array.Empty<byte>();

		static LogEntry()
		{
			using var memoryStream = MemoryStreamPool.Manager.GetStream(nameof(LogEntry));
			Serializer.Serialize(memoryStream, new LogEntry { Id = long.MaxValue, Instance = Guid.Empty, Timestamp = DateTime.UtcNow });
			Overhead = memoryStream.Length;
		}
	}
}
