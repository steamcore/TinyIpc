using System;

namespace TinyIpc
{
	public class TinyMessageReceivedEventArgs : EventArgs
	{
		public string Message { get; set; }
	}
}
