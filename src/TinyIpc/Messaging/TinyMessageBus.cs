using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET
using System.Runtime.Versioning;
#endif
using System.Threading.Channels;
using MessagePack;
#if NET
using MessagePack.Formatters;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyIpc.IO;

namespace TinyIpc.Messaging;

public partial class TinyMessageBus : IDisposable, ITinyMessageBus
{
	private readonly CancellationTokenSource cancellationTokenSource = new();
	private readonly bool disposeFile;
	private readonly Guid instanceId = Guid.NewGuid();
	private readonly TimeSpan minMessageAge;
	private readonly ITinyMemoryMappedFile memoryMappedFile;
	private readonly SemaphoreSlim messageReaderSemaphore = new(1, 1);
	private readonly ConcurrentDictionary<Guid, Channel<LogEntry>> receiverChannels = new();
	private readonly Task receiverTask;

	private readonly ILogger<TinyMessageBus>? logger;
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

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="options">Options from dependency injection or an OptionsWrapper containing options</param>
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, IOptions<TinyIpcOptions> options, ILogger<TinyMessageBus> logger)
		: this(memoryMappedFile, false, (options ?? throw new ArgumentNullException(nameof(options))).Value.MinMessageAge, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="name">A unique system wide name of this message bus, internal primitives will be prefixed before use</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "False positive")]
	public TinyMessageBus(string name, ILogger<TinyMessageBus>? logger = null)
		: this(new TinyMemoryMappedFile(name), disposeFile: true, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="name">A unique system wide name of this message bus, internal primitives will be prefixed before use</param>
	/// <param name="minMessageAge">The minimum amount of time messages are required to live before removal from the file, default is half a second</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "False positive")]
	public TinyMessageBus(string name, TimeSpan minMessageAge, ILogger<TinyMessageBus>? logger = null)
		: this(new TinyMemoryMappedFile(name), disposeFile: true, minMessageAge, logger)
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
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile, ILogger<TinyMessageBus>? logger = null)
		: this(memoryMappedFile, disposeFile, TinyIpcOptions.DefaultMinMessageAge, logger)
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
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile, TimeSpan minMessageAge, ILogger<TinyMessageBus>? logger = null)
	{
		this.memoryMappedFile = memoryMappedFile ?? throw new ArgumentNullException(nameof(memoryMappedFile));
		this.disposeFile = disposeFile;
		this.minMessageAge = minMessageAge;
		this.logger = logger;

		memoryMappedFile.FileUpdated += WhenFileUpdated;

		lastEntryId = memoryMappedFile.Read(static stream => DeserializeLogBook(stream).LastId);

		receiverTask = Task.Run(ReceiverWorker, cancellationTokenSource.Token);
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
			cancellationTokenSource.Cancel();

			disposed = true;

			foreach (var receiverChannel in receiverChannels)
			{
				receiverChannel.Value.Writer.Complete();
			}

			receiverTask.ConfigureAwait(false).GetAwaiter().GetResult();

			if (disposeFile && memoryMappedFile is IDisposable disposableFile)
			{
				messageReaderSemaphore.Wait();

				try
				{
					disposableFile.Dispose();
				}
				finally
				{
					messageReaderSemaphore.Release();
				}
			}

			messageReaderSemaphore.Dispose();
			cancellationTokenSource.Dispose();
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
	public Task PublishAsync(IReadOnlyList<byte> message)
	{
#if NET7_0_OR_GREATER
		ObjectDisposedException.ThrowIf(disposed, this);
#else
		if (disposed)
			throw new ObjectDisposedException("Can not publish messages when diposed");
#endif

#if NET
		ArgumentNullException.ThrowIfNull(message);
#else
		if (message is null)
			throw new ArgumentNullException(nameof(message), "Message can not be empty");
#endif

#if NET8_0_OR_GREATER
		ArgumentOutOfRangeException.ThrowIfZero(message.Count);
#else
		if (message.Count == 0)
			throw new ArgumentOutOfRangeException(nameof(message), "Message can not be empty");
#endif

		return PublishAsync(new[] { message });
	}

	/// <summary>
	/// Publish a number of messages to the message bus
	/// </summary>
	/// <param name="messages"></param>
	public Task PublishAsync(IReadOnlyList<IReadOnlyList<byte>> messages)
	{
#if NET7_0_OR_GREATER
		ObjectDisposedException.ThrowIf(disposed, this);
#else
		if (disposed)
			throw new ObjectDisposedException("Can not publish messages when diposed");
#endif

		if (messages is null)
			throw new ArgumentNullException(nameof(messages), "Message list can not be empty");

		var publishQueue = new Queue<LogEntry>(messages.Count);
		for (var i = 0; i < messages.Count; i++)
		{
			publishQueue.Enqueue(new LogEntry { Instance = instanceId, Message = messages[i] });

			if (logger is not null)
			{
				LogPublishingMessage(logger, messages[i].Count);
			}
		}

		return Task.Run(async () =>
		{
			while (publishQueue.Count > 0)
			{
				memoryMappedFile.ReadWrite((readStream, writeStream) => PublishMessages(readStream, writeStream, publishQueue, TimeSpan.FromMilliseconds(100)));

				// Give messages in the published log a chance to expire in case it is full
				if (publishQueue.Count > 0)
				{
					await Task.Delay(50).ConfigureAwait(false);
				}
			}
		});
	}

	/// <summary>
	/// Subscribe to messages using an async enumerable.
	/// </summary>
	/// <param name="cancellationToken"></param>
	public async IAsyncEnumerable<IReadOnlyList<byte>> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var id = Guid.NewGuid();
		var receiverChannel = Channel.CreateUnbounded<LogEntry>();

		receiverChannels[id] = receiverChannel;

		try
		{
			await foreach (var entry in StreamEntries(receiverChannel.Reader, cancellationToken).ConfigureAwait(false))
			{
				yield return entry.Message;
			}
		}
		finally
		{
			receiverChannels.TryRemove(id, out _);
		}
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
			if (logSize + LogEntry.Overhead + publishQueue.Peek().Message.Count > memoryMappedFile.MaxFileSize)
				break;

			// Write the entry to the log
			var entry = publishQueue.Dequeue();
			entry.Id = ++logBook.LastId;
			entry.Timestamp = batchTime;
			logBook.AddEntry(entry);

			logSize += LogEntry.Overhead + entry.Message.Count;

			// Skip counting empty messages though, they are skipped on the receiving end anyway
			if (entry.Message.Count == 0)
				continue;

			Interlocked.Increment(ref messagesPublished);
		}

		// Flush the updated log to the memory mapped file
		MessagePackSerializer.Serialize(writeStream, logBook, MessagePackOptions.Instance);
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

		await messageReaderSemaphore.WaitAsync().ConfigureAwait(false);

		try
		{
			Interlocked.Decrement(ref waitingReceivers);

			if (disposed)
				return;

			logBook = memoryMappedFile.Read(static stream => DeserializeLogBook(stream));
			readFrom = lastEntryId;
			lastEntryId = logBook.LastId;

			for (var i = 0; i < logBook.Entries.Count; i++)
			{
				var entry = logBook.Entries[i];

				if (entry.Id <= readFrom || entry.Instance == instanceId || entry.Message.Count == 0)
					continue;

				foreach (var receiverChannel in receiverChannels)
				{
					await receiverChannel.Value.Writer.WriteAsync(entry).ConfigureAwait(false);
				}

				if (logger is not null)
				{
					LogReceivedMessage(logger, entry.Message.Count);
				}
			}
		}
		finally
		{
			messageReaderSemaphore.Release();
		}
	}

	private async Task ReceiverWorker()
	{
		var id = Guid.NewGuid();
		var receiverChannel = Channel.CreateUnbounded<LogEntry>();

		receiverChannels[id] = receiverChannel;

		try
		{
			await foreach (var entry in StreamEntries(receiverChannel.Reader, cancellationTokenSource.Token).ConfigureAwait(false))
			{
				Interlocked.Increment(ref messagesReceived);

				try
				{
					MessageReceived?.Invoke(this, new TinyMessageReceivedEventArgs(entry.Message));
				}
				catch (Exception ex)
				{
					if (logger is not null)
					{
						LogReceiveError(logger, ex, entry.Id);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Expected
		}
		finally
		{
			receiverChannels.TryRemove(id, out _);
		}
	}

	private static async IAsyncEnumerable<LogEntry> StreamEntries(ChannelReader<LogEntry> reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			while (reader.TryRead(out var entry))
			{
				yield return entry;
			}
		}
	}

	private static LogBook DeserializeLogBook(Stream stream)
	{
		if (stream.Length == 0)
			return new LogBook();

		return MessagePackSerializer.Deserialize<LogBook>(stream, MessagePackOptions.Instance);
	}

	[LoggerMessage(0, LogLevel.Debug, "Publishing {message_length} byte message")]
	private static partial void LogPublishingMessage(ILogger logger, int message_length);

	[LoggerMessage(1, LogLevel.Debug, "Received {message_length} byte message")]
	private static partial void LogReceivedMessage(ILogger logger, int message_length);

	[LoggerMessage(2, LogLevel.Error, "Event handler failed handling message with id {id}")]
	private static partial void LogReceiveError(ILogger logger, Exception exception, long id);
}

[MessagePackObject]
public sealed class LogBook
{
	[Key(0)]
	public long LastId { get; set; }

	[Key(1)]
	public IReadOnlyList<LogEntry> Entries { get; set; } = [];

	public void AddEntry(LogEntry entry)
	{
		if (Entries is not List<LogEntry> entries)
		{
			entries = [.. Entries];

			Entries = entries;
		}

		entries.Add(entry);
	}

	public long CalculateLogSize()
	{
		var size = (long)sizeof(long);
		for (var i = 0; i < Entries.Count; i++)
		{
			size += LogEntry.Overhead + Entries[i].Message.Count;
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

		if (Entries is not List<LogEntry> entries)
		{
			entries = [.. Entries];

			Entries = entries;
		}

		entries.RemoveRange(0, i);
	}
}

[MessagePackObject]
public sealed class LogEntry
{
	public static long Overhead { get; }

	[Key(0)]
	public long Id { get; set; }

	[Key(1)]
	public Guid Instance { get; set; }

	[Key(2)]
	public DateTime Timestamp { get; set; }

	[Key(3)]
	public IReadOnlyList<byte> Message { get; set; } = [];

	static LogEntry()
	{
		using var memoryStream = MemoryStreamPool.Manager.GetStream(nameof(LogEntry));
		MessagePackSerializer.Serialize(
			(MemoryStream)memoryStream,
			new LogEntry { Id = long.MaxValue, Instance = Guid.Empty, Timestamp = DateTime.UtcNow },
			MessagePackOptions.Instance
		);
		Overhead = memoryStream.Length;
	}

	// Make sure necessary MessagePack types aren't trimmed
#if NET
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	[SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Unused on purpose")]
	[SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration.", Justification = "Unused on purpose")]
	private static readonly Type byteFormatter = typeof(InterfaceReadOnlyListFormatter<byte>);

	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	[SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Unused on purpose")]
	[SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration.", Justification = "Unused on purpose")]
	private static readonly Type logEntryFormatter = typeof(InterfaceReadOnlyListFormatter<LogEntry>);
#endif
}
