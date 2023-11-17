using MessagePack;
using MessagePack.Resolvers;

namespace TinyIpc;

internal static class MessagePackOptions
{
	internal static MessagePackSerializerOptions Instance { get; } =
		MessagePackSerializerOptions.Standard
			.WithResolver(
				CompositeResolver.Create(
					TinyIpcGeneratedResolver.Instance,
					StandardResolver.Instance
				)
			);
}
