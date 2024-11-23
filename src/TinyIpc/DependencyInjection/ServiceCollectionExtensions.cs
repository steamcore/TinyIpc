#if NET
using System.Runtime.Versioning;
#endif
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyIpc;
using TinyIpc.IO;
using TinyIpc.Messaging;
using TinyIpc.Synchronization;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
	/// <summary>
	/// Add a <see cref="ITinyReadWriteLock"/> with default options.
	/// </summary>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyReadWriteLock(this IServiceCollection services)
	{
		services.AddTinyReadWriteLock(_ => { });

		return services;
	}

	/// <summary>
	/// Add a <see cref="ITinyReadWriteLock"/> and configure default options.
	/// </summary>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyReadWriteLock(this IServiceCollection services, Action<TinyIpcOptions> configure)
	{
		services.AddOptions<TinyIpcOptions>()
			.Configure(configure);

		services.TryAddSingleton<ITinyReadWriteLock, TinyReadWriteLock>();

		return services;
	}

	/// <summary>
	/// <para>Add a <see cref="ITinyMemoryMappedFile"/> with default options.</para>
	/// <para>This also adds a <see cref="ITinyReadWriteLock"/> which is used by the file.</para>
	/// </summary>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyMemoryMappedFile(this IServiceCollection services)
	{
		services.AddTinyMemoryMappedFile(_ => { });

		return services;
	}

	/// <summary>
	/// <para>Add a <see cref="ITinyMemoryMappedFile"/> and configure default options.</para>
	/// <para>This also adds a <see cref="ITinyReadWriteLock"/> which is used by the file.</para>
	/// </summary>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyMemoryMappedFile(this IServiceCollection services, Action<TinyIpcOptions> configure)
	{
		services.AddTinyReadWriteLock(configure);

		services.TryAddSingleton<ITinyMemoryMappedFile, TinyMemoryMappedFile>();

		return services;
	}

	/// <summary>
	/// <para>Add a <see cref="ITinyMessageBus"/> with default options.</para>
	/// <para>This also adds a <see cref="ITinyReadWriteLock"/> and a <see cref="ITinyReadWriteLock"/> which is used by the message bus.</para>
	/// </summary>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyMessageBus(this IServiceCollection services)
	{
		services.AddTinyMessageBus(_ => { });

		return services;
	}

	/// <summary>
	/// <para>Add a <see cref="ITinyMessageBus"/> and configure default options.</para>
	/// <para>This also adds a <see cref="ITinyReadWriteLock"/> and a <see cref="ITinyReadWriteLock"/> which is used by the message bus.</para>
	/// </summary>
#if NET
	[SupportedOSPlatform("windows")]
#endif
	public static IServiceCollection AddTinyMessageBus(this IServiceCollection services, Action<TinyIpcOptions> configure)
	{
		services.AddTinyMemoryMappedFile(configure);

		services.TryAddSingleton<ITinyMessageBus, TinyMessageBus>();

		return services;
	}
}
