// KubeMQ .NET SDK — Events: Wildcard Subscription
//
// This example demonstrates subscribing to events using a wildcard channel pattern.
// Wildcard subscriptions receive events from all channels matching the pattern.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-events-wildcard-subscription-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var cts = new CancellationTokenSource();

// Subscribe to all channels matching "csharp-events.wildcard.*"
var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "csharp-events.wildcard.*" }, cts.Token))
    {
        Console.WriteLine($"[{msg.Channel}] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

await Task.Delay(1000);

// Publish to different sub-channels
await client.SendEventAsync(new EventMessage
{
    Channel = "csharp-events.wildcard.created",
    Body = Encoding.UTF8.GetBytes("Order #100 created")
});

await client.SendEventAsync(new EventMessage
{
    Channel = "csharp-events.wildcard.shipped",
    Body = Encoding.UTF8.GetBytes("Order #99 shipped")
});

await client.SendEventAsync(new EventMessage
{
    Channel = "csharp-events.wildcard.cancelled",
    Body = Encoding.UTF8.GetBytes("Order #98 cancelled")
});

await Task.Delay(2000);
cts.Cancel();

Console.WriteLine("Done.");
