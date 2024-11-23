using System.Diagnostics.CodeAnalysis;
using TinyIpc.Messaging;

namespace GenericHost;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "DI")]
internal sealed partial class PublishWorker(ITinyMessageBus tinyMessageBus, LoremIpsum loremIpsum, ILogger<PublishWorker> logger)
	: BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Say hello
		await PublishMessage("hello", stoppingToken);

		try
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				// Random delay to make it interesting, comment out the delay to make really make it go
				await Task.Delay(Random.Shared.Next(1_000, 3_000), stoppingToken);

				// Say nonsense
				await PublishMessage(loremIpsum.GetSentence(), stoppingToken);
			}
		}
		finally
		{
			// Say goodbye, not using stoppingToken or the message won't be sent
			await PublishMessage("goodbye", default);

			LogCount(tinyMessageBus.MessagesPublished);
		}

	}

	private Task PublishMessage(string sentence, CancellationToken cancellationToken)
	{
		var message = new WorkerMessage
		{
			ProcessId = Environment.ProcessId,
			Sentence = sentence
		};

		LogMessage(message.ProcessId, message.Sentence);

		return tinyMessageBus.PublishAsync(message.Serialize(), cancellationToken);
	}

	[LoggerMessage(1, LogLevel.Information, "Published message as {pid}: {sentence}")]
	private partial void LogMessage(int pid, string sentence);

	[LoggerMessage(2, LogLevel.Information, "Published {count} messages")]
	private partial void LogCount(long count);
}
