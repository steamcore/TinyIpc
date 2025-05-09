using Shouldly;

namespace TinyIpc.Synchronization;

public class TinyReadWriteLockTests
{
	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments(" ")]
	public void Calling_constructor_with_no_name_should_throw(string name)
	{
		Should.Throw<ArgumentException>(() => new TinyReadWriteLock(name, 1));
	}

	[Test]
	public void Calling_constructor_with_zero_readers_should_throw()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new TinyReadWriteLock(Guid.NewGuid().ToString(), 0));
	}

	[Test]
	public async Task Calling_AcquireReadLock_then_AquireWriteLock_should_wait_for_other_lock(CancellationToken cancellationToken)
	{
		var lockId = Guid.NewGuid().ToString();

		using var readWriteLock1 = new TinyReadWriteLock(lockId, 2);
		using var readWriteLock2 = new TinyReadWriteLock(lockId, 2);

		var readLock1 = readWriteLock1.AcquireReadLock(cancellationToken);
		IDisposable writeLock2 = null;

		var writeLockTask = Task.Run(() => writeLock2 = readWriteLock2.AcquireWriteLock(cancellationToken));

		WaitForTaskToStart(writeLockTask);

		readWriteLock1.IsReaderLockHeld.ShouldBeTrue();
		readWriteLock2.IsWriterLockHeld.ShouldBeFalse();

		readLock1.Dispose();

		await writeLockTask;

		readWriteLock1.IsReaderLockHeld.ShouldBeFalse();
		readWriteLock2.IsWriterLockHeld.ShouldBeTrue();

		writeLock2?.Dispose();
	}

	[Test]
	public async Task Calling_AcquireWriteLock_then_AquireReadLock_should_wait_for_other_lock(CancellationToken cancellationToken)
	{
		var lockId = Guid.NewGuid().ToString();

		using var readWriteLock1 = new TinyReadWriteLock(lockId, 2);
		using var readWriteLock2 = new TinyReadWriteLock(lockId, 2);

		var writeLock1 = readWriteLock1.AcquireWriteLock(cancellationToken);
		IDisposable readLock2 = null;

		var readLockTask = Task.Run(() => readLock2 = readWriteLock2.AcquireReadLock(cancellationToken));

		WaitForTaskToStart(readLockTask);

		readWriteLock1.IsWriterLockHeld.ShouldBeTrue();
		readWriteLock2.IsReaderLockHeld.ShouldBeFalse();

		writeLock1.Dispose();

		await readLockTask;

		readWriteLock1.IsWriterLockHeld.ShouldBeFalse();
		readWriteLock2.IsReaderLockHeld.ShouldBeTrue();

		readLock2.Dispose();
	}

	[Test]
	public void Calling_Dispose_on_read_lock_should_release_lock(CancellationToken cancellationToken)
	{
		using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 1);

		var readLock = readWriteLock.AcquireReadLock(cancellationToken);
		readWriteLock.IsReaderLockHeld.ShouldBeTrue();

		readLock.Dispose();
		readWriteLock.IsReaderLockHeld.ShouldBeFalse();
	}

	[Test]
	public void Calling_Dispose_on_write_lock_should_release_locks(CancellationToken cancellationToken)
	{
		using var readWriteLock = new TinyReadWriteLock(Guid.NewGuid().ToString(), 2);

		var writeLock = readWriteLock.AcquireWriteLock(cancellationToken);
		readWriteLock.IsWriterLockHeld.ShouldBeTrue();

		writeLock.Dispose();
		readWriteLock.IsWriterLockHeld.ShouldBeFalse();
	}

	[Test]
	public void WriteLock_should_be_exclusive(CancellationToken cancellationToken)
	{
		var lockId = Guid.NewGuid().ToString();

		using var readWriteLock1 = new TinyReadWriteLock(lockId, 2, TimeSpan.FromMilliseconds(0));
		using var readWriteLock2 = new TinyReadWriteLock(lockId, 2, TimeSpan.FromMilliseconds(0));

		// Aquire the first lock
		var writeLock1 = readWriteLock1.AcquireWriteLock(cancellationToken);

		// The second lock should now throw TimeoutException
		Should.Throw<TimeoutException>(() => readWriteLock2.AcquireWriteLock(cancellationToken));

		// Make sure the expected locks are held
		readWriteLock1.IsWriterLockHeld.ShouldBeTrue();
		readWriteLock2.IsWriterLockHeld.ShouldBeFalse();

		// By releasing the first lock, the second lock should now be able to be held
		writeLock1.Dispose();
		var writeLock2 = readWriteLock2.AcquireWriteLock(cancellationToken);

		// Make sure the expected locks are held
		readWriteLock1.IsWriterLockHeld.ShouldBeFalse();
		readWriteLock2.IsWriterLockHeld.ShouldBeTrue();

		writeLock2.Dispose();
	}

	[Test]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(7)]
	public void ReadLock_should_allow_n_readers(int n, CancellationToken cancellationToken)
	{
		var lockId = Guid.NewGuid().ToString();

		// Create more than n locks
		var locks = Enumerable.Range(0, n + 1).Select(x => new TinyReadWriteLock(lockId, n, TimeSpan.FromMilliseconds(0))).ToList();
		var heldLocks = new List<IDisposable>();

		try
		{
			// Aquire n locks
			foreach (var rwLock in locks.Take(n))
			{
				heldLocks.Add(rwLock.AcquireReadLock(cancellationToken));
			}

			// The first n locks should now be held
			foreach (var rwLock in locks.Take(n))
			{
				rwLock.IsReaderLockHeld.ShouldBeTrue("Expected lock to be held");
			}

			// Trying to aquire one more than n should throw TimeoutException
			Should.Throw<TimeoutException>(() => locks[n].AcquireReadLock(cancellationToken));

			// Release any lock of the first locks
			heldLocks[0].Dispose();

			// The last lock should now be able to aquire the lock
			heldLocks.Add(locks[n].AcquireReadLock(cancellationToken));
			locks[n].IsReaderLockHeld.ShouldBeTrue("Expected last lock to be held");
		}
		finally
		{
			foreach (var heldLock in heldLocks)
			{
				heldLock.Dispose();
			}

			foreach (var rwLock in locks)
			{
				rwLock.Dispose();
			}
		}
	}

	[Test]
	public void ReadLock_should_respect_CancellationToken(CancellationToken cancellationToken)
	{
		var lockId = Guid.NewGuid().ToString();

		using var readWriteLock1 = new TinyReadWriteLock(lockId, 1, TimeSpan.FromMilliseconds(5_000));
		using var readWriteLock2 = new TinyReadWriteLock(lockId, 1, TimeSpan.FromMilliseconds(5_000));
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

		// Aquire the first lock
		using var writeLock1 = readWriteLock1.AcquireReadLock(cancellationToken);

		// The second lock should throw OperationCanceledException when CancellationToken triggers
		Should.Throw<OperationCanceledException>(() => readWriteLock2.AcquireReadLock(cts.Token));
	}

	[Test]
	public void WriteLock_should_respect_CancellationToken(CancellationToken cancellationToken)
	{
		var lockId = Guid.NewGuid().ToString();

		using var readWriteLock1 = new TinyReadWriteLock(lockId, 2, TimeSpan.FromMilliseconds(5_000));
		using var readWriteLock2 = new TinyReadWriteLock(lockId, 2, TimeSpan.FromMilliseconds(5_000));
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

		// Aquire the first lock
		using var writeLock1 = readWriteLock1.AcquireWriteLock(cancellationToken);

		// The second lock should throw OperationCanceledException when CancellationToken triggers
		Should.Throw<OperationCanceledException>(() => readWriteLock2.AcquireWriteLock(cts.Token));
	}

	private static void WaitForTaskToStart(Task task)
	{
		while (task.Status != TaskStatus.Running)
		{
			Thread.Sleep(25);
		}
	}
}
