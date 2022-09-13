using TinyIpc.DependencyInjection;
using TinyIpc.Messaging;

namespace GenericHost;

public partial class Worker : BackgroundService
{
	private readonly ILogger<Worker> logger;
	private readonly LoremIpsum loremIpsum;
	private readonly ITinyIpcFactory tinyIpcFactory;

	public Worker(LoremIpsum loremIpsum, ITinyIpcFactory tinyIpcFactory, ILogger<Worker> logger)
	{
		this.logger = logger;
		this.loremIpsum = loremIpsum;
		this.tinyIpcFactory = tinyIpcFactory;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Create a new instance, can be called multiple times to create multiple instances, remember to dispose
		using var tinyIpcInstance = tinyIpcFactory.CreateInstance();

		// Say hello
		await LogAndSend(tinyIpcInstance.MessageBus, "hello");

		// Listen to messages being published
		tinyIpcInstance.MessageBus.MessageReceived += (sender, e) =>
		{
			var message = WorkerMessage.Deserialize(e.Message);

			LogMessage(message.ProcessId, message.Sentence);
		};

		try
		{
			var rnd = new Random();

			while (!stoppingToken.IsCancellationRequested)
			{
				// Random delay to make it interesting
				await Task.Delay(rnd.Next(1_000, 5_000), stoppingToken);

				await LogAndSend(tinyIpcInstance.MessageBus, loremIpsum.GetSentence());
			}

		}
		finally
		{
			// Say goodbye
			await LogAndSend(tinyIpcInstance.MessageBus, "goodbye");
		}

		async Task LogAndSend(ITinyMessageBus messageBus, string sentence)
		{
			LogMessage(Environment.ProcessId, sentence);

			// Send a message to the message bus
			await messageBus.PublishAsync(new WorkerMessage { ProcessId = Environment.ProcessId, Sentence = sentence }.Serialize());
		}
	}

	[LoggerMessage(1, LogLevel.Information, "Process {pid} says: {sentence}")]
	private partial void LogMessage(int pid, string sentence);
}
