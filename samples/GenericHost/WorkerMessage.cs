using ProtoBuf;

namespace GenericHost;

[ProtoContract]
public class WorkerMessage
{
	[ProtoMember(1)]
	public int ProcessId { get; set; }

	[ProtoMember(2)]
	public string Sentence { get; set; } = string.Empty;

	public byte[] Serialize()
	{
		using var ms = new MemoryStream();

		Serializer.Serialize(ms, this);

		return ms.ToArray();
	}

	public static WorkerMessage Deserialize(byte[] data)
	{
		using var ms = new MemoryStream(data);

		return Serializer.Deserialize<WorkerMessage>(ms);
	}
}
