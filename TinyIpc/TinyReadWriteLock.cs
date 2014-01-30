using System;
using System.Threading;

namespace TinyIpc
{
	/// <summary>
	/// Implements a simple inter process read/write locking mechanism
	/// Inspired by http://www.joecheng.com/blog/entries/Writinganinter-processRea.html
	/// </summary>
	public class TinyReadWriteLock : IDisposable
	{
		private readonly Mutex mutex;
		private readonly Semaphore semaphore;
		private readonly int maxReaderCount;
		private bool readLock;
		private bool writeLock;

		public bool IsReaderLockHeld { get { return readLock; } }
		public bool IsWriterLockHeld { get { return writeLock; } }

		public TinyReadWriteLock(string name, int maxReaderCount)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Lock must be named", "name");

			if (maxReaderCount == 0)
				throw new ArgumentOutOfRangeException("maxReaderCount", "Need at least one reader");

			this.maxReaderCount = maxReaderCount;
			mutex = new Mutex(false, "TinyReadWriteLock_Mutex_" + name);
			semaphore = new Semaphore(maxReaderCount, maxReaderCount, "TinyReadWriteLock_Semaphore_" + name);
		}

		public void Dispose()
		{
			ReleaseLock();
			mutex.Dispose();
			semaphore.Dispose();
		}

		public void AcquireReadLock()
		{
			if (readLock)
				return;

			if (writeLock)
				throw new InvalidOperationException("Can not acquire read lock because write lock is alread held");

			mutex.WaitOne();
			semaphore.WaitOne();
			mutex.ReleaseMutex();
			readLock = true;
		}

		public void AcquireWriteLock()
		{
			if (readLock)
				throw new InvalidOperationException("Can not acquire write lock because read lock is already held");

			mutex.WaitOne();
			for (var i = 0; i < maxReaderCount; i++)
			{
				semaphore.WaitOne();
			}
			mutex.ReleaseMutex();
			writeLock = true;
		}

		public void ReleaseLock()
		{
			if (readLock)
			{
				semaphore.Release();
				readLock = false;
			}
			if (writeLock)
			{
				for (var i = 0; i < maxReaderCount; i++)
				{
					semaphore.Release();
				}
				writeLock = false;
			}
		}
	}
}
