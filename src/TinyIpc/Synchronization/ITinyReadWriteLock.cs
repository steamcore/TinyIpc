namespace TinyIpc.Synchronization
{
	public interface ITinyReadWriteLock
	{
		/// <summary>
		/// Is true if at least one read lock is being held
		/// </summary>
		bool IsReaderLockHeld { get; }

		/// <summary>
		/// Is true if a write lock (which means all read locks) is being held
		/// </summary>
		bool IsWriterLockHeld { get; }

		/// <summary>
		/// Acquire one read lock
		/// </summary>
		void AcquireReadLock();

		/// <summary>
		/// Acquires exclusive write locking by consuming all read locks
		/// </summary>
		void AcquireWriteLock();

		/// <summary>
		/// Release one read lock
		/// </summary>
		void ReleaseReadLock();

		/// <summary>
		/// Release write lock
		/// </summary>
		void ReleaseWriteLock();
	}
}
