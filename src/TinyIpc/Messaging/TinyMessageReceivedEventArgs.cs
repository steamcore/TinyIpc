using System;
using System.Diagnostics.CodeAnalysis;

namespace TinyIpc.Messaging
{
	public class TinyMessageReceivedEventArgs : EventArgs
	{
		[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Byte array will do until possible rewrite using Span/Memory")]
		public byte[] Message { get; }

		public TinyMessageReceivedEventArgs(byte[] message)
		{
			Message = message;
		}
	}
}
