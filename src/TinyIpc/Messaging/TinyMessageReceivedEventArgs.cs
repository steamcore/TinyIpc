namespace TinyIpc.Messaging;

public class TinyMessageReceivedEventArgs : EventArgs
{
	public IReadOnlyList<byte> Message { get; }

	public TinyMessageReceivedEventArgs(IReadOnlyList<byte> message)
	{
		Message = message;
	}


	private readonly TaskCompletionSource<object>? m_task_source;
	private readonly Guid? m_task_id;

	public TinyMessageReceivedEventArgs(IReadOnlyList<byte> message, Guid id, TaskCompletionSource<object> source)
		: this(message)
	{
		m_task_source = source;
		m_task_id = id;
	}

	public void TrySetResult(object value)
	{
		m_task_source?.TrySetResult(value);
		TinyMessageBus.ExpireTaskCompletionSource(m_task_id);
	}

	public void TrySetException(Exception exception)
	{
		m_task_source?.TrySetException(exception);
		TinyMessageBus.ExpireTaskCompletionSource(m_task_id);
	}

	public void TrySetCanceled()
	{
		m_task_source?.TrySetCanceled();
		TinyMessageBus.ExpireTaskCompletionSource(m_task_id);
	}
}
