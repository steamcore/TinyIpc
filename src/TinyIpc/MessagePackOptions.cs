using MessagePack;
using MessagePack.Resolvers;

namespace TinyIpc;

[GeneratedMessagePackResolver]
internal sealed partial class AssemblyResolver;

[CompositeResolver(typeof(AssemblyResolver), typeof(StandardResolver))]
internal sealed partial class CompositeResolver;

internal static class MessagePackOptions
{
	internal static MessagePackSerializerOptions Instance { get; } =
		MessagePackSerializerOptions.Standard
			.WithResolver(CompositeResolver.Instance);
}
