using System;
using System.Text;
using TinyIpc.Messaging;

namespace ConsoleApp
{
	public class Program
	{
		public static void Main(string[] args)
		{
			using (var messagebus1 = new TinyMessageBus("Example"))
			using (var messagebus2 = new TinyMessageBus("Example"))
			{
				messagebus1.MessageReceived +=
					(sender, e) => Console.WriteLine("Received: " + Encoding.UTF8.GetString(e.Message));

				while (true)
				{
					var message = Console.ReadLine();

					if (string.IsNullOrWhiteSpace(message))
						return;

					messagebus2.PublishAsync(Encoding.UTF8.GetBytes(message));
				}
			}
		}
	}
}
