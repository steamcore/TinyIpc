using System;
using System.Threading.Tasks;

namespace TinyIpc
{
	public interface ITinyMessageBus
	{
		/// <summary>
		/// Called whenever a new message is received
		/// </summary>
		event EventHandler<TinyMessageReceivedEventArgs> MessageReceived;

		long MessagesSent { get; }
		long MessagesReceived { get; }

		/// <summary>
		/// Resets MessagesSent and MessagesReceived counters
		/// </summary>
		void ResetMetrics();

		/// <summary>
		/// Publishes a message to the message bus as soon as possible in an async task
		/// </summary>
		/// <param name="message"></param>
		Task PublishAsync(string message);
	}
}
