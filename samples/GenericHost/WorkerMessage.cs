using MemoryPack;

namespace GenericHost;

[MemoryPackable]
public partial class WorkerMessage
{
	public int ProcessId { get; set; }

	public string Sentence { get; set; } = string.Empty;

	public async ValueTask<byte[]> Serialize()
	{
		using var ms = new MemoryStream();

		await MemoryPackSerializer.SerializeAsync(ms, this);

		return ms.ToArray();
	}

	public static async ValueTask<WorkerMessage> Deserialize(IReadOnlyList<byte> data)
	{
		using var ms = new MemoryStream([.. data]);

		return await MemoryPackSerializer.DeserializeAsync<WorkerMessage>(ms) ?? new WorkerMessage();
	}
}
