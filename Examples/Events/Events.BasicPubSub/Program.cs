// KubeMQ .NET SDK — Events: Basic Pub/Sub
//
// This example demonstrates publishing and subscribing to events on a channel.
// Events are fire-and-forget with no delivery guarantee.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run
//
// See also:
//   - Events.WildcardSubscription for wildcard channel patterns
//   - EventsStore.PersistentPubSub for persistent events with replay

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

// TODO: Replace with your KubeMQ server address
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-events-basic-pubsub-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Start subscribing in the background
var cts = new CancellationTokenSource();
var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "csharp-events.basic-pubsub" }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(msg.Body.Span);
        Console.WriteLine($"Received event: {body}");
    }
});

// Allow time for subscription to establish
await Task.Delay(1000);

// Publish 5 events
for (var i = 1; i <= 5; i++)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "csharp-events.basic-pubsub",
        Body = Encoding.UTF8.GetBytes($"Event #{i}"),
        Tags = new Dictionary<string, string> { ["source"] = "example" }
    });
    Console.WriteLine($"Published event #{i}");
}

// Wait for messages to arrive, then shut down
await Task.Delay(2000);
cts.Cancel();

Console.WriteLine("Done.");

// Expected output:
// Connected to KubeMQ server
// Published event #1
// Published event #2
// Published event #3
// Published event #4
// Published event #5
// Received event: Event #1
// Received event: Event #2
// Received event: Event #3
// Received event: Event #4
// Received event: Event #5
// Done.
