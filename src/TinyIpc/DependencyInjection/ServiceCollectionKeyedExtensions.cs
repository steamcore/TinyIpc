using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyIpc;
using TinyIpc.IO;
using TinyIpc.Messaging;
using TinyIpc.Synchronization;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionKeyedExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Add a keyed <see cref="ITinyReadWriteLock"/> with named options.
		/// </summary>
		/// <param name="serviceKey">Service key, this is alos used as a name for the options.</param>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddKeyedTinyReadWriteLock(string serviceKey)
		{
			services.AddKeyedTinyReadWriteLock(serviceKey, _ => { });

			return services;
		}

		/// <summary>
		/// Add a keyed <see cref="ITinyReadWriteLock"/> and configure named options.
		/// </summary>
		/// <param name="serviceKey">Service key, this is alos used as a name for the options.</param>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddKeyedTinyReadWriteLock(string serviceKey, Action<TinyIpcOptions> configure)
		{
			services.AddOptions<TinyIpcOptions>(serviceKey)
				.Configure(options => options.Name = $"{options.Name}:{serviceKey}")
				.Configure(configure);

			services.TryAddKeyedSingleton<ITinyReadWriteLock, TinyReadWriteLock>(serviceKey);

			return services;
		}

		/// <summary>
		/// <para>Add a keyed <see cref="ITinyMemoryMappedFile"/> with named options.</para>
		/// <para>This also adds a <see cref="ITinyReadWriteLock"/> which is used by the file.</para>
		/// </summary>
		/// <param name="serviceKey">Service key, this is alos used as a name for the options.</param>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddKeyedTinyMemoryMappedFile(string serviceKey)
		{
			services.AddKeyedTinyMemoryMappedFile(serviceKey, _ => { });

			return services;
		}

		/// <summary>
		/// <para>Add a keyed <see cref="ITinyMemoryMappedFile"/> and configure named options.</para>
		/// <para>This also adds a <see cref="ITinyReadWriteLock"/> which is used by the file.</para>
		/// </summary>
		/// <param name="serviceKey">Service key, this is alos used as a name for the options.</param>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddKeyedTinyMemoryMappedFile(string serviceKey, Action<TinyIpcOptions> configure)
		{
			services.AddKeyedTinyReadWriteLock(serviceKey, configure);

			services.TryAddKeyedSingleton<ITinyMemoryMappedFile, TinyMemoryMappedFile>(serviceKey);

			return services;
		}

		/// <summary>
		/// <para>Add a keyed <see cref="ITinyMessageBus"/> with named options.</para>
		/// <para>This also adds a <see cref="ITinyReadWriteLock"/> and a <see cref="ITinyReadWriteLock"/> which is used by the message bus.</para>
		/// </summary>
		/// <param name="serviceKey">Service key, this is alos used as a name for the options.</param>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddKeyedTinyMessageBus(string serviceKey)
		{
			services.AddKeyedTinyMessageBus(serviceKey, _ => { });

			return services;
		}

		/// <summary>
		/// <para>Add a keyed <see cref="ITinyMessageBus"/> and configure named options.</para>
		/// <para>This also adds a <see cref="ITinyReadWriteLock"/> and a <see cref="ITinyReadWriteLock"/> which is used by the message bus.</para>
		/// </summary>
		/// <param name="serviceKey">Service key, this is alos used as a name for the options.</param>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddKeyedTinyMessageBus(string serviceKey, Action<TinyIpcOptions> configure)
		{
			services.AddKeyedTinyMemoryMappedFile(serviceKey, configure);

			services.TryAddKeyedSingleton<ITinyMessageBus, TinyMessageBus>(serviceKey);

			return services;
		}
	}
}
