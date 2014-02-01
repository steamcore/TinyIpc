using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using TinyIpc.Synchronization;

namespace TinyIpc.IO
{
	/// <summary>
	/// Wraps a MemoryMappedFile with inter process synchronization and signaling
	/// </summary>
	public class TinyMemoryMappedFile : IDisposable, ITinyMemoryMappedFile
	{
		private readonly Task fileWatcherTask;
		private readonly long maxFileSize;
		private readonly MemoryMappedFile memoryMappedFile;
		private readonly ITinyReadWriteLock readWriteLock;
		private readonly bool shouldDisposeLock;
		private readonly EventWaitHandle waitHandle;

		private bool disposed;

		public event EventHandler FileUpdated;

		public long MaxFileSize { get { return maxFileSize; } }

		public TinyMemoryMappedFile(string name)
			: this(name, 1024 * 1024)
		{
		}

		public TinyMemoryMappedFile(string name, long maxFileSize)
			: this(name, maxFileSize, new TinyReadWriteLock(name, 3))
		{
			shouldDisposeLock = true;
		}

		public TinyMemoryMappedFile(string name, long maxFileSize, ITinyReadWriteLock readWriteLock)
		{
			this.maxFileSize = maxFileSize;
			this.readWriteLock = readWriteLock;

			memoryMappedFile = MemoryMappedFile.CreateOrOpen("TinyMemoryMappedFile_MemoryMappedFile_" + name, maxFileSize + sizeof(int));
			waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, "TinyMemoryMappedFile_WaitHandle_" + name);
			fileWatcherTask = Task.Factory.StartNew(FileWatcher);
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			waitHandle.Set();
			fileWatcherTask.Wait();

			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				memoryMappedFile.Dispose();

				if (shouldDisposeLock && readWriteLock is TinyReadWriteLock)
				{
					(readWriteLock as TinyReadWriteLock).Dispose();
				}

				waitHandle.Dispose();
			}
		}

		/// <summary>
		/// Gets the file size
		/// </summary>
		/// <returns>File size</returns>
		public int GetFileSize()
		{
			readWriteLock.AcquireReadLock();

			try
			{
				using (var accessor = memoryMappedFile.CreateViewAccessor())
				{
					return accessor.ReadInt32(0);
				}
			}
			finally
			{
				readWriteLock.ReleaseReadLock();
			}
		}

		/// <summary>
		/// Reads the content of the memory mapped file with a read lock in place.
		/// </summary>
		/// <returns>File content</returns>
		public byte[] Read()
		{
			readWriteLock.AcquireReadLock();

			try
			{
				return InternalRead();
			}
			finally
			{
				readWriteLock.ReleaseReadLock();
			}
		}

		/// <summary>
		/// Replaces the content of the memory mapped file with a write lock in place.
		/// </summary>
		public void Write(byte[] data)
		{
			if (data.Length > maxFileSize)
				throw new ArgumentOutOfRangeException("data", "Length greater than max file size");

			readWriteLock.AcquireWriteLock();

			try
			{
				InternalWrite(data);
			}
			finally
			{
				readWriteLock.ReleaseWriteLock();
			}
		}

		/// <summary>
		/// Reads and then replaces the content of the memory mapped file with a write lock in place.
		/// </summary>
		public void ReadWrite(Func<byte[], byte[]> updateFunc)
		{
			readWriteLock.AcquireWriteLock();

			try
			{
				InternalWrite(updateFunc(InternalRead()));
			}
			finally
			{
				readWriteLock.ReleaseWriteLock();
			}
		}

		private void FileWatcher()
		{
			while (!disposed)
			{
				waitHandle.WaitOne();

				if (disposed)
					return;

				if (FileUpdated != null)
					Task.Factory.StartNew(() => FileUpdated(this, EventArgs.Empty));

				waitHandle.Reset();
			}
		}

		private byte[] InternalRead()
		{
			using (var accessor = memoryMappedFile.CreateViewAccessor())
			{
				var length = accessor.ReadInt32(0);
				var data = new byte[length];
				accessor.ReadArray(sizeof(int), data, 0, length);
				return data;
			}
		}

		private void InternalWrite(byte[] data)
		{
			if (data.Length > maxFileSize)
				throw new ArgumentOutOfRangeException("data", "Length greater than max file size");

			using (var accessor = memoryMappedFile.CreateViewAccessor())
			{
				accessor.Write(0, data.Length);
				accessor.WriteArray(sizeof (int), data, 0, data.Length);
			}

			waitHandle.Set();
		}
	}
}
