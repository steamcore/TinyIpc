using System;

namespace TinyIpc.Messaging
{
	public class TinyMessageReceivedEventArgs : EventArgs
	{
		public byte[] Message { get; set; }
	}
}
