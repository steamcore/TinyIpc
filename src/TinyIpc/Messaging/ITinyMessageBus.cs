using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TinyIpc.Messaging
{
	public interface ITinyMessageBus
	{
		/// <summary>
		/// Called whenever a new message is received
		/// </summary>
		event EventHandler<TinyMessageReceivedEventArgs> MessageReceived;

		long MessagesSent { get; }
		long MessagesPublished { get; }
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
}
