using System.Diagnostics.CodeAnalysis;
using TinyIpc.DependencyInjection;

namespace GenericHost;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "DI")]
internal sealed partial class PublishWorker(LoremIpsum loremIpsum, ITinyIpcFactory tinyIpcFactory, ILogger<PublishWorker> logger)
	: BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Create a new instance, can be called multiple times to create multiple instances, remember to dispose
		using var tinyIpcInstance = tinyIpcFactory.CreateInstance();

		// Say hello
		await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage("hello"), stoppingToken);

		try
		{
			var rnd = new Random();

			while (!stoppingToken.IsCancellationRequested)
			{
				// Random delay to make it interesting, comment out the delay to make really make it go
				await Task.Delay(rnd.Next(1_000, 3_000), stoppingToken);

				// Say nonsense
				await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage(loremIpsum.GetSentence()), stoppingToken);
			}

		}
		finally
		{
			// Say goodbye
			await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage("goodbye"), stoppingToken);

			LogCount(tinyIpcInstance.MessageBus.MessagesPublished);
		}

		static BinaryData SerializeMessage(string sentence)
		{
			return new WorkerMessage { ProcessId = Environment.ProcessId, Sentence = sentence }.Serialize();
		}
	}

	[LoggerMessage(1, LogLevel.Information, "Published {count} messages")]
	private partial void LogCount(long count);
}
