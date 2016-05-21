using NUnit.Framework;
using System;
using System.Text;
using TinyIpc.Messaging;

namespace TinyIpc.Tests
{
	[TestFixture]
	public class TinyMessageBusTests
	{
		[Test]
		public static void Messages_sent_from_one_bus_should_be_received_by_the_other()
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

				Assert.That(received, Is.EqualTo("yes"));
			}
		}

		[Test]
		public static void All_messages_should_be_processed_even_with_multiple_buses_in_a_complex_scenario()
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
					Assert.That(messagebus1.MessagesReceived, Is.EqualTo(1024 - messagebus1.MessagesSent));
					Assert.That(messagebus2.MessagesReceived, Is.EqualTo(1024 - messagebus2.MessagesSent));
					Assert.That(messagebus3.MessagesReceived, Is.EqualTo(512 - messagebus3.MessagesSent));
				}
			}
		}
	}
}
