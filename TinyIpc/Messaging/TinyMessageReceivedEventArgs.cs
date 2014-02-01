using System;

namespace TinyIpc.Messaging
{
	public class TinyMessageReceivedEventArgs : EventArgs
	{
		public string Message { get; set; }
	}
}
