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
		private readonly bool disposeLock;
		private readonly EventWaitHandle waitHandle;

		private bool disposed;

		public event EventHandler FileUpdated;

		public long MaxFileSize => maxFileSize;

		public const int DefaultMaxFileSize = 1024 * 1024;

		public TinyMemoryMappedFile(string name)
			: this(name, DefaultMaxFileSize)
		{
		}

		public TinyMemoryMappedFile(string name, long maxFileSize)
			: this(name, maxFileSize, new TinyReadWriteLock(name), true)
		{
		}

		public TinyMemoryMappedFile(string name, long maxFileSize, ITinyReadWriteLock readWriteLock, bool disposeLock)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("File must be named", nameof(name));

			if (maxFileSize <= 0)
				throw new ArgumentException("Max file size can not be less than 1 byte", nameof(maxFileSize));

			this.maxFileSize = maxFileSize;
			this.readWriteLock = readWriteLock;
			this.disposeLock = disposeLock;

			memoryMappedFile = CreateOrOpenMemoryMappedFile(name, maxFileSize);
			waitHandle = CreateEventWaitHandle(name);
			fileWatcherTask = Task.Factory.StartNew(FileWatcher);
		}

		public TinyMemoryMappedFile(MemoryMappedFile memoryMappedFile, EventWaitHandle waitHandle, long maxFileSize, ITinyReadWriteLock readWriteLock, bool disposeLock)
		{
			if (maxFileSize <= 0)
				throw new ArgumentException("Max file size can not be less than 1 byte", nameof(maxFileSize));

			this.maxFileSize = maxFileSize;
			this.readWriteLock = readWriteLock;
			this.disposeLock = disposeLock;

			this.memoryMappedFile = memoryMappedFile;
			this.waitHandle = waitHandle;
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

				if (disposeLock && readWriteLock is TinyReadWriteLock)
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
				throw new ArgumentOutOfRangeException(nameof(data), "Length greater than max file size");

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
				var pos = sizeof(int);
				for (int i = 0; i < length; i++)
				{
					data[i] = accessor.ReadByte(pos + i);
				}
				return data;
			}
		}

		private void InternalWrite(byte[] data)
		{
			if (data.Length > maxFileSize)
				throw new ArgumentOutOfRangeException(nameof(data), "Length greater than max file size");

			using (var accessor = memoryMappedFile.CreateViewAccessor())
			{
				accessor.Write(0, data.Length);
				var pos = sizeof(int);
				for (int i = 0; i < data.Length; i++)
				{
					accessor.Write(pos + i, data[i]);
				}
			}

			waitHandle.Set();
		}

		public static MemoryMappedFile CreateOrOpenMemoryMappedFile(string name, long maxFileSize)
		{
			return MemoryMappedFile.CreateOrOpen("TinyMemoryMappedFile_MemoryMappedFile_" + name, maxFileSize + sizeof(int));
		}

		public static EventWaitHandle CreateEventWaitHandle(string name)
		{
			return new EventWaitHandle(false, EventResetMode.ManualReset, "TinyMemoryMappedFile_WaitHandle_" + name);
		}
	}
}
