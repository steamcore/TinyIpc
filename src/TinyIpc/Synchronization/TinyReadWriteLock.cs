using System;
using System.Threading;

namespace TinyIpc.Synchronization
{
	/// <summary>
	/// Implements a simple inter process read/write locking mechanism
	/// Inspired by http://www.joecheng.com/blog/entries/Writinganinter-processRea.html
	/// </summary>
	public class TinyReadWriteLock : IDisposable, ITinyReadWriteLock
	{
		private readonly Mutex mutex;
		private readonly Semaphore semaphore;
		private readonly int maxReaderCount;
		private readonly TimeSpan waitTimeout;

		private bool disposed;
		private int readLocks;
		private bool writeLock;

		public bool IsReaderLockHeld => readLocks > 0;
		public bool IsWriterLockHeld => writeLock;

		public const int DefaultMaxReaderCount = 6;
		public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(5);

		/// <summary>
		/// Initializes a new instance of the TinyReadWriteLock class.
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
		public TinyReadWriteLock(string name)
			: this(name, DefaultMaxReaderCount, DefaultWaitTimeout)
		{
		}

		/// <summary>
		/// Initializes a new instance of the TinyReadWriteLock class.
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
		/// <param name="maxReaderCount">Maxium simultaneous readers, default is 6</param>
		public TinyReadWriteLock(string name, int maxReaderCount)
			: this(name, maxReaderCount, DefaultWaitTimeout)
		{
		}

		/// <summary>
		/// Initializes a new instance of the TinyReadWriteLock class.
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
		/// <param name="maxReaderCount">Maxium simultaneous readers, default is 6</param>
		/// <param name="waitTimeout">How long to wait before giving up aquiring read and write locks</param>
		public TinyReadWriteLock(string name, int maxReaderCount, TimeSpan waitTimeout)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Lock must be named", nameof(name));

			if (maxReaderCount <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxReaderCount), "Need at least one reader");

			this.maxReaderCount = maxReaderCount;
			this.waitTimeout = waitTimeout;

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
		public TinyReadWriteLock(Mutex mutex, Semaphore semaphore, int maxReaderCount, TimeSpan waitTimeout)
		{
			if (maxReaderCount <= 0)
				throw new ArgumentOutOfRangeException(nameof(maxReaderCount), "Need at least one reader");

			this.maxReaderCount = maxReaderCount;
			this.waitTimeout = waitTimeout;
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

			// Always release held semaphore locks even when triggered by the finalizer
			// or they will remain held indefinitely.
			if (readLocks > 0)
			{
				semaphore.Release(readLocks);
			}
			else if (writeLock)
			{
				semaphore.Release(maxReaderCount);
			}

			readLocks = 0;
			writeLock = false;

			if (disposing)
			{
				mutex.Dispose();
				semaphore.Dispose();
			}

			disposed = true;
		}

		/// <summary>
		/// Acquire one read lock
		/// </summary>
		public void AcquireReadLock()
		{
			if (!mutex.WaitOne(waitTimeout))
				throw new TimeoutException("Gave up waiting for read lock");

			try
			{
				if (!semaphore.WaitOne(waitTimeout))
					throw new TimeoutException("Gave up waiting for read lock");

				Interlocked.Increment(ref readLocks);
			}
			finally
			{
				mutex.ReleaseMutex();
			}
		}

		/// <summary>
		/// Acquires exclusive write locking by consuming all read locks
		/// </summary>
		public void AcquireWriteLock()
		{
			if (!mutex.WaitOne(waitTimeout))
				throw new TimeoutException("Gave up waiting for write lock");

			var readersAcquired = 0;
			try
			{
				for (var i = 0; i < maxReaderCount; i++)
				{
					if (!semaphore.WaitOne(waitTimeout))
						throw new TimeoutException("Gave up waiting for write lock");

					readersAcquired++;
				}
				writeLock = true;
			}
			catch (TimeoutException) when (readersAcquired > 0)
			{
				semaphore.Release(readersAcquired);
				throw;
			}
			finally
			{
				mutex.ReleaseMutex();
			}
		}

		/// <summary>
		/// Release one read lock
		/// </summary>
		public void ReleaseReadLock()
		{
			semaphore.Release();
			Interlocked.Decrement(ref readLocks);
		}

		/// <summary>
		/// Release write lock
		/// </summary>
		public void ReleaseWriteLock()
		{
			writeLock = false;
			semaphore.Release(maxReaderCount);
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
		public static Semaphore CreateSemaphore(string name, int maxReaderCount)
		{
			return new Semaphore(maxReaderCount, maxReaderCount, "TinyReadWriteLock_Semaphore_" + name);
		}
	}
}
