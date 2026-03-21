// KubeMQ .NET SDK — Events Store: Replay From Time
//
// This example demonstrates replaying events from a specific timestamp.
// Also shows the StartAtTimeDelta option for relative time offsets.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-eventsstore-replay-from-time-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Publish some events
for (var i = 1; i <= 5; i++)
{
    await client.SendEventStoreAsync(new EventStoreMessage
    {
        Channel = "csharp-eventsstore.replay-from-time",
        Body = Encoding.UTF8.GetBytes($"Event #{i}")
    });
}

Console.WriteLine("Published 5 events. Replaying from last 60 seconds...");

// Subscribe using time delta — replay events from the last 60 seconds
var cts = new CancellationTokenSource();
var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsStoreAsync(
        new EventStoreSubscription
        {
            Channel = "csharp-eventsstore.replay-from-time",
            StartPosition = EventStoreStartPosition.StartAtTimeDelta,
            StartTimeDeltaSeconds = 60
        }, cts.Token))
    {
        Console.WriteLine($"[Seq={msg.Sequence}] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

await Task.Delay(3000);
cts.Cancel();

Console.WriteLine("Done.");
