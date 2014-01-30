using System;

namespace TinyIpc.Example
{
	public class Program
	{
		public static void Main(string[] args)
		{
			using (var messagebus = new TinyMessageBus("Example"))
			{
				messagebus.MessageReceived += (sender, e) => Console.WriteLine(e.Message);

				while (true)
				{
					var message = Console.ReadLine();
					messagebus.PublishAsync(message);
				}
			}
		}
	}
}
