# TinyIpc #

[![NuGet](https://img.shields.io/nuget/v/TinyIpc.svg?maxAge=259200)](https://www.nuget.org/packages/TinyIpc/)

.NET inter process broadcast message bus.

Intended for quick broadcast messaging in desktop applications, it just works.

## Quick introduction ##

* Designed to be serverless
* Clients may drop in and out at any time
* Messages expire after a specified timeout, default 500 milliseconds
* The log is kept small for performance, default max log size is 1 MB
* Writes are queued until there is enough space in the log

## Benefits and drawbacks ##

It's easy to use and there is no complicated setup. It is suited for small messages, so big messages probably need some other transport mechanism. With high enough troughput messages may be lost if receivers are not able to get a read lock before the message timeout is reached. However, hundreds or even a few thousand small messages a second should be fine.

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
		<td>No master process</td>
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

One message bus listening to the other.

```csharp
using (var messagebus1 = new TinyMessageBus("ExampleChannel"))
using (var messagebus2 = new TinyMessageBus("ExampleChannel"))
{
	messagebus2.MessageReceived +=
		(sender, e) => Console.WriteLine(Encoding.UTF8.GetString(e.Message));

	while (true)
	{
		var message = Console.ReadLine();
		messagebus1.PublishAsync(Encoding.UTF8.GetBytes(message));
	}
}
```
