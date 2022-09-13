#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyIpc;
using TinyIpc.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyIpc(this IServiceCollection services)
	{
		return AddTinyIpc(services, _ => { });
	}

#if NET
		[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyIpc(this IServiceCollection services, Action<TinyIpcOptions> configure)
	{
		services.AddOptions<TinyIpcOptions>()
			.Configure(configure);

		services.TryAddSingleton<ITinyIpcFactory, TinyIpcFactory>();

		return services;
	}

#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyIpc(this IServiceCollection services, string name)
	{
		return AddTinyIpc(services, name, _ => { });
	}

#if NET
		[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyIpc(this IServiceCollection services, string name, Action<TinyIpcOptions> configure)
	{
		services.AddOptions<TinyIpcOptions>(name)
			.Configure(options => options.Name = $"{options.Name}:{name}")
			.Configure(configure);

		services.TryAddSingleton<ITinyIpcFactory, TinyIpcFactory>();

		return services;
	}
}
