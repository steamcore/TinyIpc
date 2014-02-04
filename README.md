# TinyIpc #

.NET inter process broadcast message bus and supporting classes, built with .NET 4 client profile, depends on protobuf-net.

Intend for quick broadcast messaging in desktop applications, it just works.

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
	messagebus.MessageReceived +=
		(sender, e) => Console.WriteLine(Encoding.UTF8.GetString(e.Message));

	while (true)
	{
		var message = Console.ReadLine();
		messagebus.PublishAsync(Encoding.UTF8.GetBytes(message));
	}
}
```
