# TinyIpc #

[![NuGet](https://img.shields.io/nuget/v/TinyIpc.svg?maxAge=259200)](https://www.nuget.org/packages/TinyIpc/)
![Build](https://github.com/steamcore/TinyIpc/workflows/Build/badge.svg)

.NET inter process broadcast message bus.

Intended for quick broadcast messaging in Windows desktop applications, it just works.

## Quick introduction ##

* Designed to be serverless
* Clients may drop in and out at any time
* Messages expire after a specified timeout, default 500 milliseconds
* The log is kept small for performance, default max log size is 1 MB
* Reads are queued and should be received in the same order as they were published

## Benefits and drawbacks ##

It's easy to use and there is no complicated setup. It is suited for small messages,
so big messages probably need some other transport mechanism. With high enough
throughput messages may be lost if receivers are not able to get a read lock before
the message timeout is reached.

## Performance ##
Every publish operation reads and writes the entire contents of a shared memory
mapped file and every read operation which is triggered by writes also reads the
entire file so if performance is important then batch publish several messages
at once to reduce the amount of reads and writes.

## OS Support ##

Unfortunately TinyIpc only works on Windows because the named primitives that
are core to this entire solution only works on Windows and throws
PlatformNotSupportedException on other operating systems by design.

See https://github.com/dotnet/runtime/issues/4370 for more information.

## Compared to other solutions ##

*This comparison was made in 2014.*

|                                        | TinyIPC  | XDMessaging | NVents       | IpcChannel | Named Pipes |
|----------------------------------------|----------|-------------|--------------|------------|-------------|
| Broadcasting to all listeners          | &#x2713; | &#x2713;    | &#x2713; (1) | &#x2717;   | &#x2717;    |
| No master process                      | &#x2713; | &#x2713;    | &#x2713;     | &#x2717;   | &#x2717;    |
| Insensitive to process privilege level | &#x2713; | &#x2717;    | &#x2717;     | &#x2713;   | &#x2713;    |
| Entirely in memory                     | &#x2713; | &#x2717;    | &#x2713;     | &#x2713;   | &#x2713;    |

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
		await messagebus1.PublishAsync(Encoding.UTF8.GetBytes(message));
	}
}
```
