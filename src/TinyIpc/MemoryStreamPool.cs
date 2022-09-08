using Microsoft.IO;

namespace TinyIpc;

internal static class MemoryStreamPool
{
	public static RecyclableMemoryStreamManager Manager { get; } = new();
}
