namespace TinyIpc.Messaging;

public class TinyMessageReceivedEventArgs : EventArgs
{
	public IReadOnlyList<byte> Message { get; }

	public TinyMessageReceivedEventArgs(IReadOnlyList<byte> message)
	{
		Message = message;
	}
}
