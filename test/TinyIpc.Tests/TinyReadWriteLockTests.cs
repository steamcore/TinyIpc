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

			using (var readWriteLock1 = new TinyReadWriteLock(lockId, 2))
			using (var readWriteLock2 = new TinyReadWriteLock(lockId, 2))
			{
				readWriteLock1.AcquireReadLock();

				Task.Factory.StartNew(() => readWriteLock2.AcquireWriteLock());
				Thread.Sleep(10);

				Assert.True(readWriteLock1.IsReaderLockHeld);
				Assert.False(readWriteLock2.IsWriterLockHeld);

				readWriteLock1.ReleaseReadLock();
				Thread.Sleep(10);

				Assert.False(readWriteLock1.IsReaderLockHeld);
				Assert.True(readWriteLock2.IsWriterLockHeld);
			}
		}

		[Fact]
		public void Calling_AcquireWriteLock_then_AquireReadLock_should_wait_for_other_lock()
		{
			var lockId = Guid.NewGuid().ToString();

			using (var readWriteLock1 = new TinyReadWriteLock(lockId, 2))
			using (var readWriteLock2 = new TinyReadWriteLock(lockId, 2))
			{
				readWriteLock1.AcquireWriteLock();

				Task.Factory.StartNew(() => readWriteLock2.AcquireReadLock());
				Thread.Sleep(50);

				Assert.True(readWriteLock1.IsWriterLockHeld);
				Assert.False(readWriteLock2.IsReaderLockHeld);

				readWriteLock1.ReleaseWriteLock();
				Thread.Sleep(50);

				Assert.False(readWriteLock1.IsWriterLockHeld);
				Assert.True(readWriteLock2.IsReaderLockHeld);
			}
		}

		[Fact]
		public void Calling_ReleaseLock_should_release_locks()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				readWriteLock.AcquireReadLock();
				Assert.True(readWriteLock.IsReaderLockHeld);

				readWriteLock.ReleaseReadLock();
				Assert.False(readWriteLock.IsReaderLockHeld);

				readWriteLock.AcquireWriteLock();
				Assert.True(readWriteLock.IsWriterLockHeld);

				readWriteLock.ReleaseWriteLock();
				Assert.False(readWriteLock.IsWriterLockHeld);
			}
		}

		[Fact]
		public void Calling_ReleaseLock_without_any_lock_held_should_throw()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				Assert.Throws<SemaphoreFullException>(() => readWriteLock.ReleaseReadLock());
			}
		}

		[Fact]
		public void WriteLock_should_be_exclusive()
		{
			var lockId = Guid.NewGuid().ToString();

			using (var readWriteLock1 = new TinyReadWriteLock(lockId, 2))
			using (var readWriteLock2 = new TinyReadWriteLock(lockId, 2))
			{
				readWriteLock1.AcquireWriteLock();

				Task.Factory.StartNew(() => readWriteLock2.AcquireWriteLock());

				Thread.Sleep(10);

				Assert.True(readWriteLock1.IsWriterLockHeld);
				Assert.False(readWriteLock2.IsWriterLockHeld);

				readWriteLock1.ReleaseWriteLock();
				Thread.Sleep(10);

				Assert.False(readWriteLock1.IsWriterLockHeld);
				Assert.True(readWriteLock2.IsWriterLockHeld);
			}
		}

		[Theory]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(7)]
		public void ReadLock_should_allow_n_readers(int n)
		{
			var lockId = Guid.NewGuid().ToString();
			var locks = Enumerable.Range(0, n+1).Select(x => new TinyReadWriteLock(lockId, n)).ToList();

			locks.Take(n).ToList().ForEach(l => l.AcquireReadLock());

			Task.Factory.StartNew(() => locks.Last().AcquireReadLock());
			Thread.Sleep(10);

			locks.Take(n).ToList().ForEach(l => Assert.True(l.IsReaderLockHeld));
			Assert.False(locks.Last().IsReaderLockHeld);

			locks.First().ReleaseReadLock();
			Thread.Sleep(10);

			Assert.False(locks.First().IsReaderLockHeld);
			locks.Skip(1).ToList().ForEach(l => Assert.True(l.IsReaderLockHeld));

			locks.ForEach(l => l.Dispose());
		}
	}
}
