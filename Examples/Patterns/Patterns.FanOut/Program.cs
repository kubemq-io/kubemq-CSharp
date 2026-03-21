// KubeMQ .NET SDK — Patterns: Fan-Out
//
// This example demonstrates the fan-out pattern using events.
// A single publisher sends events that are received by all subscribers
// on the channel. Each subscriber independently receives every event.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-patterns-fan-out-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var cts = new CancellationTokenSource();

// Start 3 independent subscribers — all will receive every event (no group)
for (int n = 1; n <= 3; n++)
{
    var subscriberName = $"Subscriber-{n}";
    _ = Task.Run(async () =>
    {
        await foreach (var msg in client.SubscribeToEventsAsync(
            new EventsSubscription { Channel = "csharp-patterns.fan-out" }, cts.Token))
        {
            Console.WriteLine($"[{subscriberName}] {Encoding.UTF8.GetString(msg.Body.Span)}");
        }
    });
}

// Allow subscriptions to establish
await Task.Delay(1000);

// Publish events — each subscriber receives all of them
for (var i = 1; i <= 3; i++)
{
    await client.SendEventAsync(new EventMessage
    {
        Channel = "csharp-patterns.fan-out",
        Body = Encoding.UTF8.GetBytes($"Broadcast #{i}"),
    });
    Console.WriteLine($"Published broadcast #{i}");
}

await Task.Delay(2000);
cts.Cancel();

Console.WriteLine("Done.");
