namespace TinyIpc.Messaging;

public class TinyMessageReceivedEventArgs(IReadOnlyList<byte> message)
	: EventArgs
{
	public IReadOnlyList<byte> Message { get; } = message;
}
