using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyIpc.Synchronization;

namespace TinyIpc.IO;

/// <summary>
/// Wraps a MemoryMappedFile with inter process synchronization and signaling
/// </summary>
public partial class TinyMemoryMappedFile : IDisposable, ITinyMemoryMappedFile
{
	private readonly Task fileWatcherTask;
	private readonly MemoryMappedFile memoryMappedFile;
	private readonly ITinyReadWriteLock readWriteLock;
	private readonly bool disposeLock;
	private readonly EventWaitHandle fileWaitHandle;
	private readonly ILogger<TinyMemoryMappedFile>? logger;

	private readonly EventWaitHandle disposeWaitHandle;
	private bool disposed;

	public event EventHandler? FileUpdated;

	public long MaxFileSize { get; }

	/// <summary>
	/// Initializes a new instance of the TinyMemoryMappedFile class.
	/// </summary>
	/// <param name="options">Options from dependency injection or an OptionsWrapper containing options</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyMemoryMappedFile(ITinyReadWriteLock readWriteLock, IOptions<TinyIpcOptions> options, ILogger<TinyMemoryMappedFile> logger)
		: this((options ?? throw new ArgumentNullException(nameof(options))).Value.Name, options.Value.MaxFileSize, readWriteLock, disposeLock: false, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMemoryMappedFile class.
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyMemoryMappedFile(string name, ILogger<TinyMemoryMappedFile>? logger = null)
		: this(name, TinyIpcOptions.DefaultMaxFileSize, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMemoryMappedFile class.
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
	/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "False positive")]
	public TinyMemoryMappedFile(string name, long maxFileSize, ILogger<TinyMemoryMappedFile>? logger = null)
		: this(name, maxFileSize, new TinyReadWriteLock(name), disposeLock: true, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMemoryMappedFile class.
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
	/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
	/// <param name="readWriteLock">A read/write lock that will be used to control access to the memory mapped file</param>
	/// <param name="disposeLock">Set to true if the read/write lock is to be disposed when this instance is disposed</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyMemoryMappedFile(string name, long maxFileSize, ITinyReadWriteLock readWriteLock, bool disposeLock, ILogger<TinyMemoryMappedFile>? logger = null)
		: this(CreateOrOpenMemoryMappedFile(name, maxFileSize), CreateEventWaitHandle(name), maxFileSize, readWriteLock, disposeLock, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyMemoryMappedFile class.
	/// </summary>
	/// <param name="memoryMappedFile">An instance of a memory mapped file</param>
	/// <param name="fileWaitHandle">A manual reset EventWaitHandle that is to be used to signal file changes</param>
	/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
	/// <param name="readWriteLock">A read/write lock that will be used to control access to the memory mapped file</param>
	/// <param name="disposeLock">Set to true if the read/write lock is to be disposed when this instance is disposed</param>
	public TinyMemoryMappedFile(MemoryMappedFile memoryMappedFile, EventWaitHandle fileWaitHandle, long maxFileSize, ITinyReadWriteLock readWriteLock, bool disposeLock, ILogger<TinyMemoryMappedFile>? logger = null)
	{
		this.readWriteLock = readWriteLock ?? throw new ArgumentNullException(nameof(readWriteLock));
		this.memoryMappedFile = memoryMappedFile ?? throw new ArgumentNullException(nameof(memoryMappedFile));
		this.fileWaitHandle = fileWaitHandle ?? throw new ArgumentNullException(nameof(fileWaitHandle));
		this.disposeLock = disposeLock;
		this.logger = logger;

		MaxFileSize = maxFileSize;

		disposeWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

		fileWatcherTask = Task.Run(() => FileWatcher());
	}

	~TinyMemoryMappedFile()
	{
		Dispose(false);
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

		// Always set the dispose wait handle even when dispised  by the finalizer
		// otherwize the file watcher task will needleessly have to wait for its timeout.
		disposeWaitHandle?.Set();
		fileWatcherTask?.Wait(TinyIpcOptions.DefaultWaitTimeout);

		if (disposing)
		{
			memoryMappedFile.Dispose();

			if (disposeLock && readWriteLock is IDisposable disposableLock)
			{
				disposableLock.Dispose();
			}

			fileWaitHandle.Dispose();
			disposeWaitHandle?.Dispose();
		}

		disposed = true;
	}

	/// <summary>
	/// Gets the file size
	/// </summary>
	/// <returns>File size</returns>
	public int GetFileSize()
	{
		using var readLock = readWriteLock.AcquireReadLock();
		using var accessor = memoryMappedFile.CreateViewAccessor();
		var fileSize = accessor.ReadInt32(0);

		if (logger is not null)
		{
			LogReadFileSize(logger, fileSize);
		}

		return fileSize;
	}

	/// <summary>
	/// Reads the content of the memory mapped file with a read lock in place.
	/// </summary>
	/// <returns>File content</returns>
	public T Read<T>(Func<MemoryStream, T> readData)
	{
#if NET
		ArgumentNullException.ThrowIfNull(readData);
#else
		if (readData is null)
			throw new ArgumentNullException(nameof(readData));
#endif

		using var readLock = readWriteLock.AcquireReadLock();
		using var readStream = MemoryStreamPool.Manager.GetStream(nameof(TinyMemoryMappedFile));

		InternalRead(readStream);
		readStream.Seek(0, SeekOrigin.Begin);

		if (logger is not null)
		{
			LogReadFile(logger, readStream.Length);
		}

		return readData(readStream);
	}

	/// <summary>
	/// Replaces the content of the memory mapped file with a write lock in place.
	/// </summary>
	public void Write(MemoryStream data)
	{
#if NET
		ArgumentNullException.ThrowIfNull(data);
#else
		if (data is null)
			throw new ArgumentNullException(nameof(data));
#endif

#if NET8_0_OR_GREATER
		ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, MaxFileSize);
#else
		if (data.Length > MaxFileSize)
			throw new ArgumentOutOfRangeException(nameof(data), "Length greater than max file size");
#endif

		using var writeLock = readWriteLock.AcquireWriteLock();

		try
		{
			InternalWrite(data);

			if (logger is not null)
			{
				LogWroteFile(logger, data.Length);
			}
		}
		finally
		{
			fileWaitHandle.Set();
			fileWaitHandle.Reset();
		}
	}

	/// <summary>
	/// Reads and then replaces the content of the memory mapped file with a write lock in place.
	/// </summary>
	public void ReadWrite(Action<MemoryStream, MemoryStream> updateFunc)
	{
#if NET
		ArgumentNullException.ThrowIfNull(updateFunc);
#else
		if (updateFunc is null)
			throw new ArgumentNullException(nameof(updateFunc));
#endif

		using var writeLock = readWriteLock.AcquireWriteLock();

		try
		{
			using var readStream = MemoryStreamPool.Manager.GetStream(nameof(TinyMemoryMappedFile));
			using var writeStream = MemoryStreamPool.Manager.GetStream(nameof(TinyMemoryMappedFile));

			InternalRead(readStream);
			readStream.Seek(0, SeekOrigin.Begin);

			if (logger is not null)
			{
				LogReadFile(logger, readStream.Length);
			}

			updateFunc(readStream, writeStream);
			writeStream.Seek(0, SeekOrigin.Begin);

			InternalWrite(writeStream);

			if (logger is not null)
			{
				LogWroteFile(logger, writeStream.Length);
			}
		}
		finally
		{
			fileWaitHandle.Set();
			fileWaitHandle.Reset();
		}
	}

	private void FileWatcher()
	{
		var waitHandles = new[]
		{
			disposeWaitHandle,
			fileWaitHandle
		};

		while (!disposed)
		{
			var result = WaitHandle.WaitAny(waitHandles, TinyIpcOptions.DefaultWaitTimeout);

			// Triggers when disposed
			if (result == 0 || disposed)
				return;

			// Triggers when the file is changed
			if (result == 1)
				FileUpdated?.Invoke(this, EventArgs.Empty);
		}
	}

	private void InternalRead(MemoryStream output)
	{
		using var accessor = memoryMappedFile.CreateViewAccessor();
		var length = accessor.ReadInt32(0);

		var buffer = ArrayPool<byte>.Shared.Rent(length);
		try
		{
			accessor.ReadArray(sizeof(int), buffer, 0, length);
			output.Write(buffer, 0, length);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	private void InternalWrite(MemoryStream input)
	{
		if (input.Length > MaxFileSize)
			throw new ArgumentOutOfRangeException(nameof(input), "Length greater than max file size");

		var length = (int)input.Length;
		var buffer = ArrayPool<byte>.Shared.Rent(length);
		try
		{
			input.Read(buffer, 0, length);

			using var accessor = memoryMappedFile.CreateViewAccessor();
			accessor.Write(0, length);
			accessor.WriteArray(sizeof(int), buffer, 0, length);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	/// <summary>
	/// Create or open a MemoryMappedFile that can be used to construct a TinyMemoryMappedFile
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended</param>
	/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
	/// <returns>A system wide MemoryMappedFile</returns>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static MemoryMappedFile CreateOrOpenMemoryMappedFile(string name, long maxFileSize)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("File must be named", nameof(name));

		if (maxFileSize <= 0)
			throw new ArgumentException("Max file size can not be less than 1 byte", nameof(maxFileSize));

		return MemoryMappedFile.CreateOrOpen("TinyMemoryMappedFile_MemoryMappedFile_" + name, maxFileSize + sizeof(int));
	}

	/// <summary>
	/// Create or open an EventWaitHandle that can be used to construct a TinyMemoryMappedFile
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended</param>
	/// <returns>A system wide EventWaitHandle</returns>
	public static EventWaitHandle CreateEventWaitHandle(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("EventWaitHandle must be named", nameof(name));

		return new EventWaitHandle(false, EventResetMode.ManualReset, "TinyMemoryMappedFile_WaitHandle_" + name);
	}

	[LoggerMessage(0, LogLevel.Trace, "Read file size, memory mapped file was {file_size} bytes")]
	private static partial void LogReadFileSize(ILogger logger, long file_size);

	[LoggerMessage(1, LogLevel.Trace, "Read {file_size} bytes from memory mapped file")]
	private static partial void LogReadFile(ILogger logger, long file_size);

	[LoggerMessage(2, LogLevel.Trace, "Wrote {file_size} bytes to memory mapped file")]
	private static partial void LogWroteFile(ILogger logger, long file_size);
}
