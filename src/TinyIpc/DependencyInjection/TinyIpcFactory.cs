#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TinyIpc.DependencyInjection;

public interface ITinyIpcFactory
{
	ITinyIpcInstance CreateInstance();
	ITinyIpcInstance CreateInstance(string name);
}

public sealed class TinyIpcFactory(IOptionsMonitor<TinyIpcOptions> optionsMonitor, ILoggerFactory loggerFactory) : ITinyIpcFactory
{
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public ITinyIpcInstance CreateInstance()
	{
		var instance = new TinyIpcInstance(
			new OptionsWrapper<TinyIpcOptions>(optionsMonitor.CurrentValue),
			loggerFactory
		);

		return instance;
	}

#if NET
	[SupportedOSPlatform("windows")]
#endif
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
