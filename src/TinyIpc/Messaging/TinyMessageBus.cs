using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET
using System.Runtime.Versioning;
#endif
using System.Threading.Channels;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyIpc.IO;

namespace TinyIpc.Messaging;

public partial class TinyMessageBus : ITinyMessageBus
{
	private readonly CancellationTokenSource cancellationTokenSource = new();
	private readonly bool disposeFile;
	private readonly Guid instanceId = Guid.NewGuid();
	private readonly ITinyMemoryMappedFile memoryMappedFile;
	private readonly TimeProvider timeProvider;
	private readonly IOptions<TinyIpcOptions> options;
	private readonly SemaphoreSlim messageReaderSemaphore = new(1, 1);
	private readonly ConcurrentDictionary<Guid, Channel<LogEntry>> receiverChannels = new();
	private readonly Task receiverTask;

	private readonly ILogger<TinyMessageBus>? logger;
	private bool disposed;
	private long lastEntryId = -1;
	private long messagesPublished;
	private long messagesReceived;

	/// <summary>
	/// Called whenever a new message is received
	/// </summary>
	public event EventHandler<TinyMessageReceivedEventArgs>? MessageReceived;

	public long MessagesPublished => Interlocked.Read(ref messagesPublished);
	public long MessagesReceived => Interlocked.Read(ref messagesReceived);

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="name">A unique system wide name of this message bus, internal primitives will be prefixed before use</param>
	/// <param name="options">Options from dependency injection or an OptionsWrapper containing options</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyMessageBus(string name, IOptions<TinyIpcOptions>? options = null, ILogger<TinyMessageBus>? logger = null)
		: this(new TinyMemoryMappedFile(name), disposeFile: true, TimeProvider.System, options ?? new OptionsWrapper<TinyIpcOptions>(new TinyIpcOptions()), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="memoryMappedFile">
	/// An instance of a ITinyMemoryMappedFile that will be used to transmit messages.
	/// The file should be larger than the size of all messages that can be expected to be transmitted, including message overhead, per MinMessageAge in the options.
	/// </param>
	/// <param name="options">Options from dependency injection or an OptionsWrapper containing options</param>
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, IOptions<TinyIpcOptions>? options = null, ILogger<TinyMessageBus>? logger = null)
		: this(memoryMappedFile, disposeFile: false, TimeProvider.System, options ?? new OptionsWrapper<TinyIpcOptions>(new TinyIpcOptions()), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="memoryMappedFile">
	/// An instance of a ITinyMemoryMappedFile that will be used to transmit messages.
	/// The file should be larger than the size of all messages that can be expected to be transmitted, including message overhead, per MinMessageAge in the options.
	/// </param>
	/// <param name="disposeFile">Set to true if the file is to be disposed when this instance is disposed</param>
	/// <param name="options">Options from dependency injection or an OptionsWrapper containing options</param>
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile, IOptions<TinyIpcOptions>? options = null, ILogger<TinyMessageBus>? logger = null)
		: this(memoryMappedFile, disposeFile, TimeProvider.System, options ?? new OptionsWrapper<TinyIpcOptions>(new TinyIpcOptions()), logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMessageBus class.
	/// </summary>
	/// <param name="memoryMappedFile">
	/// An instance of a ITinyMemoryMappedFile that will be used to transmit messages.
	/// The file should be larger than the size of all messages that can be expected to be transmitted, including message overhead, per MinMessageAge in the options.
	/// </param>
	/// <param name="disposeFile">Set to true if the file is to be disposed when this instance is disposed</param>
	/// <param name="options">Options from dependency injection or an OptionsWrapper containing options</param>
	public TinyMessageBus(ITinyMemoryMappedFile memoryMappedFile, bool disposeFile, TimeProvider timeProvider, IOptions<TinyIpcOptions> options, ILogger<TinyMessageBus>? logger = null)
	{
		this.memoryMappedFile = memoryMappedFile ?? throw new ArgumentNullException(nameof(memoryMappedFile));
		this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		this.options = options ?? throw new ArgumentNullException(nameof(options));
		this.disposeFile = disposeFile;
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

			try
			{
				receiverTask.ConfigureAwait(false).GetAwaiter().GetResult();
			}
			catch (TaskCanceledException)
			{
				// Expected
			}

			if (disposeFile)
			{
				if (!messageReaderSemaphore.Wait(options.Value.WaitTimeout))
				{
					throw new TimeoutException("Could not acquire message reader semaphore for disposal");
				}

				try
				{
					memoryMappedFile.Dispose();
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
		Interlocked.Exchange(ref messagesPublished, 0);
		Interlocked.Exchange(ref messagesReceived, 0);
	}

	/// <summary>
	/// Publishes a message to the message bus as soon as possible in a background task
	/// </summary>
	/// <param name="message"></param>
	public Task PublishAsync(BinaryData message)
	{
#if NET8_0_OR_GREATER
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
		ArgumentOutOfRangeException.ThrowIfZero(message.Length);
#else
		if (message.Length == 0)
			throw new ArgumentOutOfRangeException(nameof(message), "Message can not be empty");
#endif

		return PublishAsync([message]);
	}

	/// <summary>
	/// Publish a number of messages to the message bus
	/// </summary>
	/// <param name="messages"></param>
	public Task PublishAsync(IReadOnlyList<BinaryData> messages)
	{
#if NET8_0_OR_GREATER
		ObjectDisposedException.ThrowIf(disposed, this);
#else
		if (disposed)
			throw new ObjectDisposedException("Can not publish messages when diposed");
#endif

#if NET
		ArgumentNullException.ThrowIfNull(messages);
#else
		if (messages is null)
			throw new ArgumentNullException(nameof(messages), "Message list can not be null");
#endif

		if (messages.Count == 0)
		{
			return Task.CompletedTask;
		}

		return Task.Run(async () =>
		{
			if (logger is not null)
			{
				foreach (var message in messages)
				{
					LogPublishingMessage(logger, message.Length);
				}
			}

			var publishQueue = new Queue<BinaryData>(messages);

			while (publishQueue.Count > 0)
			{
				memoryMappedFile.ReadWrite((readStream, writeStream) =>
				{
					var publishCount = PublishMessages(readStream, writeStream, publishQueue, TimeSpan.FromMilliseconds(100));

					Interlocked.Add(ref messagesPublished, publishCount);
				});

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
	public async IAsyncEnumerable<BinaryData> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var id = Guid.NewGuid();
		var receiverChannel = Channel.CreateUnbounded<LogEntry>();

		receiverChannels[id] = receiverChannel;

		try
		{
			await foreach (var entry in StreamEntries(receiverChannel.Reader, cancellationToken).ConfigureAwait(false))
			{
				yield return BinaryData.FromBytes(entry.Message);
			}
		}
		finally
		{
			receiverChannels.TryRemove(id, out _);
		}
	}

	private int PublishMessages(Stream readStream, Stream writeStream, Queue<BinaryData> publishQueue, TimeSpan timeout)
	{
		var logBook = DeserializeLogBook(readStream);
		var lastId = logBook.LastId;
		var entriesToTrim = logBook.CountEntriesToTrim(timeProvider, options.Value.MinMessageAge);
		var logSize = logBook.CalculateLogSize(entriesToTrim);
		var entries = entriesToTrim == 0 ? logBook.Entries : logBook.Entries.Skip(entriesToTrim).ToImmutableList();

		// Start slot timer after deserializing log so deserialization doesn't starve the slot time
		var slotTimer = Stopwatch.StartNew();
		var batchTime = timeProvider.GetTimestamp();
		var publishCount = 0;

		// Try to exhaust the publish queue but don't keep a write lock forever
		while (publishQueue.Count > 0 && slotTimer.Elapsed < timeout)
		{
			// Check if the next message will fit in the log
			if (logSize + LogEntry.Overhead + publishQueue.Peek().Length > memoryMappedFile.MaxFileSize)
				break;

			// Write the entry to the log
			var message = publishQueue.Dequeue();

			// Skip empty messages though, they would be skipped on the receiving end anyway
			if (message.Length == 0)
				continue;

			entries = entries.Add(new()
			{
				Id = ++lastId,
				Instance = instanceId,
				Message = message,
				Timestamp = batchTime
			});

			logSize += LogEntry.Overhead + message.Length;
			publishCount++;
		}

		// Flush the updated log to the memory mapped file
		MessagePackSerializer.Serialize(writeStream, new LogBook(lastId, entries), MessagePackOptions.Instance);

		return publishCount;
	}

	internal Task ReadAsync()
	{
		return ReceiveMessages();
	}

	private void WhenFileUpdated(object? sender, EventArgs args)
	{
		_ = ReceiveMessages();
	}

	/// <summary>
	/// Receives messages from the memory mapped file and forwards them to the registered receiver channels.
	/// </summary>
	private async Task ReceiveMessages()
	{
		if (disposed)
			return;

		LogBook logBook;
		long readFrom;

		if (!await messageReaderSemaphore.WaitAsync(options.Value.WaitTimeout).ConfigureAwait(false))
		{
			throw new TimeoutException("Could not acquire message reader semaphore");
		}

		try
		{
			if (disposed)
				return;

			logBook = memoryMappedFile.Read(static stream => DeserializeLogBook(stream));
			readFrom = lastEntryId;
			lastEntryId = logBook.LastId;
			var readCount = 0;

			for (var i = 0; i < logBook.Entries.Count; i++)
			{
				var entry = logBook.Entries[i];

				if (entry.Id <= readFrom || entry.Instance == instanceId || entry.Message.Length == 0)
					continue;

				readCount++;

				foreach (var receiverChannel in receiverChannels)
				{
					await receiverChannel.Value.Writer.WriteAsync(entry).ConfigureAwait(false);
				}

				if (logger is not null)
				{
					LogReceivedMessage(logger, entry.Message.Length);
				}
			}

			Interlocked.Add(ref messagesReceived, readCount);
		}
		finally
		{
			messageReaderSemaphore.Release();
		}
	}

	/// <summary>
	/// Worker task that processes messages from the receiver channels and invokes the MessageReceived event.
	/// </summary>
	private async Task ReceiverWorker()
	{
		var id = Guid.NewGuid();
		var receiverChannel = Channel.CreateUnbounded<LogEntry>();

		receiverChannels[id] = receiverChannel;

		try
		{
			await foreach (var entry in StreamEntries(receiverChannel.Reader, cancellationTokenSource.Token).ConfigureAwait(false))
			{
				try
				{
					MessageReceived?.Invoke(this, new TinyMessageReceivedEventArgs(BinaryData.FromBytes(entry.Message)));
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
		{
#if NET8_0_OR_GREATER
			return new LogBook(0, []);
#else
			return new LogBook(0, ImmutableList<LogEntry>.Empty);
#endif
		}

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
public readonly record struct LogBook(
	[property: Key(0)] long LastId,
	[property: Key(1)] ImmutableList<LogEntry> Entries
)
{
	public long CalculateLogSize(int start)
	{
		var size = (long)sizeof(long);
		for (var i = start; i < Entries.Count; i++)
		{
			size += LogEntry.Overhead + Entries[i].Message.Length;
		}
		return size;
	}

	public int CountEntriesToTrim(TimeProvider timeProvider, TimeSpan minMessageAge)
	{
#if NET
		ArgumentNullException.ThrowIfNull(timeProvider);
#else
		if (timeProvider is null)
			throw new ArgumentNullException(nameof(timeProvider));
#endif

		if (Entries.Count == 0)
		{
			return 0;
		}

		var cutoffPoint = timeProvider.GetTimestamp() - minMessageAge.Ticks / TimeSpan.TicksPerSecond * Stopwatch.Frequency;

		var i = 0;
		for (; i < Entries.Count; i++)
		{
			if (Entries[i].Timestamp >= cutoffPoint)
				break;
		}

		return i;
	}
}

[MessagePackObject]
public readonly record struct LogEntry(
	[property: Key(0)] long Id,
	[property: Key(1)] Guid Instance,
	[property: Key(2)] long Timestamp,
	[property: Key(3)] ReadOnlyMemory<byte> Message
)
{
	public static long Overhead { get; }

	static LogEntry()
	{
		using var memoryStream = MemoryStreamPool.Manager.GetStream(nameof(LogEntry));

		MessagePackSerializer.Serialize(
			(MemoryStream)memoryStream,
			new LogEntry { Id = long.MaxValue, Instance = Guid.Empty, Timestamp = TimeProvider.System.GetTimestamp() },
			MessagePackOptions.Instance
		);

		Overhead = memoryStream.Length;
	}
}
