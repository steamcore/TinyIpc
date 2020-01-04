using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyIpc.IO;
using TinyIpc.Messaging;
using TinyIpc.Synchronization;
using Xunit;

namespace TinyIpc.Tests
{
	public class TinyMessageBusTests
	{
		[Fact]
		public async Task Messages_sent_from_one_bus_should_be_received_by_the_other()
		{
			using var messagebus1 = new TinyMessageBus("Example");
			using var messagebus2 = new TinyMessageBus("Example");

			var received = "nope";

			messagebus2.MessageReceived += (sender, e) => received = Encoding.UTF8.GetString(e.Message);

			await messagebus1.PublishAsync(Encoding.UTF8.GetBytes("lorem"));
			await messagebus2.PublishAsync(Encoding.UTF8.GetBytes("ipsum"));
			await messagebus1.PublishAsync(Encoding.UTF8.GetBytes("yes"));

			await messagebus2.ReadAsync();

			Assert.Equal("yes", received);
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

			for (int i = 0; i < firstRound; i++)
			{
				var messages = Enumerable.Range(0, messagesPerRound).Select(_ => Guid.NewGuid().ToByteArray());
				await buses[rnd.Next() % buses.Length].PublishAsync(messages);
			}

			// Add a new bus to the mix
			using var messagebus3 = new TinyMessageBus("Example");

			buses = new[] { messagebus1, messagebus2, messagebus3 };

			for (int i = 0; i < secondRound; i++)
			{
				var messages = Enumerable.Range(0, messagesPerRound).Select(_ => Guid.NewGuid().ToByteArray());
				await buses[rnd.Next() % buses.Length].PublishAsync(messages);
			}

			// Force a final read of all messages to work around timing issuees
			await messagebus1.ReadAsync();
			await messagebus2.ReadAsync();
			await messagebus3.ReadAsync();

			// Counters should check out
			Assert.Equal(total * messagesPerRound - messagebus1.MessagesPublished, messagebus1.MessagesReceived);
			Assert.Equal(total * messagesPerRound - messagebus2.MessagesPublished, messagebus2.MessagesReceived);
			Assert.Equal(secondRound * messagesPerRound - messagebus3.MessagesPublished, messagebus3.MessagesReceived);
		}

		[Fact]
		public async Task All_primitives_should_be_configurable()
		{
			var name = "Example";
			var maxReaderCount = TinyReadWriteLock.DefaultMaxReaderCount;
			var maxFileSize = TinyMemoryMappedFile.DefaultMaxFileSize;
			var waitTimeout = TinyReadWriteLock.DefaultWaitTimeout;

			// Create underlying primitives first so they can be configured
			var lockMutex = TinyReadWriteLock.CreateMutex(name);
			var lockSemaphore = TinyReadWriteLock.CreateSemaphore(name, maxReaderCount);
			var memoryMappedFile = TinyMemoryMappedFile.CreateOrOpenMemoryMappedFile(name, maxFileSize);
			var eventWaitHandle = TinyMemoryMappedFile.CreateEventWaitHandle(name);

			// Create the actual message bus
			var tinyReadWriteLock = new TinyReadWriteLock(lockMutex, lockSemaphore, maxReaderCount, waitTimeout);
			var tinyMemoryMappedFile = new TinyMemoryMappedFile(memoryMappedFile, eventWaitHandle, maxFileSize, tinyReadWriteLock, disposeLock: true);

			using var messageBus = new TinyMessageBus(tinyMemoryMappedFile, disposeFile: true);
			await messageBus.PublishAsync(Encoding.UTF8.GetBytes("lorem"));
			await messageBus.PublishAsync(Encoding.UTF8.GetBytes("ipsum"));
		}
	}
}
