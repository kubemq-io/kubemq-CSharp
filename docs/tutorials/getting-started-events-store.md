# Getting Started with Events Store in KubeMQ .NET SDK

In this tutorial, you'll build a persistent event publisher and subscriber using KubeMQ's Events Store. Unlike ephemeral events, Events Store messages are persisted by the server and can be replayed from any point in time — ideal for audit trails, event sourcing, or catching up after downtime.

## What You'll Build

A sensor-logging system where a publisher writes temperature readings to a persistent channel, and a subscriber replays all stored events from the beginning. Late-joining subscribers receive the full history before receiving new events.

## Prerequisites

- **.NET 8+** installed (`dotnet --version`)
- **KubeMQ server** running on `localhost:50000` ([quickstart guide](https://docs.kubemq.io/getting-started/quick-start))

Create a new console project and add the SDK:

```bash
dotnet new console -n SensorLogger
cd SensorLogger
dotnet add package KubeMQ.Sdk
```

## Step 1 — Connect to the KubeMQ Server

The `KubeMQClient` is the unified entry point for all messaging patterns in the .NET SDK. It implements `IAsyncDisposable`, so `await using` ensures the gRPC connection is properly torn down.

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "sensor-logger",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");
```

## Step 2 — Publish Persistent Events

Use `SendEventStoreAsync` to send events that are stored by the server. Each event carries a byte array body and optional metadata. Unlike regular events, these persist until the server's retention policy removes them.

```csharp
var channel = "sensors.temperature";

for (var i = 1; i <= 5; i++)
{
    var reading = $"{{ \"sensor\": \"temp-01\", \"value\": {20 + i * 2}, \"unit\": \"celsius\" }}";
    await client.SendEventStoreAsync(new EventStoreMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes(reading)
    });
    Console.WriteLine($"[Publisher] Stored reading #{i}");
}
```

You can publish before any subscriber connects — the server stores the events and delivers them when a subscriber joins.

## Step 3 — Subscribe with Replay

Subscribers use `SubscribeToEventsStoreAsync`, which returns an `IAsyncEnumerable`. The key difference from regular events is `EventStoreSubscription.StartPosition`: set it to `EventStoreStartPosition.StartFromFirst` to replay all stored events from the beginning.

```csharp
var cts = new CancellationTokenSource();

var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsStoreAsync(
        new EventStoreSubscription
        {
            Channel = channel,
            StartPosition = EventStoreStartPosition.StartFromFirst
        }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(msg.Body.Span);
        Console.WriteLine($"[Subscriber] [Seq={msg.Sequence}] {body}");
    }
});
```

We launch the subscription on a background `Task` because `await foreach` blocks until the token is cancelled. Each `EventStoreReceived` includes a `Sequence` number, useful for idempotency or checkpointing.

## Step 4 — Shut Down Gracefully

```csharp
await Task.Delay(3000);
cts.Cancel();

Console.WriteLine("\nSensor logger shut down.");
```

Cancelling the `CancellationTokenSource` breaks the `await foreach` loop and cleanly unsubscribes from the channel.

## Complete Program

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "sensor-logger",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = "sensors.temperature";

for (var i = 1; i <= 5; i++)
{
    var reading = $"{{ \"sensor\": \"temp-01\", \"value\": {20 + i * 2}, \"unit\": \"celsius\" }}";
    await client.SendEventStoreAsync(new EventStoreMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes(reading)
    });
    Console.WriteLine($"[Publisher] Stored reading #{i}");
}

var cts = new CancellationTokenSource();

var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsStoreAsync(
        new EventStoreSubscription
        {
            Channel = channel,
            StartPosition = EventStoreStartPosition.StartFromFirst
        }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(msg.Body.Span);
        Console.WriteLine($"[Subscriber] [Seq={msg.Sequence}] {body}");
    }
});

await Task.Delay(3000);
cts.Cancel();

Console.WriteLine("\nSensor logger shut down.");
```

## Expected Output

```
Connected to KubeMQ server
[Publisher] Stored reading #1
[Publisher] Stored reading #2
[Publisher] Stored reading #3
[Publisher] Stored reading #4
[Publisher] Stored reading #5
[Subscriber] [Seq=<seq>] { "sensor": "temp-01", "value": 22, "unit": "celsius" }
[Subscriber] [Seq=<seq>] { "sensor": "temp-01", "value": 24, "unit": "celsius" }
[Subscriber] [Seq=<seq>] { "sensor": "temp-01", "value": 26, "unit": "celsius" }
[Subscriber] [Seq=<seq>] { "sensor": "temp-01", "value": 28, "unit": "celsius" }
[Subscriber] [Seq=<seq>] { "sensor": "temp-01", "value": 30, "unit": "celsius" }

Sensor logger shut down.
```

## Replay Options

`EventStoreStartPosition` controls where the subscription begins:

| Position | Use Case |
|----------|----------|
| `StartFromFirst` | Replay all stored events from the beginning |
| `StartFromLast` | Start from the most recent event only |
| `StartFromNew` | Receive only events published after subscribing (default) |
| `StartAtSequence` | Start from a specific sequence (set `StartSequence`) |
| `StartAtTime` | Start from a point in time (set `StartTime`) |
| `StartAtTimeDelta` | Start from N seconds ago (set `StartTimeDeltaSeconds`) |

## Error Handling

Common issues and how to handle them:

| Error | Cause | Fix |
|-------|-------|-----|
| `Connection refused` | KubeMQ server not running | Start the server: `docker run -p 50000:50000 kubemq/kubemq` |
| `KubeMQConfigurationException` | Invalid channel or start position | Ensure channel has no wildcards; set required fields for `StartAtSequence`, `StartAtTime`, or `StartAtTimeDelta` |
| `RpcException` | Network interruption | Catch and reconnect with `ConnectAsync()` |

For production, wrap your subscriber in a resilient loop:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    try
    {
        await foreach (var msg in client.SubscribeToEventsStoreAsync(subscription, stoppingToken))
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

- **[Getting Started with Events](getting-started-events.md)** — ephemeral fire-and-forget messaging
- **[Use Consumer Groups](../how-to/use-consumer-groups.md)** — load-balance Events Store across multiple subscribers
- **[Building a Task Queue](building-a-task-queue.md)** — guaranteed delivery with acknowledgment
