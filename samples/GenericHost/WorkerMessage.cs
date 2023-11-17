using MessagePack;

namespace GenericHost;

[MessagePackObject]
public class WorkerMessage
{
	[Key(1)]
	public int ProcessId { get; set; }

	[Key(2)]
	public string Sentence { get; set; } = string.Empty;

	public byte[] Serialize()
	{
		using var ms = new MemoryStream();

		MessagePackSerializer.Serialize(ms, this, MessagePackOptions.Instance);

		return ms.ToArray();
	}

	public static WorkerMessage Deserialize(IReadOnlyList<byte> data)
	{
		using var ms = new MemoryStream([.. data]);

		return MessagePackSerializer.Deserialize<WorkerMessage>(ms, MessagePackOptions.Instance);
	}
}
