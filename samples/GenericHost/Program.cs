using GenericHost;

var host = Host.CreateDefaultBuilder(args)
	.ConfigureLogging(builder =>
	{
		// Some self promotion for TinyLogger
		builder.AddTinyConsoleLogger();
	})
	.ConfigureServices(services =>
	{
		// Add a ITinyMessageBus with default options
		services.AddTinyMessageBus();

		// Or add the same service but configure options
		//services.AddTinyMessageBus(options =>
		//{
		//	options.Name = "445059aa-6d9d-431e-98e2-5b454cc2ccb8";
		//});

		services.AddTransient<LoremIpsum>();
		services.AddHostedService<ReceiverWorker>();
		services.AddHostedService<PublishWorker>();

		// Demo of advanced usage

		// You can also add a ITinyMemoryMappedFile if that is of use to you
		//services.AddTinyMemoryMappedFile();

		// Or add a ITinyReadWriteLock
		//services.AddTinyReadWriteLock();

		// You can also add a ITinyIpcFactory which can create multiple instances
		// with the same options using .CreateInstance()
		//services.AddTinyIpcFactory(options =>
		//{
		//	options.Name = "445059aa-6d9d-431e-98e2-5b454cc2ccb8";
		//});

		// You can also add a named configurations to the factory that can be created
		// with .CreateInstance("somename")
		//services.AddTinyIpcFactory("somename", options =>
		//{
		//	options.Name = "a08faa94-192d-4f72-a9d2-6770f3e44368";
		//});
	})
	.Build();

Console.WriteLine("Run multiple instances of this program to see IPC in action");
Console.WriteLine("Press Ctrl+C key to exit");
Console.WriteLine();

await host.RunAsync();
