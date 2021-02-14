using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
#if NET
using System.Runtime.Versioning;
#endif
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

		public event EventHandler? FileUpdated;

		public long MaxFileSize { get; }

		public const int DefaultMaxFileSize = 1024 * 1024;

		/// <summary>
		/// Initializes a new instance of the TinyMemoryMappedFile class.
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
#if NET
		[SupportedOSPlatform("windows")]
#endif
		public TinyMemoryMappedFile(string name)
			: this(name, DefaultMaxFileSize)
		{
		}

		/// <summary>
		/// Initializes a new instance of the TinyMemoryMappedFile class.
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
		/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
		[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Incorrect warning, lock is being disposed")]
#if NET
		[SupportedOSPlatform("windows")]
#endif
		public TinyMemoryMappedFile(string name, long maxFileSize)
			: this(name, maxFileSize, new TinyReadWriteLock(name), disposeLock: true)
		{
		}

		/// <summary>
		/// Initializes a new instance of the TinyMemoryMappedFile class.
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended before use</param>
		/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
		/// <param name="readWriteLock">A read/write lock that will be used to control access to the memory mapped file</param>
		/// <param name="disposeLock">Set to true if the read/write lock is to be disposed when this instance is disposed</param>
#if NET
		[SupportedOSPlatform("windows")]
#endif
		public TinyMemoryMappedFile(string name, long maxFileSize, ITinyReadWriteLock readWriteLock, bool disposeLock)
			: this(CreateOrOpenMemoryMappedFile(name, maxFileSize), CreateEventWaitHandle(name), maxFileSize, readWriteLock, disposeLock)
		{
		}

		/// <summary>
		/// Initializes a new instance of the TinyMemoryMappedFile class.
		/// </summary>
		/// <param name="memoryMappedFile">An instance of a memory mapped file</param>
		/// <param name="fileWaitHandle">A manual reset EventWaitHandle that is to be used to signal file changes</param>
		/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
		/// <param name="readWriteLock">A read/write lock that will be used to control access to the memory mapped file</param>
		/// <param name="disposeLock">Set to true if the read/write lock is to be disposed when this instance is disposed</param>
		public TinyMemoryMappedFile(MemoryMappedFile memoryMappedFile, EventWaitHandle fileWaitHandle, long maxFileSize, ITinyReadWriteLock readWriteLock, bool disposeLock)
		{
			this.readWriteLock = readWriteLock ?? throw new ArgumentNullException(nameof(readWriteLock));
			this.memoryMappedFile = memoryMappedFile ?? throw new ArgumentNullException(nameof(memoryMappedFile));
			this.fileWaitHandle = fileWaitHandle ?? throw new ArgumentNullException(nameof(fileWaitHandle));
			this.disposeLock = disposeLock;

			MaxFileSize = maxFileSize;

			disposeWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

			fileWatcherTask = Task.Run(() => FileWatcher());
		}

		~TinyMemoryMappedFile()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
				return;

			// Always set the dispose wait handle even when dispised  by the finalizer
			// otherwize the file watcher task will needleessly have to wait for its timeout.
			disposeWaitHandle?.Set();
			fileWatcherTask?.Wait(TinyReadWriteLock.DefaultWaitTimeout);

			if (disposing)
			{
				memoryMappedFile.Dispose();

				if (disposeLock && readWriteLock is IDisposable disposableLock)
				{
					disposableLock.Dispose();
				}

				fileWaitHandle.Dispose();
				disposeWaitHandle?.Dispose();
			}

			disposed = true;
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
				using var accessor = memoryMappedFile.CreateViewAccessor();
				return accessor.ReadInt32(0);
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
			if (data is null)
				throw new ArgumentNullException(nameof(data));

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
			if (updateFunc is null)
				throw new ArgumentNullException(nameof(updateFunc));

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
			using var accessor = memoryMappedFile.CreateViewAccessor();
			var length = accessor.ReadInt32(0);
			var data = new byte[length];
			accessor.ReadArray(sizeof(int), data, 0, length);
			return data;
		}

		private void InternalWrite(byte[] data)
		{
			if (data.Length > MaxFileSize)
				throw new ArgumentOutOfRangeException(nameof(data), "Length greater than max file size");

			using var accessor = memoryMappedFile.CreateViewAccessor();
			accessor.Write(0, data.Length);
			accessor.WriteArray(sizeof(int), data, 0, data.Length);
		}

		/// <summary>
		/// Create or open a MemoryMappedFile that can be used to construct a TinyMemoryMappedFile
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended</param>
		/// <param name="maxFileSize">The maximum amount of data that can be written to the file memory mapped file</param>
		/// <returns>A system wide MemoryMappedFile</returns>
#if NET
		[SupportedOSPlatform("windows")]
#endif
		public static MemoryMappedFile CreateOrOpenMemoryMappedFile(string name, long maxFileSize)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("File must be named", nameof(name));

			if (maxFileSize <= 0)
				throw new ArgumentException("Max file size can not be less than 1 byte", nameof(maxFileSize));

			return MemoryMappedFile.CreateOrOpen("TinyMemoryMappedFile_MemoryMappedFile_" + name, maxFileSize + sizeof(int));
		}

		/// <summary>
		/// Create or open an EventWaitHandle that can be used to construct a TinyMemoryMappedFile
		/// </summary>
		/// <param name="name">A system wide unique name, the name will have a prefix appended</param>
		/// <returns>A system wide EventWaitHandle</returns>
		public static EventWaitHandle CreateEventWaitHandle(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("EventWaitHandle must be named", nameof(name));

			return new EventWaitHandle(false, EventResetMode.ManualReset, "TinyMemoryMappedFile_WaitHandle_" + name);
		}
	}
}
