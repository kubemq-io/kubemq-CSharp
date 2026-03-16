# How To: Use Consumer Groups

Distribute messages across multiple subscribers using consumer groups for load-balanced processing.

## How Consumer Groups Work

When multiple subscribers join the same `Group` on a channel, each message is delivered to **exactly one** subscriber in the group. Without a group, every subscriber receives every message (fan-out).

## Events — Load-Balanced Subscription

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "events-group-demo",
});
await client.ConnectAsync();

using var cts = new CancellationTokenSource();

// Worker A
var workerA = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "orders.created", Group = "processors" },
        cts.Token))
    {
        Console.WriteLine($"[Worker A] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

// Worker B
var workerB = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "orders.created", Group = "processors" },
        cts.Token))
    {
        Console.WriteLine($"[Worker B] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

await Task.Delay(1000);

// Publish 6 events — each goes to exactly one worker
for (int i = 1; i <= 6; i++)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "orders.created",
        Body = Encoding.UTF8.GetBytes($"Order #{i}"),
    });
}

await Task.Delay(2000);
cts.Cancel();
```

## Events Store — Persistent with Group

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "store-group-demo",
});
await client.ConnectAsync();

using var cts = new CancellationTokenSource();

var subscriber = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventStoreAsync(
        new EventStoreSubscription
        {
            Channel = "audit.logs",
            Group = "log-indexers",
            StartPosition = EventStoreStartPosition.FromFirst,
        },
        cts.Token))
    {
        Console.WriteLine($"[Indexer] seq={msg.Sequence} {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

await Task.Delay(1000);

for (int i = 1; i <= 5; i++)
{
    await client.PublishEventStoreAsync(new EventStoreMessage
    {
        Channel = "audit.logs",
        Body = Encoding.UTF8.GetBytes($"Log entry {i}"),
    });
}

await Task.Delay(2000);
cts.Cancel();
```

## Commands — Distributed Command Handlers

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "cmd-group-demo",
});
await client.ConnectAsync();

using var cts = new CancellationTokenSource();

var handler = Task.Run(async () =>
{
    await foreach (var cmd in client.SubscribeToCommandsAsync(
        new CommandsSubscription { Channel = "commands.process", Group = "handlers" },
        cts.Token))
    {
        Console.WriteLine($"[Handler] {Encoding.UTF8.GetString(cmd.Body.Span)}");
        await client.SendCommandResponseAsync(
            cmd.RequestId,
            cmd.ReplyChannel!,
            executed: true);
    }
});

await Task.Delay(1000);

var response = await client.SendCommandAsync(new CommandMessage
{
    Channel = "commands.process",
    Body = Encoding.UTF8.GetBytes("do-work"),
    TimeoutInSeconds = 5,
});
Console.WriteLine($"Executed: {response.Executed}");

cts.Cancel();
```

## Queries — Distributed Query Handlers

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "query-group-demo",
});
await client.ConnectAsync();

using var cts = new CancellationTokenSource();

var handler = Task.Run(async () =>
{
    await foreach (var query in client.SubscribeToQueriesAsync(
        new QueriesSubscription { Channel = "queries.lookup", Group = "responders" },
        cts.Token))
    {
        Console.WriteLine($"[Responder] {Encoding.UTF8.GetString(query.Body.Span)}");
        await client.SendQueryResponseAsync(
            query.RequestId,
            query.ReplyChannel!,
            body: Encoding.UTF8.GetBytes("{\"status\":\"ok\"}"));
    }
});

await Task.Delay(1000);

var result = await client.SendQueryAsync(new QueryMessage
{
    Channel = "queries.lookup",
    Body = Encoding.UTF8.GetBytes("find-user"),
    TimeoutInSeconds = 5,
});
Console.WriteLine($"Response: {Encoding.UTF8.GetString(result.Body.Span)}");

cts.Cancel();
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| All subscribers receive every message | No `Group` set | Add `Group = "my-group"` to the subscription |
| One subscriber gets all messages | Only one subscriber in the group | Scale up by adding more group members |
| Messages stop after subscriber crash | No other group member available | Run 2+ subscribers per group for HA |
| Different groups get the same message | Groups are independent | This is correct — groups are isolated fan-out targets |
