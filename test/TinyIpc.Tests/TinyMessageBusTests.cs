using System;
using System.Text;
using TinyIpc.IO;
using TinyIpc.Messaging;
using TinyIpc.Synchronization;
using Xunit;

namespace TinyIpc.Tests
{
	public class TinyMessageBusTests
	{
		[Fact]
		public void Messages_sent_from_one_bus_should_be_received_by_the_other()
		{
			using (var messagebus1 = new TinyMessageBus("Example"))
			using (var messagebus2 = new TinyMessageBus("Example"))
			{
				var received = "nope";

				messagebus2.MessageReceived += (sender, e) => received = Encoding.UTF8.GetString(e.Message);

				messagebus1.PublishAsync(Encoding.UTF8.GetBytes("lorem"));
				messagebus2.PublishAsync(Encoding.UTF8.GetBytes("ipsum"));
				messagebus1.PublishAsync(Encoding.UTF8.GetBytes("yes"));

				messagebus1.ProcessIncomingMessages();
				messagebus2.ProcessIncomingMessages();

				Assert.Equal("yes", received);
			}
		}

		[Fact]
		public void All_messages_should_be_processed_even_with_multiple_buses_in_a_complex_scenario()
		{
			var rnd = new Random();

			// Start up two chatty buses talking to each other
			using (var messagebus1 = new TinyMessageBus("Example"))
			using (var messagebus2 = new TinyMessageBus("Example"))
			{
				var buses = new[] { messagebus1, messagebus2 };

				for (int i = 0; i < 512; i++)
				{
					buses[rnd.Next() % buses.Length].PublishAsync(Guid.NewGuid().ToByteArray());
				}

				// Wait for all messages published so far to be processed so the counters will be predictable
				messagebus1.WaitAll();
				messagebus2.WaitAll();

				// Add a new bus to the mix
				using (var messagebus3 = new TinyMessageBus("Example"))
				{
					buses = new[] { messagebus1, messagebus2, messagebus3 };

					for (int i = 0; i < 512; i++)
					{
						buses[rnd.Next() % buses.Length].PublishAsync(Guid.NewGuid().ToByteArray());
					}

					// Force a final read of all messages to work around timing issuees
					messagebus1.ProcessIncomingMessages();
					messagebus2.ProcessIncomingMessages();
					messagebus3.ProcessIncomingMessages();

					// Counters should check out
					Assert.Equal(1024 - messagebus1.MessagesSent, messagebus1.MessagesReceived);
					Assert.Equal(1024 - messagebus2.MessagesSent, messagebus2.MessagesReceived);
					Assert.Equal(512 - messagebus3.MessagesSent, messagebus3.MessagesReceived);
				}
			}
		}

		[Fact]
		public void All_primitives_should_be_configurable()
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
			using (var messageBus = new TinyMessageBus(tinyMemoryMappedFile, disposeFile: true))
			{
				messageBus.PublishAsync(Encoding.UTF8.GetBytes("lorem"));
				messageBus.PublishAsync(Encoding.UTF8.GetBytes("ipsum"));
			}
		}
	}
}
