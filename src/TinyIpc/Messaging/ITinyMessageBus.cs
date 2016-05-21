using System;

namespace TinyIpc.Messaging
{
	public interface ITinyMessageBus
	{
		/// <summary>
		/// Called whenever a new message is received
		/// </summary>
		event EventHandler<TinyMessageReceivedEventArgs> MessageReceived;

		bool MessagesBeingProcessed { get; }
		long MessagesSent { get; }
		long MessagesReceived { get; }

		/// <summary>
		/// Resets MessagesSent and MessagesReceived counters
		/// </summary>
		void ResetMetrics();

		/// <summary>
		/// Publishes a message to the message bus as soon as possible in a background task
		/// </summary>
		/// <param name="message"></param>
		void PublishAsync(byte[] message);
	}
}
