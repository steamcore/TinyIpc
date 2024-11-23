using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Options;
using TinyIpc.IO;
using TinyIpc.Messaging;

namespace TinyIpc.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "BenchmarkDotNet")]
public class Benchmark : IDisposable
{
	private static readonly IOptions<TinyIpcOptions> options = new OptionsWrapper<TinyIpcOptions>(new TinyIpcOptions { MinMessageAge = TimeSpan.Zero });

	private readonly BinaryData message = BinaryData.FromString("Lorem ipsum dolor sit amet.");
	private readonly TinyMessageBus messagebusWithRealFile = new("benchmark", options);
	private readonly TinyMessageBus messagebusWithFakeFile = new(new FakeMemoryMappedFile(100_000), disposeFile: true, options);

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

internal sealed class FakeMemoryMappedFile(int maxFileSize)
	: ITinyMemoryMappedFile, IDisposable
{
	private readonly MemoryStream memoryStream = new(maxFileSize);
	private readonly MemoryStream writeStream = new(maxFileSize);

	public long MaxFileSize { get; } = maxFileSize;

	public event EventHandler? FileUpdated;

	public void Dispose()
	{
		memoryStream.Dispose();
		writeStream.Dispose();
	}

	public int GetFileSize(CancellationToken cancellationToken = default)
	{
		return (int)memoryStream.Length;
	}

	public T Read<T>(Func<MemoryStream, T> readData, CancellationToken cancellationToken = default)
	{
		memoryStream.Seek(0, SeekOrigin.Begin);

		return readData(memoryStream);
	}

	public void ReadWrite(Action<MemoryStream, MemoryStream> updateFunc, CancellationToken cancellationToken = default)
	{
		memoryStream.Seek(0, SeekOrigin.Begin);
		writeStream.SetLength(0);

		updateFunc(memoryStream, writeStream);

		memoryStream.SetLength(0);
		writeStream.Seek(0, SeekOrigin.Begin);

		writeStream.CopyTo(memoryStream);

		FileUpdated?.Invoke(this, EventArgs.Empty);
	}

	public void Write(MemoryStream data, CancellationToken cancellationToken = default)
	{
		memoryStream.SetLength(0);

		data.CopyTo(memoryStream);

		FileUpdated?.Invoke(this, EventArgs.Empty);
	}
}
