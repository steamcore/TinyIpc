using TinyIpc.Messaging;

// Normally there is one message bus per process, but here is two for demonstration purposes
using var messagebus1 = new TinyMessageBus("Example");
using var messagebus2 = new TinyMessageBus("Example");

Console.WriteLine("Type something and press enter. Ctrl+C to quit.");

messagebus1.MessageReceived +=
	(sender, e) => Console.WriteLine("Received: " + e.Message.ToString());

while (true)
{
	var message = Console.ReadLine();

	if (string.IsNullOrWhiteSpace(message))
	{
		return;
	}

	await messagebus2.PublishAsync(BinaryData.FromString(message));
}
