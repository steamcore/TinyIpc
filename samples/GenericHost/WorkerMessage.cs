using MessagePack;

namespace GenericHost;

[MessagePackObject]
public class WorkerMessage
{
	[Key(1)]
	public int ProcessId { get; set; }

	[Key(2)]
	public string Sentence { get; set; } = string.Empty;

	public BinaryData Serialize()
	{
		using var ms = new MemoryStream();

		MessagePackSerializer.Serialize(ms, this, MessagePackOptions.Instance);

		return BinaryData.FromBytes(ms.ToArray());
	}

	public static WorkerMessage Deserialize(BinaryData data)
	{
		ArgumentNullException.ThrowIfNull(data);

		using var ms = data.ToStream();

		return MessagePackSerializer.Deserialize<WorkerMessage>(ms, MessagePackOptions.Instance);
	}
}
