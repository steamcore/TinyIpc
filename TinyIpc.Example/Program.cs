using System;
using System.Text;
using TinyIpc.Messaging;

namespace TinyIpc.Example
{
	public class Program
	{
		public static void Main(string[] args)
		{
			using (var messagebus = new TinyMessageBus("Example"))
			{
				messagebus.MessageReceived +=
					(sender, e) => Console.WriteLine(Encoding.UTF8.GetString(e.Message));

				while (true)
				{
					var message = Console.ReadLine();

					if (string.IsNullOrWhiteSpace(message))
						return;

					messagebus.PublishAsync(Encoding.UTF8.GetBytes(message));
				}
			}
		}
	}
}
