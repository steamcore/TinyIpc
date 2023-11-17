using TinyIpc.DependencyInjection;

namespace GenericHost;

public partial class PublishWorker(LoremIpsum loremIpsum, ITinyIpcFactory tinyIpcFactory, ILogger<PublishWorker> logger)
	: BackgroundService
{
	private readonly ILogger<PublishWorker> logger = logger;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Create a new instance, can be called multiple times to create multiple instances, remember to dispose
		using var tinyIpcInstance = tinyIpcFactory.CreateInstance();

		// Say hello
		await PublishMessage(tinyIpcInstance, "hello");

		try
		{
			var rnd = new Random();

			while (!stoppingToken.IsCancellationRequested)
			{
				// Random delay to make it interesting, comment out the delay to make really make it go
				await Task.Delay(rnd.Next(1_000, 3_000), stoppingToken);

				// Say nonsense
				await PublishMessage(tinyIpcInstance, loremIpsum.GetSentence());
			}

		}
		finally
		{
			// Say goodbye
			await PublishMessage(tinyIpcInstance, "goodbye");

			LogCount(tinyIpcInstance.MessageBus.MessagesPublished);
		}

		static async ValueTask PublishMessage(ITinyIpcInstance tinyIpcInstance, string message)
		{
			var serializedMessage = await SerializeMessage(message);

			await tinyIpcInstance.MessageBus.PublishAsync(serializedMessage);
		}

		static async ValueTask<IReadOnlyList<byte>> SerializeMessage(string sentence)
		{
			var message = new WorkerMessage { ProcessId = Environment.ProcessId, Sentence = sentence };

			return await message.Serialize();
		}
	}

	[LoggerMessage(1, LogLevel.Information, "Published {count} messages")]
	private partial void LogCount(long count);
}
