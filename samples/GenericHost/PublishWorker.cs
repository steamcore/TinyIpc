using TinyIpc.DependencyInjection;

namespace GenericHost;

public partial class PublishWorker : BackgroundService
{
	private readonly LoremIpsum loremIpsum;
	private readonly ITinyIpcFactory tinyIpcFactory;
	private readonly ILogger<PublishWorker> logger;

	public PublishWorker(LoremIpsum loremIpsum, ITinyIpcFactory tinyIpcFactory, ILogger<PublishWorker> logger)
	{
		this.loremIpsum = loremIpsum;
		this.tinyIpcFactory = tinyIpcFactory;
		this.logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Create a new instance, can be called multiple times to create multiple instances, remember to dispose
		using var tinyIpcInstance = tinyIpcFactory.CreateInstance();

		// Say hello
		await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage("hello"));

		try
		{
			var rnd = new Random();

			while (!stoppingToken.IsCancellationRequested)
			{
				// Random delay to make it interesting, comment out the delay to make really make it go
				await Task.Delay(rnd.Next(1_000, 3_000), stoppingToken);

				// Say nonsense
				await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage(loremIpsum.GetSentence()));
			}

		}
		finally
		{
			// Say goodbye
			await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage("goodbye"));

			LogCount(tinyIpcInstance.MessageBus.MessagesPublished);
		}

		static IReadOnlyList<byte> SerializeMessage(string sentence)
		{
			return new WorkerMessage { ProcessId = Environment.ProcessId, Sentence = sentence }.Serialize();
		}
	}

	[LoggerMessage(1, LogLevel.Information, "Published {count} messages")]
	private partial void LogCount(long count);
}
