using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace TinyIpc.Synchronization
{
	public class TinyReadWriteLockTests
	{
		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData(" ")]
		public void Calling_constructor_with_no_name_should_throw(string name)
		{
			Should.Throw<ArgumentException>(() => new TinyReadWriteLock(name, 1));
		}

		[Fact]
		public void Calling_constructor_with_zero_readers_should_throw()
		{
			Should.Throw<ArgumentOutOfRangeException>(() => new TinyReadWriteLock(Guid.NewGuid().ToString(), 0));
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

			readWriteLock1.IsReaderLockHeld.ShouldBeTrue();
			readWriteLock2.IsWriterLockHeld.ShouldBeFalse();

			readWriteLock1.ReleaseReadLock();

			writeLockTask.Wait();

			readWriteLock1.IsReaderLockHeld.ShouldBeFalse();
			readWriteLock2.IsWriterLockHeld.ShouldBeTrue();
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

			readWriteLock1.IsWriterLockHeld.ShouldBeTrue();
			readWriteLock2.IsReaderLockHeld.ShouldBeFalse();

			readWriteLock1.ReleaseWriteLock();

			readLockTask.Wait();

			readWriteLock1.IsWriterLockHeld.ShouldBeFalse();
			readWriteLock2.IsReaderLockHeld.ShouldBeTrue();
		}

		[Fact]
		public void Calling_ReleaseReadLock_should_release_lock()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1);

			readWriteLock.AcquireReadLock();
			readWriteLock.IsReaderLockHeld.ShouldBeTrue();

			readWriteLock.ReleaseReadLock();
			readWriteLock.IsReaderLockHeld.ShouldBeFalse();
		}

		[Fact]
		public void Calling_ReleaseWriteLock_should_release_locks()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 2);

			readWriteLock.AcquireWriteLock();
			readWriteLock.IsWriterLockHeld.ShouldBeTrue();

			readWriteLock.ReleaseWriteLock();
			readWriteLock.IsWriterLockHeld.ShouldBeFalse();
		}

		[Fact]
		public void Calling_ReleaseReadLock_without_any_lock_held_should_throw()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1);

			Should.Throw<SemaphoreFullException>(() => readWriteLock.ReleaseReadLock());
		}

		[Fact]
		public void Calling_ReleaseWriteLock_without_any_lock_held_should_throw()
		{
			using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1);

			Should.Throw<SemaphoreFullException>(() => readWriteLock.ReleaseWriteLock());
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
			Should.Throw<TimeoutException>(() => readWriteLock2.AcquireWriteLock());

			// Make sure the expected locks are held
			readWriteLock1.IsWriterLockHeld.ShouldBeTrue();
			readWriteLock2.IsWriterLockHeld.ShouldBeFalse();

			// By releasing the first lock, the second lock should now be able to be held
			readWriteLock1.ReleaseWriteLock();
			readWriteLock2.AcquireWriteLock();

			// Make sure the expected locks are held
			readWriteLock1.IsWriterLockHeld.ShouldBeFalse();
			readWriteLock2.IsWriterLockHeld.ShouldBeTrue();
		}

		[Theory]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(7)]
		public void ReadLock_should_allow_n_readers(int n)
		{
			var lockId = Guid.NewGuid().ToString();

			// Create more than n locks
			var locks = Enumerable.Range(0, n + 1).Select(x => new TinyReadWriteLock(lockId, n, TimeSpan.FromMilliseconds(0))).ToList();

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
					rwLock.IsReaderLockHeld.ShouldBeTrue("Expected lock to be held");
				}

				// Trying to aquire one more than n should throw TimeoutException
				Should.Throw<TimeoutException>(() => locks[n].AcquireReadLock());

				// Release any lock of the first locks
				locks[0].ReleaseReadLock();

				// The last lock should now be able to aquire the lock
				locks[n].AcquireReadLock();
				locks[n].IsReaderLockHeld.ShouldBeTrue("Expected last lock to be held");
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
