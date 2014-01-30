# TinyIpc #

.NET inter process broadcast message bus and supporting classes.

Intend for low to medium throughput messaging in desktop applications, built with .NET client profile, depends on protobuf-net.

Not intended for high throughput systems, guaranteed delivery or keeping a persistent message log.

## Compared to other solutions ##

<table>
	<tr>
		<th></th>
		<th>TinyIPC</th>
		<th>XDMessaging</th>
		<th>NVents</th>
		<th>IpcChannel</th>
		<th>Named Pipes</th>
	</tr>
	<tr>
		<td>Broadcasting to all listeners</td>
		<td>&#x2713;</td>
		<td>&#x2713;</td>
		<td>&#x2713; (1)</td>
		<td>&#x2717;</td>
		<td>&#x2717;</td>
	</tr>
	<tr>
		<td>No server master process</td>
		<td>&#x2713;</td>
		<td>&#x2713;</td>
		<td>&#x2713;</td>
		<td>&#x2717;</td>
		<td>&#x2717;</td>
	</tr>
	<tr>
		<td>Insensitive to process privilege level</td>
		<td>&#x2713;</td>
		<td>&#x2717;</td>
		<td>&#x2717;</td>
		<td>&#x2713;</td>
		<td>&#x2713;</td>
	</tr>
	<tr>
		<td>Entirely in memory</td>
		<td>&#x2713;</td>
		<td>&#x2717;</td>
		<td>&#x2713;</td>
		<td>&#x2713;</td>
		<td>&#x2713;</td>
	</tr>
</table>

1 Via SSDP network discovery

## Simple example ##

```csharp
using (var messagebus = new TinyMessageBus("ExampleChannel"))
{
	messagebus.MessageReceived += (sender, received) => Console.WriteLine(received.Message);

	while (true)
	{
		var message = Console.ReadLine();
		messagebus.Publish(message);
	}
}
```
