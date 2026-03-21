# Implementing CQRS with KubeMQ

This guide demonstrates a Command Query Responsibility Segregation (CQRS) architecture using KubeMQ's native messaging primitives: commands for writes, queries for reads, and events for state synchronization.

## Architecture

```
┌────────┐  command   ┌──────────────┐  event   ┌──────────────┐
│ Client │───────────▶│ Write Service │────────▶│ Read Service │
│        │            │ (cmd handler) │          │ (projection) │
│        │◀───────────│              │          │              │
│        │  query     └──────────────┘          └──────────────┘
│        │────────────────────────────────────▶│              │
│        │◀───────────────────────────────────│              │
└────────┘                                     └──────────────┘
```

1. **Commands** carry write intent — "create order", "update status". The write service processes commands and emits domain events.
2. **Events** propagate state changes to read-side projections asynchronously.
3. **Queries** retrieve data from the read-optimized projection, independent of the write model.

## Prerequisites

- KubeMQ server on `localhost:50000`
- `dotnet add package KubeMQ.Sdk`

## Write Service — Command Handler

The write service subscribes to commands, validates them, applies business logic, and publishes domain events.

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Events;
using System.Collections.Concurrent;
using System.Text;

var store = new ConcurrentDictionary<string, string>();

async Task RunWriteService(KubeMQClient client, CancellationToken ct)
{
    await using var eventStream = await client.CreateEventStreamAsync(
        onError: ex => Console.WriteLine($"[Write] Event stream error: {ex.Message}"));

    await foreach (var cmd in client.SubscribeToCommandsAsync(
        new CommandsSubscription { Channel = "cqrs.commands" }, ct))
    {
        var body = Encoding.UTF8.GetString(cmd.Body.Span);
        var orderId = cmd.Tags?["order_id"] ?? "unknown";
        Console.WriteLine($"[Write] Command: {body}");

        store[orderId] = body;

        await client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = cmd.RequestId,
            ReplyChannel = cmd.ReplyChannel!,
            Executed = true,
        });

        await eventStream.SendAsync(new EventMessage
        {
            Channel = "cqrs.events",
            Body = cmd.Body.ToArray(),
            Metadata = orderId,
        }, "cqrs-write-svc");

        Console.WriteLine($"[Write] Order {orderId} persisted, event emitted");
    }
}
```

## Read Service — Query Handler with Event Projection

The read service maintains a denormalized projection updated by domain events, and serves queries against it.

```csharp
using KubeMQ.Sdk.Queries;
using System.Text.Json;

var projection = new ConcurrentDictionary<string, string>();

async Task RunEventProjection(KubeMQClient client, CancellationToken ct)
{
    await foreach (var ev in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "cqrs.events" }, ct))
    {
        var key = ev.Metadata ?? "";
        projection[key] = Encoding.UTF8.GetString(ev.Body.Span);
        Console.WriteLine($"[Read] Projection updated: key={key}");
    }
}

async Task RunQueryHandler(KubeMQClient client, CancellationToken ct)
{
    await foreach (var q in client.SubscribeToQueriesAsync(
        new QueriesSubscription { Channel = "cqrs.queries" }, ct))
    {
        var key = Encoding.UTF8.GetString(q.Body.Span);
        var found = projection.TryGetValue(key, out var data);

        var result = JsonSerializer.Serialize(new { found, data = data ?? "" });

        await client.SendQueryResponseAsync(new QueryResponse
        {
            RequestId = q.RequestId,
            ReplyChannel = q.ReplyChannel!,
            Executed = true,
            Body = Encoding.UTF8.GetBytes(result),
        });

        Console.WriteLine($"[Read] Query served: key={key} found={found}");
    }
}
```

## Client — Sending Commands and Queries

```csharp
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "cqrs-demo",
});
await client.ConnectAsync();

var cts = new CancellationTokenSource();

_ = Task.Run(() => RunWriteService(client, cts.Token));
_ = Task.Run(() => RunEventProjection(client, cts.Token));
_ = Task.Run(() => RunQueryHandler(client, cts.Token));
await Task.Delay(1000);

// Write via command
var cmdResp = await client.SendCommandAsync(new CommandMessage
{
    Channel = "cqrs.commands",
    Body = Encoding.UTF8.GetBytes("""{"item":"widget","qty":5}"""),
    Tags = new Dictionary<string, string> { ["order_id"] = "ORD-001" },
    TimeoutInSeconds = 10,
});
Console.WriteLine($"[Client] Command executed: {cmdResp.Executed}");

await Task.Delay(500);

// Read via query
var queryResp = await client.SendQueryAsync(new QueryMessage
{
    Channel = "cqrs.queries",
    Body = Encoding.UTF8.GetBytes("ORD-001"),
    TimeoutInSeconds = 10,
});
Console.WriteLine($"[Client] Query result: {Encoding.UTF8.GetString(queryResp.Body.Span)}");

cts.Cancel();
```

## Design Considerations

| Concern | Approach |
|---|---|
| **Consistency** | Eventually consistent — events propagate asynchronously to the read model |
| **Ordering** | Use events-store with sequence replay if strict ordering matters |
| **Durability** | Commands are request-reply; the write service persists before acking |
| **Scaling** | Read and write services scale independently via consumer groups |
| **Failure** | If the read service misses events, replay from events-store |

## When to Use This Pattern

- Systems where read and write workloads have different scaling requirements
- Domain models that benefit from separate write validation and read optimization
- Microservices that need event-driven state synchronization across bounded contexts
