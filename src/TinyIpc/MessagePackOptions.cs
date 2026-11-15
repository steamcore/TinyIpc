using MessagePack;

namespace TinyIpc;

internal static class MessagePackOptions
{
	internal static MessagePackSerializerOptions Instance { get; } =
		MessagePackSerializerOptions.DefaultAot;
}
