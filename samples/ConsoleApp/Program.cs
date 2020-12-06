using System;
using System.Text;
using System.Threading.Tasks;
using TinyIpc.Messaging;

namespace ConsoleApp
{
	public class Program
	{
		public static async Task Main()
		{
			using var messagebus1 = new TinyMessageBus("Example");
			using var messagebus2 = new TinyMessageBus("Example");

			Console.WriteLine("Type something and press enter. Ctrl+C to quit.");

			messagebus1.MessageReceived +=
				(sender, e) => Console.WriteLine("Received: " + Encoding.UTF8.GetString(e.Message));

			while (true)
			{
				var message = Console.ReadLine();

				if (string.IsNullOrWhiteSpace(message))
					return;

				await messagebus2.PublishAsync(Encoding.UTF8.GetBytes(message));
			}
		}
	}
}
