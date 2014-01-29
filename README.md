# TinyIpc #

Simple .NET inter process message bus with supporting classes.

Intend for desktop applications, built with .NET client profile, depends on protobuf-net.

## Simple example ##

	using (var messagebus = new TinyMessageBus("ExampleChannel"))
	{
		messagebus.MessageReceived += (sender, received) => Console.WriteLine(received.Message);

		while (true)
		{
			var message = Console.ReadLine();
			messagebus.Publish(message);
		}
	}
  