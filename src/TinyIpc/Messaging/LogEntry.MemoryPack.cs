#if USE_MEMORYPACK
using MemoryPack;

namespace TinyIpc.Messaging;

[MemoryPackable]
public sealed partial class LogEntry
{
	private static long? overhead;

	public static long Overhead => overhead ??= GetOverhead();

	public long Id { get; set; }

	public Guid Instance { get; set; }

	public DateTime Timestamp { get; set; }

	public IReadOnlyList<byte> Message { get; set; } = Array.Empty<byte>();

	private static long GetOverhead()
	{
		var result = MemoryPackSerializer.Serialize(new LogEntry { Id = long.MaxValue, Instance = Guid.Empty, Timestamp = DateTime.UtcNow });
		return result.Length;
	}
}
#endif
