# TinyIpc

[![NuGet](https://img.shields.io/nuget/v/TinyIpc.svg?maxAge=259200)](https://www.nuget.org/packages/TinyIpc/)
![Build](https://github.com/steamcore/TinyIpc/workflows/Build/badge.svg)

**TinyIpc** is a lightweight, serverless .NET inter-process broadcast message bus designed
for simplicity and performance in Windows desktop applications.

## Table of Contents
- [Features](#features)
- [Benefits and Limitations](#benefits-and-limitations)
- [Performance](#performance)
- [OS Support](#os-support)
- [Feature Comparison](#feature-comparison)
- [Examples](#examples)
  - [Simple Example](#simple-example)
  - [Generic Hosting Example](#generic-hosting-example)

## Features

- **Serverless Architecture**: No master process required.
- **Flexible Messaging**: Clients can join or leave at any time.
- **Automatic Expiration**: Messages expire after a configurable timeout (default: 1 second).
- **Memory Efficient**: Default max log size is 1 MB for high performance.
- **FIFO Guarantee**: Messages are received in the order they are published.
- **Fully async**: Supports receiving events in callbacks or via async enumerable

## Benefits and Limitations

**Benefits**
- Easy to set up, no complex configurations required.
- Ideal for small, quick messages.
- Fully in-memory for high-speed communication.
- Can batch send many messages in one operation.
- Broadcast messages to all listeners (except the sender).

**Limitations**
- Windows only: Relies on named primitives unavailable on other platforms.
- Message size: Not suitable for large payloads.
- Timeout sensitivity: High throughput can lead to message loss if receivers fail to acquire locks in time.

## Performance

Every publish operation reads and writes the entire shared memory mapped file, and every
receive operation which is triggered after writes also reads the entire file.

Thus, if high throughput is desired, batch publish several messages at once to reduce
I/O operations.

## OS Support

TinyIpc currently supports Windows only due to reliance on platform-specific primitives.

For more details, refer to [this issue](https://github.com/dotnet/runtime/issues/4370).

## Feature Comparison

|                                             | TinyIPC  | IpcChannel | Named Pipes |
|---------------------------------------------|----------|------------|-------------|
| Broadcasting to all listeners (except self) | &#x2713; | &#x2717;   | &#x2717;    |
| Serverless architecture                     | &#x2713; | &#x2717;   | &#x2717;    |
| Process privilege agnostic                  | &#x2713; | &#x2713;   | &#x2713;    |
| Fully in-memory                             | &#x2713; | &#x2713;   | &#x2713;    |

## Examples

### Simple Example

Check [ConsoleApp](samples/ConsoleApp/) for a sample application.

```csharp
using var messagebus1 = new TinyMessageBus("ExampleChannel");
using var messagebus2 = new TinyMessageBus("ExampleChannel");

messagebus2.MessageReceived +=
	(sender, e) => Console.WriteLine(e.Message.ToString());

while (true)
{
	var message = Console.ReadLine();
	await messagebus1.PublishAsync(BinaryData.FromString(message));
}
```
### Generic Hosting Example

Check [GenericHost](samples/GenericHost/) for a sample application.

```csharp
// Add service to IServiceCollection
services.AddTinyIpc(options =>
{
	options.Name = "ExampleChannel";
});

// Later use ITinyIpcFactory to create instances
using var tinyIpcInstance1 = tinyIpcFactory.CreateInstance();
using var tinyIpcInstance2 = tinyIpcFactory.CreateInstance();

tinyIpcInstance2.MessageBus.MessageReceived +=
	(sender, e) => Console.WriteLine(e.Message.ToString());

while (true)
{
	var message = Console.ReadLine();
	await tinyIpcInstance1.MessageBus.PublishAsync(BinaryData.FromString(message));
}
```
