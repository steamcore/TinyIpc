using System;

namespace TinyIpc
{
	public interface ITinyMemoryMappedFile
	{
		event EventHandler FileUpdated;

		/// <summary>
		/// Reads the content of the memory mapped file with a read lock in place.
		/// </summary>
		/// <returns>File content</returns>
		byte[] Read();

		/// <summary>
		/// Replaces the content of the memory mapped file with a write lock in place.
		/// </summary>
		void Write(byte[] data);

		/// <summary>
		/// Reads and then replaces the content of the memory mapped file with a write lock in place.
		/// </summary>
		void ReadWrite(Func<byte[], byte[]> updateFunc);
	}
}
