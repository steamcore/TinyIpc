using System.Text;
using BenchmarkDotNet.Attributes;
using TinyIpc.IO;
using TinyIpc.Messaging;

namespace TinyIpc.Benchmarks;

[MemoryDiagnoser]
public class Benchmark : IDisposable
{
	private readonly byte[] message = Encoding.UTF8.GetBytes("Lorem ipsum dolor sit amet.");
	private readonly TinyMessageBus messagebusWithRealFile = new TinyMessageBus("benchmark", TimeSpan.Zero);
	private readonly TinyMessageBus messagebusWithFakeFile = new TinyMessageBus(new FakeMemoryMappedFile(100_000), true, TimeSpan.Zero);

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			messagebusWithRealFile.Dispose();
			messagebusWithFakeFile.Dispose();
		}
	}

	[Benchmark]
	public Task PublishRealFile()
	{
		return messagebusWithRealFile.PublishAsync(message);
	}

	[Benchmark]
	public Task PublishFakeFile()
	{
		return messagebusWithFakeFile.PublishAsync(message);
	}
}

internal sealed class FakeMemoryMappedFile : ITinyMemoryMappedFile, IDisposable
{
	private readonly MemoryStream memoryStream;
	private readonly MemoryStream writeStream;

	public long MaxFileSize { get; }

	public event EventHandler? FileUpdated;

	public FakeMemoryMappedFile(int maxFileSize)
	{
		MaxFileSize = maxFileSize;

		memoryStream = new MemoryStream(maxFileSize);
		writeStream = new MemoryStream(maxFileSize);
	}

	public void Dispose()
	{
		memoryStream.Dispose();
		writeStream.Dispose();
	}

	public int GetFileSize()
	{
		return (int)memoryStream.Length;
	}

	public byte[] Read()
	{
		return memoryStream.ToArray();
	}

	public void ReadWrite(Func<byte[], byte[]> updateFunc)
	{
		Write(updateFunc(Read()));

		FileUpdated?.Invoke(this, EventArgs.Empty);
	}

	public void Write(byte[] data)
	{
		memoryStream.SetLength(0);
		memoryStream.Write(data, 0, data.Length);

		FileUpdated?.Invoke(this, EventArgs.Empty);
	}
}
