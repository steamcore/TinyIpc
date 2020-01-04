using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TinyIpc.Synchronization;
using Xunit;

// ReSharper disable InconsistentNaming

namespace TinyIpc.Tests
{
	public class TinyReadWriteLockTests
	{
		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData(" ")]
		public void Calling_constructor_with_no_name_should_throw(string name)
		{
			Assert.Throws<ArgumentException>(() => new TinyReadWriteLock(name, 1));
		}

		[Fact]
		public void Calling_constructor_with_zero_readers_should_throw()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new TinyReadWriteLock(Guid.NewGuid().ToString(), 0));
		}

		[Fact]
		public void Calling_AcquireReadLock_then_AquireWriteLock_should_wait_for_other_lock()
		{
			var lockId = Guid.NewGuid().ToString();

			using var readWriteLock1 = new TinyReadWriteLock(lockId, 2);
			using var readWriteLock2 = new TinyReadWriteLock(lockId, 2);

			readWriteLock1.AcquireReadLock();

			var writeLockTask = Task.Run(() => readWriteLock2.AcquireWriteLock());

			WaitForTaskToStart(writeLockTask);

			Assert.True(readWriteLock1.IsReaderLockHeld);
			Assert.False(readWriteLock2.IsWriterLockHeld);

			readWriteLock1.ReleaseReadLock();

			writeLockTask.Wait();

			Assert.False(readWriteLock1.IsReaderLockHeld);
			Assert.True(readWriteLock2.IsWriterLockHeld);
		}

		[Fact]
		public void Calling_AcquireWriteLock_then_AquireReadLock_should_wait_for_other_lock()
		{
			var lockId = Guid.NewGuid().ToString();

			using var readWriteLock1 = new TinyReadWriteLock(lockId, 2);
			using var readWriteLock2 = new TinyReadWriteLock(lockId, 2);

			readWriteLock1.AcquireWriteLock();

			var readLockTask = Task.Run(() => readWriteLock2.AcquireReadLock());

			WaitForTaskToStart(readLockTask);

			Assert.True(readWriteLock1.IsWriterLockHeld);
			Assert.False(readWriteLock2.IsReaderLockHeld);

			readWriteLock1.ReleaseWriteLock();

			readLockTask.Wait();

			Assert.False(readWriteLock1.IsWriterLockHeld);
			Assert.True(readWriteLock2.IsReaderLockHeld);
		}

		[Fact]
		public void Calling_ReleaseReadLock_should_release_lock()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1);

			readWriteLock.AcquireReadLock();
			Assert.True(readWriteLock.IsReaderLockHeld);

			readWriteLock.ReleaseReadLock();
			Assert.False(readWriteLock.IsReaderLockHeld);
		}

		[Fact]
		public void Calling_ReleaseWriteLock_should_release_locks()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 2);

			readWriteLock.AcquireWriteLock();
			Assert.True(readWriteLock.IsWriterLockHeld);

			readWriteLock.ReleaseWriteLock();
			Assert.False(readWriteLock.IsWriterLockHeld);
		}

		[Fact]
		public void Calling_ReleaseReadLock_without_any_lock_held_should_throw()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1);

			Assert.Throws<SemaphoreFullException>(() => readWriteLock.ReleaseReadLock());
		}

		[Fact]
		public void Calling_ReleaseWriteLock_without_any_lock_held_should_throw()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1);

			Assert.Throws<SemaphoreFullException>(() => readWriteLock.ReleaseWriteLock());
		}

		[Fact]
		public void WriteLock_should_be_exclusive()
		{
			var lockId = Guid.NewGuid().ToString();

			using var readWriteLock1 = new TinyReadWriteLock(lockId, 2, TimeSpan.FromMilliseconds(0));
			using var readWriteLock2 = new TinyReadWriteLock(lockId, 2, TimeSpan.FromMilliseconds(0));

			// Aquire the first lock
			readWriteLock1.AcquireWriteLock();

			// The second lock should now throw TimeoutException
			Assert.Throws<TimeoutException>(() => readWriteLock2.AcquireWriteLock());

			// Make sure the expected locks are held
			Assert.True(readWriteLock1.IsWriterLockHeld);
			Assert.False(readWriteLock2.IsWriterLockHeld);

			// By releasing the first lock, the second lock should now be able to be held
			readWriteLock1.ReleaseWriteLock();
			readWriteLock2.AcquireWriteLock();

			// Make sure the expected locks are held
			Assert.False(readWriteLock1.IsWriterLockHeld);
			Assert.True(readWriteLock2.IsWriterLockHeld);
		}

		[Theory]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(7)]
		public void ReadLock_should_allow_n_readers(int n)
		{
			var lockId = Guid.NewGuid().ToString();

			// Create more than n locks
			var locks = Enumerable.Range(0, n+1).Select(x => new TinyReadWriteLock(lockId, n, TimeSpan.FromMilliseconds(0))).ToList();

			try
			{
				// Aquire n locks
				foreach (var rwLock in locks.Take(n))
				{
					rwLock.AcquireReadLock();
				}

				// The first n locks should now be held
				foreach (var rwLock in locks.Take(n))
				{
					Assert.True(rwLock.IsReaderLockHeld, "Expected lock to be held");
				}

				// Trying to aquire one more than n should throw TimeoutException
				Assert.Throws<TimeoutException>(() => locks[n].AcquireReadLock());

				// Release any lock of the first locks
				locks[0].ReleaseReadLock();

				// The last lock should now be able to aquire the lock
				locks[n].AcquireReadLock();
				Assert.True(locks[n].IsReaderLockHeld, "Expected last lock to be held");
			}
			finally
			{
				foreach (var rwLock in locks)
				{
					rwLock.Dispose();
				}
			}
		}

		private static void WaitForTaskToStart(Task task)
		{
			while (task.Status != TaskStatus.Running)
			{
				Thread.Sleep(25);
			}
		}
	}
}
