namespace TinyIpc.Messaging;

public class TinyMessageReceivedEventArgs(BinaryData message)
	: EventArgs
{
	public BinaryData Message { get; } = message;
}
