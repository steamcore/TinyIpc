using System;

namespace TinyIpc.Messaging
{
	public class TinyMessageReceivedEventArgs : EventArgs
	{
		public byte[] Message { get; }

		public TinyMessageReceivedEventArgs(byte[] message)
		{
			Message = message;
		}
	}
}
