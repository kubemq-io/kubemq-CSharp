// KubeMQ .NET SDK — Events Store: Persistent Pub/Sub
//
// This example demonstrates publishing and subscribing with server-side persistence.
// Events Store messages are stored by the server and can be replayed.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-eventsstore-persistent-pubsub-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Publish events first — they are persisted
for (var i = 1; i <= 5; i++)
{
    await client.PublishEventStoreAsync(new EventStoreMessage
    {
        Channel = "csharp-eventsstore.persistent-pubsub",
        Body = Encoding.UTF8.GetBytes($"Persistent Event #{i}")
    });
    Console.WriteLine($"Published persistent event #{i}");
}

// Subscribe from the beginning — replays all stored events
var cts = new CancellationTokenSource();
var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventStoreAsync(
        new EventStoreSubscription
        {
            Channel = "csharp-eventsstore.persistent-pubsub",
            StartPosition = EventStoreStartPosition.FromFirst
        }, cts.Token))
    {
        Console.WriteLine($"[Seq={msg.Sequence}] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

await Task.Delay(3000);
cts.Cancel();

Console.WriteLine("Done.");
