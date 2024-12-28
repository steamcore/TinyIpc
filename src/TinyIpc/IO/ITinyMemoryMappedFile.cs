namespace TinyIpc.IO;

public interface ITinyMemoryMappedFile : IDisposable
{
	/// <summary>
	/// Called whenever the file is written to
	/// </summary>
	event EventHandler? FileUpdated;

	/// <summary>
	/// The maximum amount of data that can be written to the file
	/// </summary>
	long MaxFileSize { get; }

	/// <summary>
	/// The name of the file if it was created with a name
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Gets the file size
	/// </summary>
	/// <returns>File size</returns>
	int GetFileSize(CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads the content of the memory mapped file with a read lock in place.
	/// </summary>
	/// <returns>File content</returns>
	T Read<T>(Func<MemoryStream, T> readData, CancellationToken cancellationToken = default);

	/// <summary>
	/// Replaces the content of the memory mapped file with a write lock in place.
	/// </summary>
	void Write(MemoryStream data, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads and then replaces the content of the memory mapped file with a write lock in place.
	/// </summary>
	void ReadWrite(Action<MemoryStream, MemoryStream> updateFunc, CancellationToken cancellationToken = default);
}
