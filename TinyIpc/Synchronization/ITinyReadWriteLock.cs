namespace TinyIpc.Synchronization
{
	public interface ITinyReadWriteLock
	{
		bool IsReaderLockHeld { get; }
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
