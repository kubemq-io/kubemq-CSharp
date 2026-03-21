# Request-Reply with Commands in KubeMQ .NET SDK

In this tutorial, you'll build a command-and-response system using KubeMQ's `KubeMQClient`. Commands are different from events and queues — the sender *blocks* until the handler responds, giving you synchronous confirmation that an action was executed.

## What You'll Build

A device-control system where a controller sends commands to restart services and the handler confirms execution. This pattern is ideal for operations where you need to know the outcome before proceeding.

## Prerequisites

- **.NET 8+** installed (`dotnet --version`)
- **KubeMQ server** running on `localhost:50000` ([quickstart guide](https://docs.kubemq.io/getting-started/quick-start))

Create a new console project and add the SDK:

```bash
dotnet new console -n DeviceController
cd DeviceController
dotnet add package KubeMQ.Sdk
```

## Step 1 — Connect to KubeMQ

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "device-controller",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = "devices.commands";
```

## Step 2 — Register the Command Handler

The handler subscribes to a channel and processes incoming commands. `SubscribeToCommandsAsync` returns an `IAsyncEnumerable<CommandMessageReceived>` — the idiomatic C# way to consume a stream. Every command must receive a response before the sender's timeout expires.

```csharp
var cts = new CancellationTokenSource();

var handlerTask = Task.Run(async () =>
{
    await foreach (var cmd in client.SubscribeToCommandsAsync(
        new CommandsSubscription { Channel = channel }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(cmd.Body.Span);
        Console.WriteLine($"\n[Handler] Received command: {body}");

        var success = ExecuteCommand(body);

        await client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = cmd.RequestId,
            ReplyChannel = cmd.ReplyChannel!,
            Executed = success,
            Error = success ? null : $"Unknown command: {body}",
        });

        Console.WriteLine($"  [Handler] Response sent: executed={success}");
    }
});
```

The `RequestId` and `ReplyChannel` link the response back to the correct sender — KubeMQ uses this correlation to route replies. Without them, the sender would time out waiting.

## Step 3 — Send Commands and Await Responses

Each command includes a `TimeoutInSeconds` — if the handler doesn't respond within that window, the sender gets a `KubeMQTimeoutException`. This prevents your system from hanging indefinitely.

```csharp
await Task.Delay(1000);

string[] commands = { "restart-web-server", "clear-cache", "UNKNOWN_ACTION" };

foreach (var action in commands)
{
    Console.WriteLine($"\n[Controller] Sending command: {action}");

    try
    {
        var response = await client.SendCommandAsync(new CommandMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes(action),
            TimeoutInSeconds = 10,
            Tags = new Dictionary<string, string>
            {
                ["action"] = action,
                ["operator"] = "admin"
            }
        });

        if (response.Executed)
        {
            Console.WriteLine("[Controller] Command executed successfully");
        }
        else
        {
            Console.WriteLine($"[Controller] Command failed: {response.Error}");
        }
    }
    catch (KubeMQ.Sdk.Exceptions.KubeMQTimeoutException)
    {
        Console.WriteLine("[Controller] Command timed out — no handler responded");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Controller] Error: {ex.Message}");
    }
}
```

We deliberately include `UNKNOWN_ACTION` to show how the handler can reject commands it doesn't understand. The sender sees the failure immediately through the response — no exception, just `Executed = false`.

## Step 4 — Clean Up

```csharp
await Task.Delay(1000);
cts.Cancel();

Console.WriteLine("\nDevice controller shut down.");

bool ExecuteCommand(string command)
{
    Thread.Sleep(100);
    return command switch
    {
        "restart-web-server" => true,
        "clear-cache" => true,
        _ => false
    };
}
```

## Complete Program

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "device-controller",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = "devices.commands";
var cts = new CancellationTokenSource();

var handlerTask = Task.Run(async () =>
{
    await foreach (var cmd in client.SubscribeToCommandsAsync(
        new CommandsSubscription { Channel = channel }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(cmd.Body.Span);
        Console.WriteLine($"\n[Handler] Received command: {body}");

        var success = ExecuteCommand(body);

        await client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = cmd.RequestId,
            ReplyChannel = cmd.ReplyChannel!,
            Executed = success,
            Error = success ? null : $"Unknown command: {body}",
        });

        Console.WriteLine($"  [Handler] Response sent: executed={success}");
    }
});

await Task.Delay(1000);

string[] commands = { "restart-web-server", "clear-cache", "UNKNOWN_ACTION" };

foreach (var action in commands)
{
    Console.WriteLine($"\n[Controller] Sending command: {action}");

    try
    {
        var response = await client.SendCommandAsync(new CommandMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes(action),
            TimeoutInSeconds = 10,
            Tags = new Dictionary<string, string>
            {
                ["action"] = action,
                ["operator"] = "admin"
            }
        });

        if (response.Executed)
        {
            Console.WriteLine("[Controller] Command executed successfully");
        }
        else
        {
            Console.WriteLine($"[Controller] Command failed: {response.Error}");
        }
    }
    catch (KubeMQ.Sdk.Exceptions.KubeMQTimeoutException)
    {
        Console.WriteLine("[Controller] Command timed out — no handler responded");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Controller] Error: {ex.Message}");
    }
}

await Task.Delay(1000);
cts.Cancel();

Console.WriteLine("\nDevice controller shut down.");

bool ExecuteCommand(string command)
{
    Thread.Sleep(100);
    return command switch
    {
        "restart-web-server" => true,
        "clear-cache" => true,
        _ => false
    };
}
```

## Expected Output

```
Connected to KubeMQ server

[Controller] Sending command: restart-web-server

[Handler] Received command: restart-web-server
  [Handler] Response sent: executed=True
[Controller] Command executed successfully

[Controller] Sending command: clear-cache

[Handler] Received command: clear-cache
  [Handler] Response sent: executed=True
[Controller] Command executed successfully

[Controller] Sending command: UNKNOWN_ACTION

[Handler] Received command: UNKNOWN_ACTION
  [Handler] Response sent: executed=False
[Controller] Command failed: Unknown command: UNKNOWN_ACTION

Device controller shut down.
```

## Error Handling

| Error | Cause | Fix |
|-------|-------|-----|
| `KubeMQTimeoutException` | No handler responded in time | Increase `TimeoutInSeconds` or verify handler is running |
| `KubeMQOperationException` | Server-side error during command delivery | Check server logs; verify channel exists |
| `RpcException` | Network interruption | Reconnect with `ConnectAsync()` |

The most critical rule: **always send a response from the handler**. If your handler throws an exception without responding, the sender blocks until timeout. Wrap your handler logic defensively:

```csharp
await foreach (var cmd in client.SubscribeToCommandsAsync(subscription, cts.Token))
{
    try
    {
        var result = await ProcessCommandAsync(cmd);
        await client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = cmd.RequestId,
            ReplyChannel = cmd.ReplyChannel!,
            Executed = result,
        });
    }
    catch (Exception ex)
    {
        await client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = cmd.RequestId,
            ReplyChannel = cmd.ReplyChannel!,
            Executed = false,
            Error = ex.Message,
        });
    }
}
```

## Next Steps

- **[Getting Started with Events](getting-started-events.md)** — fire-and-forget real-time messaging
- **[Building a Task Queue](building-a-task-queue.md)** — guaranteed delivery with acknowledgment
- **Queries** — like commands, but the response carries a data payload
- **Consumer Groups** — load-balance commands across multiple handlers
