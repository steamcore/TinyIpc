namespace TinyIpc.Messaging;

public interface ITinyMessageBus
{
	/// <summary>
	/// Called whenever a new message is received
	/// </summary>
	event EventHandler<TinyMessageReceivedEventArgs>? MessageReceived;

	/// <summary>
	/// Number of messages that have been published by this message bus
	/// </summary>
	long MessagesPublished { get; }

	/// <summary>
	/// Number of messages that have been received by this message bus
	/// </summary>
	long MessagesReceived { get; }

	/// <summary>
	/// Resets MessagesSent and MessagesReceived counters
	/// </summary>
	void ResetMetrics();

	/// <summary>
	/// Publish a message to the message bus
	/// </summary>
	/// <param name="message"></param>
	Task PublishAsync(byte[] message);

	/// <summary>
	/// Publish a number of messages to the message bus
	/// </summary>
	/// <param name="messages"></param>
	Task PublishAsync(IEnumerable<byte[]> messages);
}
