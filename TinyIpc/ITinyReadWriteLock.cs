using System;

namespace TinyIpc
{
	public interface ITinyReadWriteLock
	{
		bool IsReaderLockHeld { get; }
		bool IsWriterLockHeld { get; }

		/// <summary>
		/// Acquire one read lock
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if write lock is already held</exception>
		void AcquireReadLock();

		/// <summary>
		/// Acquires exclusive write locking by consuming all read locks
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown if read lock is already held</exception>
		void AcquireWriteLock();

		/// <summary>
		/// Will release any lock held
		/// </summary>
		void ReleaseLock();
	}
}
