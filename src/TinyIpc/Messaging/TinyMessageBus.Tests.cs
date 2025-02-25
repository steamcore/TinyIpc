using Shouldly;
using TinyIpc.IO;
using TinyIpc.Synchronization;

namespace TinyIpc.Messaging;

public class TinyMessageBusTests
{
	[Test]
	public async Task Messages_sent_from_one_bus_should_be_received_by_the_other_event_handler(CancellationToken cancellationToken)
	{
		var name = Guid.NewGuid().ToString();

		using var messagebus1 = new TinyMessageBus(name);
		using var messagebus2 = new TinyMessageBus(name);

		var received = "nope";

		messagebus2.MessageReceived += (sender, e) => received = e.Message.ToString();

		await messagebus1.PublishAsync(BinaryData.FromString("lorem"), cancellationToken);
		await messagebus2.PublishAsync(BinaryData.FromString("ipsum"), cancellationToken);
		await messagebus1.PublishAsync(BinaryData.FromString("yes"), cancellationToken);

		await messagebus2.ReadAsync();

		// Disposing the message buses forces the read tasks to finish
		await messagebus2.DisposeAsync();

		received.ShouldBe("yes");
	}

	[Test]
	public async Task Messages_sent_from_one_bus_should_be_received_by_the_other_subscriber(CancellationToken cancellationToken)
	{
		var name = Guid.NewGuid().ToString();

		using var messagebus1 = new TinyMessageBus(name);
		using var messagebus2 = new TinyMessageBus(name);

		var received = "nope";

		var subscribeTcs = new TaskCompletionSource<bool>();
		var subscribeTask = Task.Run(async () =>
		{
			subscribeTcs.SetResult(true);
			await foreach (var message in messagebus2.SubscribeAsync(cancellationToken))
			{
				received = message.ToString();
			}
		}, cancellationToken);


		// Wait for the subscribe task to start
		await subscribeTcs.Task;
		await Task.Delay(100, cancellationToken);

		await messagebus1.PublishAsync(BinaryData.FromString("lorem"), cancellationToken);
		await messagebus2.PublishAsync(BinaryData.FromString("ipsum"), cancellationToken);
		await messagebus1.PublishAsync(BinaryData.FromString("yes"), cancellationToken);

		await messagebus2.ReadAsync();

		// Disposing the message bus forces the read task to finish
		await messagebus2.DisposeAsync();
		await subscribeTask;

		received.ShouldBe("yes");
	}

	[Test]
	public async Task All_messages_should_be_processed_even_with_multiple_buses_in_a_complex_scenario(CancellationToken cancellationToken)
	{
		var name = Guid.NewGuid().ToString();
		var messagesPerRound = 32;
		var firstRound = 16;
		var secondRound = 16;
		var total = firstRound + secondRound;
		var rnd = new Random();

		// Start up two chatty buses talking to each other
		using var messagebus1 = new TinyMessageBus(name);
		using var messagebus2 = new TinyMessageBus(name);

		var buses = new[] { messagebus1, messagebus2 };

		for (var i = 0; i < firstRound; i++)
		{
			var messages = Enumerable.Range(0, messagesPerRound).Select(_ => BinaryData.FromBytes(Guid.NewGuid().ToByteArray())).ToList();
			await buses[rnd.Next() % buses.Length].PublishAsync(messages, cancellationToken);
		}

		// Add a new bus to the mix
		using var messagebus3 = new TinyMessageBus(name);

		buses = [messagebus1, messagebus2, messagebus3];

		for (var i = 0; i < secondRound; i++)
		{
			var messages = Enumerable.Range(0, messagesPerRound).Select(_ => BinaryData.FromBytes(Guid.NewGuid().ToByteArray())).ToList();
			await buses[rnd.Next() % buses.Length].PublishAsync(messages, cancellationToken);
		}

		// Force a final read of all messages to work around timing issuees
		await messagebus1.ReadAsync();
		await messagebus2.ReadAsync();
		await messagebus3.ReadAsync();

		// Disposing the message buses forces the read tasks to finish
		await messagebus1.DisposeAsync();
		await messagebus2.DisposeAsync();
		await messagebus3.DisposeAsync();

		// Counters should check out
		messagebus1.MessagesReceived.ShouldBe(total * messagesPerRound - messagebus1.MessagesPublished);
		messagebus2.MessagesReceived.ShouldBe(total * messagesPerRound - messagebus2.MessagesPublished);
		messagebus3.MessagesReceived.ShouldBe(secondRound * messagesPerRound - messagebus3.MessagesPublished);
	}

	[Test]
	public async Task All_primitives_should_be_configurable(CancellationToken cancellationToken)
	{
		var name = Guid.NewGuid().ToString();
		var maxReaderCount = TinyIpcOptions.DefaultMaxReaderCount;
		var maxFileSize = TinyIpcOptions.DefaultMaxFileSize;
		var waitTimeout = TinyIpcOptions.DefaultWaitTimeout;

		// Create underlying primitives first so they can be configured
		using var lockMutex = TinyReadWriteLock.CreateMutex(name);
		using var lockSemaphore = TinyReadWriteLock.CreateSemaphore(name, maxReaderCount);
		using var memoryMappedFile = TinyMemoryMappedFile.CreateOrOpenMemoryMappedFile(name, maxFileSize);
		using var eventWaitHandle = TinyMemoryMappedFile.CreateEventWaitHandle(name);

		// Create the actual message bus
		using var tinyReadWriteLock = new TinyReadWriteLock(lockMutex, lockSemaphore, maxReaderCount, waitTimeout);
		using var tinyMemoryMappedFile = new TinyMemoryMappedFile(memoryMappedFile, eventWaitHandle, maxFileSize, tinyReadWriteLock, disposeLock: true);

		using var messageBus = new TinyMessageBus(tinyMemoryMappedFile, disposeFile: true);
		await messageBus.PublishAsync(BinaryData.FromString("lorem"), cancellationToken);
		await messageBus.PublishAsync(BinaryData.FromString("ipsum"), cancellationToken);

		messageBus.MessagesPublished.ShouldBe(2);
	}
}
