using System.Text;
using Shouldly;
using TinyIpc.IO;
using TinyIpc.Synchronization;
using Xunit;

namespace TinyIpc.Messaging;

public class TinyMessageBusTests
{
	[Fact]
	public async Task Messages_sent_from_one_bus_should_be_received_by_the_other_event_handler()
	{
		using var messagebus1 = new TinyMessageBus("Example");
		using var messagebus2 = new TinyMessageBus("Example");

		var received = "nope";

		messagebus2.MessageReceived += (sender, e) => received = Encoding.UTF8.GetString(e.Message.ToArray());

		await messagebus1.PublishAsync(Encoding.UTF8.GetBytes("lorem"));
		await messagebus2.PublishAsync(Encoding.UTF8.GetBytes("ipsum"));
		await messagebus1.PublishAsync(Encoding.UTF8.GetBytes("yes"));

		await messagebus2.ReadAsync();

		// Disposing the message bus forces the read task to finish
		messagebus2.Dispose();

		received.ShouldBe("yes");
	}

	[Fact]
	public async Task Messages_sent_from_one_bus_should_be_received_by_the_other_subscriber()
	{
		using var messagebus1 = new TinyMessageBus("Example");
		using var messagebus2 = new TinyMessageBus("Example");

		var received = "nope";

		var subscribeTask = Task.Run(async () =>
		{
			await foreach (var message in messagebus2.SubscribeAsync())
			{
				received = Encoding.UTF8.GetString(message.ToArray());
			}
		});

		await messagebus1.PublishAsync(Encoding.UTF8.GetBytes("lorem"));
		await messagebus2.PublishAsync(Encoding.UTF8.GetBytes("ipsum"));
		await messagebus1.PublishAsync(Encoding.UTF8.GetBytes("yes"));

		await messagebus2.ReadAsync();

		// Disposing the message bus forces the read task to finish
		messagebus2.Dispose();
		await subscribeTask;

		received.ShouldBe("yes");
	}

	[Fact]
	public async Task All_messages_should_be_processed_even_with_multiple_buses_in_a_complex_scenario()
	{
		var messagesPerRound = 32;
		var firstRound = 16;
		var secondRound = 16;
		var total = firstRound + secondRound;
		var rnd = new Random();

		// Start up two chatty buses talking to each other
		using var messagebus1 = new TinyMessageBus("Example");
		using var messagebus2 = new TinyMessageBus("Example");

		var buses = new[] { messagebus1, messagebus2 };

		for (var i = 0; i < firstRound; i++)
		{
			var messages = Enumerable.Range(0, messagesPerRound).Select(_ => Guid.NewGuid().ToByteArray()).ToList();
			await buses[rnd.Next() % buses.Length].PublishAsync(messages);
		}

		// Add a new bus to the mix
		using var messagebus3 = new TinyMessageBus("Example");

		buses = [messagebus1, messagebus2, messagebus3];

		for (var i = 0; i < secondRound; i++)
		{
			var messages = Enumerable.Range(0, messagesPerRound).Select(_ => Guid.NewGuid().ToByteArray()).ToList();
			await buses[rnd.Next() % buses.Length].PublishAsync(messages);
		}

		// Force a final read of all messages to work around timing issuees
		await messagebus1.ReadAsync();
		await messagebus2.ReadAsync();
		await messagebus3.ReadAsync();

		// Disposing the message buses forces the read tasks to finish
		messagebus1.Dispose();
		messagebus2.Dispose();
		messagebus3.Dispose();

		// Counters should check out
		messagebus1.MessagesReceived.ShouldBe(total * messagesPerRound - messagebus1.MessagesPublished);
		messagebus2.MessagesReceived.ShouldBe(total * messagesPerRound - messagebus2.MessagesPublished);
		messagebus3.MessagesReceived.ShouldBe(secondRound * messagesPerRound - messagebus3.MessagesPublished);
	}

	[Fact]
	public async Task All_primitives_should_be_configurable()
	{
		var name = "Example";
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
		await messageBus.PublishAsync(Encoding.UTF8.GetBytes("lorem"));
		await messageBus.PublishAsync(Encoding.UTF8.GetBytes("ipsum"));

		messageBus.MessagesPublished.ShouldBe(2);
	}
}
