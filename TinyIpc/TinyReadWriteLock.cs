using System;
using System.Threading;

namespace TinyIpc
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

		public bool IsReaderLockHeld { get { return readLocks > 0; } }
		public bool IsWriterLockHeld { get { return writeLock; } }

		public TinyReadWriteLock(string name, int maxReaderCount)
			: this(name, maxReaderCount, TimeSpan.FromSeconds(5))
		{
		}

		public TinyReadWriteLock(string name, int maxReaderCount, TimeSpan waitTimeout)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Lock must be named", "name");

			if (maxReaderCount == 0)
				throw new ArgumentOutOfRangeException("maxReaderCount", "Need at least one reader");

			this.maxReaderCount = maxReaderCount;
			this.waitTimeout = waitTimeout;
			mutex = new Mutex(false, "TinyReadWriteLock_Mutex_" + name);
			semaphore = new Semaphore(maxReaderCount, maxReaderCount, "TinyReadWriteLock_Semaphore_" + name);
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;

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
			mutex.Dispose();
			semaphore.Dispose();
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
			catch (TimeoutException)
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
	}
}
