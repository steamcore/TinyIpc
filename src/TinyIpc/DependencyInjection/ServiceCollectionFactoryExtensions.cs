using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyIpc;
using TinyIpc.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionFactoryExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Add a <see cref="ITinyIpcFactory"/> with default options.
		/// </summary>
		/// <remarks>
		/// Use <see cref="ITinyIpcFactory.CreateInstance()"/> to create instances.
		/// </remarks>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddTinyIpcFactory()
		{
			return AddTinyIpcFactory(services, _ => { });
		}

		/// <summary>
		/// Add a <see cref="ITinyIpcFactory"/> and configure default options.
		/// </summary>
		/// <remarks>
		/// Use <see cref="ITinyIpcFactory.CreateInstance()"/> to create instances.
		/// </remarks>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddTinyIpcFactory(Action<TinyIpcOptions> configure)
		{
			services.AddOptions<TinyIpcOptions>()
				.Configure(configure);

			services.TryAddSingleton<ITinyIpcFactory, TinyIpcFactory>();

			return services;
		}

		/// <summary>
		/// Add a <see cref="ITinyIpcFactory"/> and configure named options.
		/// </summary>
		/// <remarks>
		/// Use <see cref="ITinyIpcFactory.CreateInstance(string)"/> to create instances with the name.
		/// </remarks>
		/// <param name="name">Name of this configuration.</param>
		[SupportedOSPlatform("windows")]
		public IServiceCollection AddTinyIpcFactory(string name, Action<TinyIpcOptions> configure)
		{
			services.AddOptions<TinyIpcOptions>(name)
				.Configure(options => options.Name = $"{options.Name}:{name}")
				.Configure(configure);

			services.TryAddSingleton<ITinyIpcFactory, TinyIpcFactory>();

			return services;
		}
	}
}
