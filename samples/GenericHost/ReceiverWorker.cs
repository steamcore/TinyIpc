using System.Diagnostics.CodeAnalysis;
using TinyIpc.Messaging;

namespace GenericHost;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "DI")]
internal sealed partial class ReceiverWorker(ITinyMessageBus tinyMessageBus, ILogger<ReceiverWorker> logger)
	: BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			// Subscribe to messages being published
			await foreach (var message in tinyMessageBus.SubscribeAsync(stoppingToken))
			{
				ReceiveMessage(message);
			}
		}
		finally
		{
			LogCount(tinyMessageBus.MessagesReceived);
		}
	}

	private void ReceiveMessage(BinaryData message)
	{
		var workerMessage = WorkerMessage.Deserialize(message);

		LogMessage(workerMessage.ProcessId, workerMessage.Sentence);
	}

	[LoggerMessage(1, LogLevel.Information, "Received message from {pid}: {sentence}")]
	private partial void LogMessage(int pid, string sentence);

	[LoggerMessage(2, LogLevel.Information, "Received {count} messages")]
	private partial void LogCount(long count);
}
