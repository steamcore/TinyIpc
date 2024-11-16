using System.Diagnostics.CodeAnalysis;
using TinyIpc.DependencyInjection;

namespace GenericHost;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "DI")]
internal sealed partial class ReceiverWorker(ITinyIpcFactory tinyIpcFactory, ILogger<ReceiverWorker> logger)
	: BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Create a new instance, can be called multiple times to create multiple instances, remember to dispose
		using var tinyIpcInstance = tinyIpcFactory.CreateInstance();

		try
		{
			// Subscribe to messages being published
			await foreach (var message in tinyIpcInstance.MessageBus.SubscribeAsync(stoppingToken))
			{
				var workerMessage = WorkerMessage.Deserialize(message);

				LogMessage(workerMessage.ProcessId, workerMessage.Sentence);
			}
		}
		finally
		{
			LogCount(tinyIpcInstance.MessageBus.MessagesReceived);
		}
	}

	[LoggerMessage(1, LogLevel.Information, "Process {pid} says: {sentence}")]
	private partial void LogMessage(int pid, string sentence);

	[LoggerMessage(2, LogLevel.Information, "Received {count} messages")]
	private partial void LogCount(long count);
}
