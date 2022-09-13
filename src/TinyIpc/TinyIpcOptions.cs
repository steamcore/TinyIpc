using System.Diagnostics;

namespace TinyIpc;

public class TinyIpcOptions
{
	public const int DefaultMaxFileSize = 1024 * 1024;
	public const int DefaultMaxReaderCount = 6;

	public static readonly TimeSpan DefaultMinMessageAge = TimeSpan.FromMilliseconds(500);
	public static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(5);

	/// <summary>
	/// The name of this set of locks and memory mapped file, default value is process name.
	/// </summary>
	public string Name { get; set; } = Process.GetCurrentProcess().ProcessName;

	/// <summary>
	/// The maximum amount of data that can be written to the file memory mapped file, default is 1 MiB
	/// </summary>
	public long MaxFileSize { get; set; } = DefaultMaxFileSize;

	/// <summary>
	/// Maxium simultaneous readers, default is 6
	/// </summary>
	public int MaxReaderCount { get; set; } = DefaultMaxReaderCount;

	/// <summary>
	/// The minimum amount of time messages are required to live before removal from the file, default is half a second
	/// </summary>
	public TimeSpan MinMessageAge { get; set; } = DefaultMinMessageAge;

	/// <summary>
	/// How long to wait before giving up aquiring read and write locks, default is 5 seconds
	/// </summary>
	public TimeSpan WaitTimeout { get; set; } = DefaultWaitTimeout;
}
