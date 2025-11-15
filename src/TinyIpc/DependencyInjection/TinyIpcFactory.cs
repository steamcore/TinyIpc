using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TinyIpc.DependencyInjection;

public interface ITinyIpcFactory
{
	/// <summary>
	/// Create a set of locks, memory mapped file accessor and message bus.
	/// </summary>
	ITinyIpcInstance CreateInstance();

	/// <summary>
	/// Create a named set of locks, memory mapped file accessor and message bus.
	/// </summary>
	/// <param name="name">The name that was used in a call to AddTinyIpcFactory.</param>
	ITinyIpcInstance CreateInstance(string name);
}

public sealed class TinyIpcFactory(IOptionsMonitor<TinyIpcOptions> optionsMonitor, ILoggerFactory loggerFactory) : ITinyIpcFactory
{
	[SupportedOSPlatform("windows")]
	public ITinyIpcInstance CreateInstance()
	{
		var instance = new TinyIpcInstance(
			new OptionsWrapper<TinyIpcOptions>(optionsMonitor.CurrentValue),
			loggerFactory
		);

		return instance;
	}

	[SupportedOSPlatform("windows")]
	public ITinyIpcInstance CreateInstance(string name)
	{
		var options = optionsMonitor.Get(name);

		var instance = new TinyIpcInstance(
			new OptionsWrapper<TinyIpcOptions>(options),
			loggerFactory
		);

		return instance;
	}
}
