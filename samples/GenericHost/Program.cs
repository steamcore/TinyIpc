using GenericHost;

var host = Host.CreateDefaultBuilder(args)
	.ConfigureLogging(builder =>
	{
		// Some self promotion for TinyLogger
		builder.AddTinyConsoleLogger();
	})
	.ConfigureServices(services =>
	{
		// Add TinyIpc and give it a unique name
		services.AddTinyIpc(options =>
		{
			options.Name = "445059aa-6d9d-431e-98e2-5b454cc2ccb8";
		});

		// You can also add a named instance that can be created with CreateInstance("somename")
		services.AddTinyIpc("somename", options =>
		{
			options.Name = "a08faa94-192d-4f72-a9d2-6770f3e44368";
		});

		services.AddTransient<LoremIpsum>();
		services.AddHostedService<ReceiverWorker>();
		services.AddHostedService<PublishWorker>();
	})
	.Build();

await host.RunAsync();
