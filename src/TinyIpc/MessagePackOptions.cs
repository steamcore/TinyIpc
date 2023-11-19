using MessagePack;
using MessagePack.Resolvers;
using TinyIpc.Messaging;

namespace TinyIpc;

internal static class MessagePackOptions
{
	internal static MessagePackSerializerOptions Instance { get; } =
		MessagePackSerializerOptions.Standard
			.WithResolver(
				CompositeResolver.Create(
					LogBookResolver.Instance,
					StandardResolver.Instance
				)
			);
}
