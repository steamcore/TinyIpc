#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyIpc.IO;
using TinyIpc.Messaging;
using TinyIpc.Synchronization;

namespace TinyIpc.DependencyInjection;

public interface ITinyIpcInstance : IDisposable
{
	ITinyReadWriteLock ReadWriteLock { get; }
	ITinyMemoryMappedFile MemoryMappedFile { get; }
	ITinyMessageBus MessageBus { get; }
}

public sealed class TinyIpcInstance : ITinyIpcInstance
{
	private readonly TinyReadWriteLock readWriteLock;
	private readonly TinyMemoryMappedFile memoryMappedFile;
	private readonly TinyMessageBus messageBus;

	public ITinyReadWriteLock ReadWriteLock => readWriteLock;
	public ITinyMemoryMappedFile MemoryMappedFile => memoryMappedFile;
	public ITinyMessageBus MessageBus => messageBus;

#if NET
	[SupportedOSPlatform("windows")]
#endif
	public TinyIpcInstance(IOptions<TinyIpcOptions> options, ILoggerFactory loggerFactory)
	{
		readWriteLock = new TinyReadWriteLock(options, loggerFactory.CreateLogger<TinyReadWriteLock>());
		memoryMappedFile = new TinyMemoryMappedFile(readWriteLock, options, loggerFactory.CreateLogger<TinyMemoryMappedFile>());
		messageBus = new TinyMessageBus(memoryMappedFile, options, loggerFactory.CreateLogger<TinyMessageBus>());
	}

	public void Dispose()
	{
		messageBus.Dispose();
		memoryMappedFile.Dispose();
		readWriteLock.Dispose();
	}
}
