namespace TinyIpc.Messaging;

public interface ITinyMessageBus : IDisposable, IAsyncDisposable
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
	/// The name of the message bus if it was created with a name
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Resets MessagesSent and MessagesReceived counters
	/// </summary>
	void ResetMetrics();

	/// <summary>
	/// Publish a message to the message bus
	/// </summary>
	/// <param name="message"></param>
	Task PublishAsync(BinaryData message, CancellationToken cancellationToken = default);

	/// <summary>
	/// Publish a number of messages to the message bus
	/// </summary>
	/// <param name="messages"></param>
	Task PublishAsync(IReadOnlyList<BinaryData> messages, CancellationToken cancellationToken = default);

	/// <summary>
	/// Subscribe to messages using an async enumerable
	/// </summary>
	/// <param name="cancellationToken"></param>
	IAsyncEnumerable<BinaryData> SubscribeAsync(CancellationToken cancellationToken = default);
}
