// KubeMQ .NET SDK — Events: Multiple Subscribers with Group Load Balancing
//
// This example demonstrates multiple subscribers on the same channel using groups.
// When subscribers share a group, messages are load-balanced among them.
// Without a group, all subscribers receive every message.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-events-multiple-subscribers-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var cts = new CancellationTokenSource();

// Two subscribers in the same group — messages load-balanced
var sub1 = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "csharp-events.multiple-subscribers", Group = "workers" }, cts.Token))
    {
        Console.WriteLine($"[Worker-1] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

var sub2 = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "csharp-events.multiple-subscribers", Group = "workers" }, cts.Token))
    {
        Console.WriteLine($"[Worker-2] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

await Task.Delay(1000);

// Publish 6 events — distributed between the two workers
for (var i = 1; i <= 6; i++)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "csharp-events.multiple-subscribers",
        Body = Encoding.UTF8.GetBytes($"Task #{i}")
    });
}

await Task.Delay(2000);
cts.Cancel();

Console.WriteLine("Done.");
