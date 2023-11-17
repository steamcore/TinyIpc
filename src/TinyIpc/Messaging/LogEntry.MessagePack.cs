#if USE_MESSAGEPACK
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using MessagePack;
#if NET
using MessagePack.Formatters;
#endif

namespace TinyIpc.Messaging;

[MessagePackObject]
public sealed class LogEntry
{
	public static long Overhead { get; }

	[Key(0)]
	public long Id { get; set; }

	[Key(1)]
	public Guid Instance { get; set; }

	[Key(2)]
	public DateTime Timestamp { get; set; }

	[Key(3)]
	public IReadOnlyList<byte> Message { get; set; } = Array.Empty<byte>();

	static LogEntry()
	{
		using var memoryStream = MemoryStreamPool.Manager.GetStream(nameof(LogEntry));
		MessagePackSerializer.Serialize(
			memoryStream,
			new LogEntry { Id = long.MaxValue, Instance = Guid.Empty, Timestamp = DateTime.UtcNow },
			MessagePackOptions.Instance
		);
		Overhead = memoryStream.Length;
	}

	// Make sure necessary MessagePack types aren't trimmed
#if NET
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	[SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Unused on purpose")]
	[SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration.", Justification = "Unused on purpose")]
	private static readonly Type byteFormatter = typeof(InterfaceReadOnlyListFormatter<byte>);

	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
	[SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "Unused on purpose")]
	[SuppressMessage("Roslynator", "RCS1213:Remove unused member declaration.", Justification = "Unused on purpose")]
	private static readonly Type logEntryFormatter = typeof(InterfaceReadOnlyListFormatter<LogEntry>);
#endif
}
#endif
