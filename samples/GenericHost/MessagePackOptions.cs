using MessagePack;
using MessagePack.Resolvers;

namespace GenericHost;

internal static class MessagePackOptions
{
	internal static MessagePackSerializerOptions Instance { get; } =
		MessagePackSerializerOptions.Standard
			.WithResolver(
				CompositeResolver.Create(
					GenericHostGeneratedResolver.Instance,
					StandardResolver.Instance
				)
			);
}
