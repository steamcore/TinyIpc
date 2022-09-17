using TinyIpc.DependencyInjection;

namespace GenericHost;

public partial class PublishWorker : BackgroundService
{
	private readonly LoremIpsum loremIpsum;
	private readonly ITinyIpcFactory tinyIpcFactory;

	public PublishWorker(LoremIpsum loremIpsum, ITinyIpcFactory tinyIpcFactory)
	{
		this.loremIpsum = loremIpsum;
		this.tinyIpcFactory = tinyIpcFactory;
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
				// Random delay to make it interesting
				await Task.Delay(rnd.Next(1_000, 5_000), stoppingToken);

				// Say nonsense
				await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage(loremIpsum.GetSentence()));
			}

		}
		finally
		{
			// Say goodbye
			await tinyIpcInstance.MessageBus.PublishAsync(SerializeMessage("goodbye"));
		}

		static IReadOnlyList<byte> SerializeMessage(string sentence)
		{
			return new WorkerMessage { ProcessId = Environment.ProcessId, Sentence = sentence }.Serialize();
		}
	}
}
