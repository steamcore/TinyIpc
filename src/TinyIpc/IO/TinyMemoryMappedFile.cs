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
		private readonly MemoryMappedFile memoryMappedFile;
		private readonly ITinyReadWriteLock readWriteLock;
		private readonly bool disposeLock;
		private readonly EventWaitHandle fileWaitHandle;

		private readonly EventWaitHandle disposeWaitHandle;
		private bool disposed;

		public event EventHandler FileUpdated;

		public long MaxFileSize { get; private set; }

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
			: this(CreateOrOpenMemoryMappedFile(name, maxFileSize), CreateEventWaitHandle(name), maxFileSize, readWriteLock, disposeLock)
		{
		}

		public TinyMemoryMappedFile(MemoryMappedFile memoryMappedFile, EventWaitHandle fileWaitHandle, long maxFileSize, ITinyReadWriteLock readWriteLock, bool disposeLock)
		{
			this.readWriteLock = readWriteLock;
			this.disposeLock = disposeLock;
			this.memoryMappedFile = memoryMappedFile;
			this.fileWaitHandle = fileWaitHandle;

			MaxFileSize = maxFileSize;

			disposeWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

			fileWatcherTask = Task.Run(() => FileWatcher());
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			disposeWaitHandle.Set();
			fileWatcherTask.Wait(TinyReadWriteLock.DefaultWaitTimeout);

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

				fileWaitHandle.Dispose();
				disposeWaitHandle.Dispose();
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
			if (data.Length > MaxFileSize)
				throw new ArgumentOutOfRangeException(nameof(data), "Length greater than max file size");

			readWriteLock.AcquireWriteLock();

			try
			{
				InternalWrite(data);
			}
			finally
			{
				readWriteLock.ReleaseWriteLock();
				fileWaitHandle.Set();
				fileWaitHandle.Reset();
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
				fileWaitHandle.Set();
				fileWaitHandle.Reset();
			}
		}

		private void FileWatcher()
		{
			var waitHandles = new[]
			{
				disposeWaitHandle,
				fileWaitHandle
			};

			while (!disposed)
			{
				var result = WaitHandle.WaitAny(waitHandles, TinyReadWriteLock.DefaultWaitTimeout);

				// Triggers when disposed
				if (result == 0 || disposed)
					return;

				// Triggers when the file is changed
				if (result == 1)
					FileUpdated?.Invoke(this, EventArgs.Empty);
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
			if (data.Length > MaxFileSize)
				throw new ArgumentOutOfRangeException(nameof(data), "Length greater than max file size");

			using (var accessor = memoryMappedFile.CreateViewAccessor())
			{
				accessor.Write(0, data.Length);
				accessor.WriteArray(sizeof(int), data, 0, data.Length);
			}
		}

		public static MemoryMappedFile CreateOrOpenMemoryMappedFile(string name, long maxFileSize)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("File must be named", nameof(name));

			if (maxFileSize <= 0)
				throw new ArgumentException("Max file size can not be less than 1 byte", nameof(maxFileSize));

			return MemoryMappedFile.CreateOrOpen("TinyMemoryMappedFile_MemoryMappedFile_" + name, maxFileSize + sizeof(int));
		}

		public static EventWaitHandle CreateEventWaitHandle(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("EventWaitHandle must be named", nameof(name));

			return new EventWaitHandle(false, EventResetMode.ManualReset, "TinyMemoryMappedFile_WaitHandle_" + name);
		}
	}
}
