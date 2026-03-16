# Getting Started with Events in KubeMQ .NET SDK

In this tutorial, you'll build a real-time event publisher and subscriber using KubeMQ's `KubeMQClient`. By the end, you'll understand how fire-and-forget messaging works in C# and when to choose events over queues.

## What You'll Build

A live notification system where a publisher sends user-signup events and a subscriber processes them in real time. Events are ephemeral — subscribers only receive messages while they're connected.

## Prerequisites

- **.NET 8+** installed (`dotnet --version`)
- **KubeMQ server** running on `localhost:50000` ([quickstart guide](https://docs.kubemq.io/getting-started/quick-start))

Create a new console project and add the SDK:

```bash
dotnet new console -n NotificationSystem
cd NotificationSystem
dotnet add package KubeMQ.Sdk
```

## Step 1 — Connect to the KubeMQ Server

The `KubeMQClient` is the unified entry point for all messaging patterns in the .NET SDK. It implements `IAsyncDisposable`, so `await using` ensures the gRPC connection is properly torn down.

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "notification-service",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");
```

The `ConnectAsync()` call establishes the gRPC channel. If the server is unreachable, you'll get an immediate exception rather than a silent failure downstream.

## Step 2 — Subscribe to Events

Subscribers must connect *before* publishers send, because events are not persisted. The `SubscribeToEventsAsync` method returns an `IAsyncEnumerable` — the idiomatic C# way to consume a stream of messages.

```csharp
var channel = "user.signups";
var cts = new CancellationTokenSource();

var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = channel }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(msg.Body.Span);
        Console.WriteLine($"[Subscriber] New signup: {body}");

        if (msg.Tags.Count > 0)
        {
            foreach (var tag in msg.Tags)
            {
                Console.WriteLine($"  {tag.Key}: {tag.Value}");
            }
        }
    }
});
```

We launch the subscription on a background `Task` because `await foreach` blocks until the token is cancelled. This lets the main thread continue to publish events.

## Step 3 — Publish Events

With the subscriber listening, we can send events. Each event carries a byte array body, optional metadata string, and key-value tags.

```csharp
await Task.Delay(1000);

string[] newUsers = { "alice@example.com", "bob@example.com", "carol@example.com" };

foreach (var user in newUsers)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes(user),
        Tags = new Dictionary<string, string>
        {
            ["source"] = "registration-api",
            ["priority"] = "normal"
        }
    });
    Console.WriteLine($"[Publisher] Sent signup event for: {user}");
}
```

The `Task.Delay(1000)` gives the subscription time to register on the server. In production, your publisher and subscriber would typically be separate processes, so this delay is unnecessary.

## Step 4 — Shut Down Gracefully

```csharp
await Task.Delay(2000);
cts.Cancel();

Console.WriteLine("\nNotification system shut down.");
```

Cancelling the `CancellationTokenSource` breaks the `await foreach` loop, which cleanly unsubscribes from the channel.

## Complete Program

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "notification-service",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = "user.signups";
var cts = new CancellationTokenSource();

var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = channel }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(msg.Body.Span);
        Console.WriteLine($"[Subscriber] New signup: {body}");

        if (msg.Tags.Count > 0)
        {
            foreach (var tag in msg.Tags)
            {
                Console.WriteLine($"  {tag.Key}: {tag.Value}");
            }
        }
    }
});

await Task.Delay(1000);

string[] newUsers = { "alice@example.com", "bob@example.com", "carol@example.com" };

foreach (var user in newUsers)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes(user),
        Tags = new Dictionary<string, string>
        {
            ["source"] = "registration-api",
            ["priority"] = "normal"
        }
    });
    Console.WriteLine($"[Publisher] Sent signup event for: {user}");
}

await Task.Delay(2000);
cts.Cancel();

Console.WriteLine("\nNotification system shut down.");
```

## Expected Output

```
Connected to KubeMQ server
[Publisher] Sent signup event for: alice@example.com
[Publisher] Sent signup event for: bob@example.com
[Publisher] Sent signup event for: carol@example.com
[Subscriber] New signup: alice@example.com
  source: registration-api
  priority: normal
[Subscriber] New signup: bob@example.com
  source: registration-api
  priority: normal
[Subscriber] New signup: carol@example.com
  source: registration-api
  priority: normal

Notification system shut down.
```

## Error Handling

Common issues and how to handle them:

| Error | Cause | Fix |
|-------|-------|-----|
| `Connection refused` | KubeMQ server not running | Start the server: `docker run -p 50000:50000 kubemq/kubemq` |
| `Subscriber missed events` | Subscriber connected after publisher sent | Always subscribe before publishing |
| `RpcException` | Network interruption | Catch and reconnect with `ConnectAsync()` |

For production, wrap your subscriber in a resilient loop:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    try
    {
        await foreach (var msg in client.SubscribeToEventsAsync(subscription, stoppingToken))
        {
            ProcessEvent(msg);
        }
    }
    catch (RpcException) when (!stoppingToken.IsCancellationRequested)
    {
        Console.WriteLine("Connection lost, reconnecting in 3s...");
        await Task.Delay(3000, stoppingToken);
        await client.ConnectAsync();
    }
}
```

## Next Steps

- **[Building a Task Queue](building-a-task-queue.md)** — guaranteed delivery with acknowledgment
- **[Request-Reply with Commands](request-reply-with-commands.md)** — synchronous command execution
- **[Getting Started with Events Store](getting-started-events-store.md)** — persistent events with replay from any point in time
- **Consumer Groups** — load-balance events across multiple subscribers
