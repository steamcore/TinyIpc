using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
// ReSharper disable InconsistentNaming

namespace TinyIpc.Tests
{
	[TestFixture]
	public class TinyReadWriteLockTests
	{
		[TestCase(null)]
		[TestCase("")]
		[TestCase(" ")]
		public void Calling_constructor_with_no_name_should_throw(string name)
		{
			Assert.That(() => new TinyReadWriteLock(name, 1), Throws.ArgumentException);
		}
		[Test]
		public void Calling_constructor_with_zero_readers_should_throw()
		{
			Assert.That(() => new TinyReadWriteLock(Guid.NewGuid().ToString(), 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Calling_AcquireReadLock_after_AquireWriteLock_should_throw()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				readWriteLock.AcquireReadLock();

				Assert.That(() => readWriteLock.AcquireWriteLock(), Throws.InvalidOperationException);
			}
		}

		[Test]
		public void Calling_AcquireReadLock_twice_should_have_no_effect()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				readWriteLock.AcquireReadLock();
				readWriteLock.AcquireReadLock();
			}
		}

		[Test]
		public void Calling_AcquireWriteLock_after_AquireReadLock_should_throw()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				readWriteLock.AcquireWriteLock();

				Assert.That(() => readWriteLock.AcquireReadLock(), Throws.InvalidOperationException);
			}
		}

		[Test]
		public void Calling_ReleaseLock_should_release_locks()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				readWriteLock.AcquireReadLock();
				Assert.That(readWriteLock.IsReaderLockHeld, Is.True);

				readWriteLock.ReleaseLock();
				Assert.That(readWriteLock.IsReaderLockHeld, Is.False);

				readWriteLock.AcquireWriteLock();
				Assert.That(readWriteLock.IsWriterLockHeld, Is.True);

				readWriteLock.ReleaseLock();
				Assert.That(readWriteLock.IsWriterLockHeld, Is.False);
			}
		}

		[Test]
		public void Calling_ReleaseLock_without_any_lock_held_should_have_no_effect()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				readWriteLock.ReleaseLock();
			}
		}

		[Test]
		public void Calling_ReleaseLock_twice_should_have_no_effect()
		{
			using (var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1))
			{
				readWriteLock.ReleaseLock();
				readWriteLock.ReleaseLock();
			}
		}

		[Test]
		public void WriteLock_should_be_exclusive()
		{
			var lockId = Guid.NewGuid().ToString();

			using (var readWriteLock1 = new TinyReadWriteLock(lockId, 2))
			using (var readWriteLock2 = new TinyReadWriteLock(lockId, 2))
			{
				readWriteLock1.AcquireWriteLock();

				Task.Factory.StartNew(() => readWriteLock2.AcquireWriteLock());

				Thread.Sleep(10);

				Assert.That(readWriteLock1.IsWriterLockHeld, Is.True);
				Assert.That(readWriteLock2.IsWriterLockHeld, Is.False);

				readWriteLock1.ReleaseLock();
				Thread.Sleep(10);

				Assert.That(readWriteLock1.IsWriterLockHeld, Is.False);
				Assert.That(readWriteLock2.IsWriterLockHeld, Is.True);
			}
		}

		[TestCase(2)]
		[TestCase(3)]
		[TestCase(7)]
		public void ReadLock_should_allow_n_readers(int n)
		{
			var lockId = Guid.NewGuid().ToString();
			var locks = Enumerable.Range(0, n+1).Select(x => new TinyReadWriteLock(lockId, n)).ToList();

			locks.Take(n).ToList().ForEach(l => l.AcquireReadLock());

			Task.Factory.StartNew(() => locks.Last().AcquireReadLock());
			Thread.Sleep(10);

			locks.Take(n).ToList().ForEach(l => Assert.That(l.IsReaderLockHeld, Is.True));
			Assert.That(locks.Last().IsReaderLockHeld, Is.False);

			locks.First().ReleaseLock();
			Thread.Sleep(10);

			Assert.That(locks.First().IsReaderLockHeld, Is.False);
			locks.Skip(1).ToList().ForEach(l => Assert.That(l.IsReaderLockHeld, Is.True));

			locks.ForEach(l => l.ReleaseLock());
		}
	}
}
