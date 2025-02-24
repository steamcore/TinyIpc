namespace TinyIpc.Synchronization;

public interface ITinyReadWriteLock : IDisposable
{
	/// <summary>
	/// Is true if a read lock is being held
	/// </summary>
	bool IsReaderLockHeld { get; }

	/// <summary>
	/// Is true if a write lock (which means all read locks) is being held
	/// </summary>
	bool IsWriterLockHeld { get; }

	/// <summary>
	/// The name of the lock if it was created with a name
	/// </summary>
	string? Name { get; }

	/// <summary>
	/// Acquire a read lock, only one read lock can be held by once instance
	/// but multiple read locks may be held at the same time by multiple instances
	/// </summary>
	/// <returns>A disposable that releases the read lock</returns>
	IDisposable AcquireReadLock(CancellationToken cancellationToken = default);

	/// <summary>
	/// Acquires exclusive write locking by consuming all read locks
	/// </summary>
	/// <returns>A disposable that releases the write lock</returns>
	IDisposable AcquireWriteLock(CancellationToken cancellationToken = default);
}
