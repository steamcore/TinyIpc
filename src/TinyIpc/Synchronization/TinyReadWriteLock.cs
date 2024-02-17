#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TinyIpc.Synchronization;

/// <summary>
/// Implements a simple inter process read/write locking mechanism
/// Inspired by http://www.joecheng.com/blog/entries/Writinganinter-processRea.html
/// </summary>
public partial class TinyReadWriteLock : IDisposable, ITinyReadWriteLock
{
	private readonly Mutex mutex;
	private readonly Semaphore semaphore;
	private readonly SemaphoreSlim synchronizationLock = new(1, 1);
	private readonly int maxReaderCount;
	private readonly TimeSpan waitTimeout;
	private readonly ILogger<TinyReadWriteLock>? logger;

	private bool disposed;
	private int readLocks;
	private bool writeLock;

	public bool IsReaderLockHeld => readLocks > 0;
	public bool IsWriterLockHeld => writeLock;

	/// <summary>
	/// Initializes a new instance of the TinyReadWriteLock class.
	/// </summary>
	/// <param name="options">Options from dependency injection or an OptionsWrapper containing options</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyReadWriteLock(IOptions<TinyIpcOptions> options, ILogger<TinyReadWriteLock> logger)
		: this((options ?? throw new ArgumentNullException(nameof(options))).Value.Name, options.Value.MaxReaderCount, options.Value.WaitTimeout, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyReadWriteLock class.
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyReadWriteLock(string name, ILogger<TinyReadWriteLock>? logger = null)
		: this(name, TinyIpcOptions.DefaultMaxReaderCount, TinyIpcOptions.DefaultWaitTimeout, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyReadWriteLock class.
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
	/// <param name="maxReaderCount">Maxium simultaneous readers, default is 6</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyReadWriteLock(string name, int maxReaderCount, ILogger<TinyReadWriteLock>? logger = null)
		: this(name, maxReaderCount, TinyIpcOptions.DefaultWaitTimeout, logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the TinyReadWriteLock class.
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
	/// <param name="maxReaderCount">Maxium simultaneous readers, default is 6</param>
	/// <param name="waitTimeout">How long to wait before giving up aquiring read and write locks</param>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyReadWriteLock(string name, int maxReaderCount, TimeSpan waitTimeout, ILogger<TinyReadWriteLock>? logger = null)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Lock must be named", nameof(name));

		if (maxReaderCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxReaderCount), "Need at least one reader");

		this.maxReaderCount = maxReaderCount;
		this.waitTimeout = waitTimeout;
		this.logger = logger;

		mutex = CreateMutex(name);
		semaphore = CreateSemaphore(name, maxReaderCount);
	}

	/// <summary>
	/// Initializes a new instance of the TinyReadWriteLock class.
	/// </summary>
	/// <param name="mutex">Should be a system wide Mutex that is used to control access to the semaphore</param>
	/// <param name="semaphore">Should be a system wide Semaphore with at least one max count, default is 6</param>
	/// <param name="maxReaderCount">Maxium simultaneous readers, must be the same as the Semaphore count, default is 6</param>
	/// <param name="waitTimeout">How long to wait before giving up aquiring read and write locks</param>
	public TinyReadWriteLock(Mutex mutex, Semaphore semaphore, int maxReaderCount, TimeSpan waitTimeout, ILogger<TinyReadWriteLock>? logger = null)
	{
		if (maxReaderCount <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxReaderCount), "Need at least one reader");

		this.maxReaderCount = maxReaderCount;
		this.waitTimeout = waitTimeout;
		this.logger = logger;
		this.mutex = mutex ?? throw new ArgumentNullException(nameof(mutex));
		this.semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
	}

	~TinyReadWriteLock()
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

		if (disposing)
		{
			// The Mutex and Semaphore MUST NOT be disposed while they are being held,
			// it is better to throw and not dispose them at all rather than to dispose
			// them and prevent some other thread to release them or it might break other
			// processes as the locks may be held indefinitely.
			if (!synchronizationLock.Wait(waitTimeout))
				throw new TimeoutException("Could not dispose of locks, timed out waiting for SemaphoreSlim");
		}

		disposed = true;

		// Always release held Mutex and Semaphore even when triggered by the finalizer
		mutex?.Dispose();
		semaphore?.Dispose();
		synchronizationLock?.Dispose();
	}

	/// <summary>
	/// Acquire a read lock, only one read lock can be held by once instance
	/// but multiple read locks may be held at the same time by multiple instances
	/// </summary>
	/// <returns>A disposable that releases the read lock</returns>
	public IDisposable AcquireReadLock()
	{
#if NET7_0_OR_GREATER
		ObjectDisposedException.ThrowIf(disposed, this);
#else
		if (disposed)
			throw new ObjectDisposedException(nameof(TinyReadWriteLock));
#endif

		if (!synchronizationLock.Wait(waitTimeout))
			throw new TimeoutException("Could not acquire read lock, timed out waiting for SemaphoreSlim");

		if (!mutex.WaitOne(waitTimeout))
		{
			synchronizationLock.Release();
			throw new TimeoutException("Could not acquire read lock, timed out waiting for Mutex");
		}

		try
		{
			if (!semaphore.WaitOne(waitTimeout))
			{
				synchronizationLock.Release();
				throw new TimeoutException("Could not acquire read lock, timed out waiting for Semaphore");
			}

			Interlocked.Increment(ref readLocks);
		}
		finally
		{
			mutex.ReleaseMutex();
		}

		if (logger is not null)
		{
			LogAcquiredReadLock(logger);
		}

		return new SynchronizationDisposable(() =>
		{
			semaphore.Release();
			synchronizationLock.Release();
			Interlocked.Decrement(ref readLocks);

			if (logger is not null)
			{
				LogReleasedReadLock(logger);
			}
		});
	}

	/// <summary>
	/// Acquires exclusive write locking by consuming all read locks
	/// </summary>
	/// <returns>A disposable that releases the write lock</returns>
	public IDisposable AcquireWriteLock()
	{
#if NET7_0_OR_GREATER
		ObjectDisposedException.ThrowIf(disposed, this);
#else
		if (disposed)
			throw new ObjectDisposedException(nameof(TinyReadWriteLock));
#endif

		if (!synchronizationLock.Wait(waitTimeout))
			throw new TimeoutException("Could not acquire write lock, timed out waiting for SemaphoreSlim");

		if (!mutex.WaitOne(waitTimeout))
		{
			synchronizationLock.Release();
			throw new TimeoutException("Could not acquire write lock, timed out waiting for Mutex");
		}

		var readersAcquired = 0;
		try
		{
			for (var i = 0; i < maxReaderCount; i++)
			{
				if (!semaphore.WaitOne(waitTimeout))
				{
					if (readersAcquired > 0)
					{
						semaphore.Release(readersAcquired);
					}

					synchronizationLock.Release();
					throw new TimeoutException("Could not acquire write lock, timed out waiting for Semaphore");
				}

				readersAcquired++;
			}

			writeLock = true;
		}
		finally
		{
			mutex.ReleaseMutex();
		}

		if (logger is not null)
		{
			LogAcquiredWriteLock(logger);
		}

		return new SynchronizationDisposable(() =>
		{
			semaphore.Release(maxReaderCount);
			synchronizationLock.Release();
			writeLock = false;

			if (logger is not null)
			{
				LogReleasedWriteLock(logger);
			}
		});
	}

	/// <summary>
	/// Create a system wide Mutex that can be used to construct a TinyReadWriteLock
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended</param>
	/// <returns>A system wide Mutex</returns>
	public static Mutex CreateMutex(string name)
	{
		return new Mutex(false, "TinyReadWriteLock_Mutex_" + name);
	}

	/// <summary>
	/// Create a system wide Semaphore that can be used to construct a TinyReadWriteLock
	/// </summary>
	/// <param name="name">A system wide unique name, the name will have a prefix appended</param>
	/// <param name="maxReaderCount">Maximum number of simultaneous readers</param>
	/// <returns>A system wide Semaphore</returns>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static Semaphore CreateSemaphore(string name, int maxReaderCount)
	{
		return new Semaphore(maxReaderCount, maxReaderCount, "TinyReadWriteLock_Semaphore_" + name);
	}

	[LoggerMessage(0, LogLevel.Trace, "Acquired read lock")]
	private static partial void LogAcquiredReadLock(ILogger logger);

	[LoggerMessage(1, LogLevel.Trace, "Released read lock")]
	private static partial void LogReleasedReadLock(ILogger logger);

	[LoggerMessage(2, LogLevel.Trace, "Acquired write lock")]
	private static partial void LogAcquiredWriteLock(ILogger logger);

	[LoggerMessage(3, LogLevel.Trace, "Released write lock")]
	private static partial void LogReleasedWriteLock(ILogger logger);

	private sealed class SynchronizationDisposable(Action action) : IDisposable
	{
		private bool disposed;

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;

			action();
		}
	}
}
