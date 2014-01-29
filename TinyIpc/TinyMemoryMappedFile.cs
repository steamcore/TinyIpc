using System;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace TinyIpc
{
	/// <summary>
	/// Wraps a MemoryMappedFile with inter process synchronization and signaling
	/// </summary>
	public class TinyMemoryMappedFile : IDisposable
	{
		private bool disposed;

		private readonly MemoryMappedFile memoryMappedFile;
		private readonly int operationTimeoutMs;
		private readonly TinyReadWriteLock readWriteLock;
		private readonly Task fileWatcherTask;
		private readonly EventWaitHandle waitHandle;

		public event EventHandler FileUpdated;

		public TinyMemoryMappedFile(string name)
			: this(name, 1024 * 1024)
		{
		}

		public TinyMemoryMappedFile(string name, long maxFileSize)
			: this(name, maxFileSize, TimeSpan.FromMilliseconds(500))
		{
		}

		public TinyMemoryMappedFile(string name, long maxFileSize, TimeSpan operationTimeout)
		{
			memoryMappedFile = MemoryMappedFile.CreateOrOpen("TinyMemoryMappedFile_MemoryMappedFile_" + name, maxFileSize);
			operationTimeoutMs = (int)operationTimeout.TotalMilliseconds;
			readWriteLock = new TinyReadWriteLock(name, 3);
			waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset, "TinyMemoryMappedFile_WaitHandle_" + name);
			fileWatcherTask = Task.Factory.StartNew(FileWatcher);
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			fileWatcherTask.Wait();

			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				memoryMappedFile.Dispose();
				readWriteLock.Dispose();
				waitHandle.Dispose();
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
				readWriteLock.ReleaseLock();
			}
		}

		/// <summary>
		/// Replaces the content of the memory mapped file with a write lock in place.
		/// </summary>
		public void Write(byte[] data)
		{
			readWriteLock.AcquireWriteLock();

			try
			{
				InternalWrite(data);
			}
			finally
			{
				readWriteLock.ReleaseLock();
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
				readWriteLock.ReleaseLock();
			}
		}

		private void FileWatcher()
		{
			while (!disposed)
			{
				if (!waitHandle.WaitOne(operationTimeoutMs))
					continue;

				if (disposed)
					return;

				if (FileUpdated != null)
					Task.Factory.StartNew(() => FileUpdated(this, null));

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
			using (var accessor = memoryMappedFile.CreateViewAccessor())
			{
				accessor.Write(0, data.Length);
				accessor.WriteArray(sizeof (int), data, 0, data.Length);
			}

			waitHandle.Set();
		}
	}
}
