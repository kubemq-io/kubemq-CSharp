// KubeMQ .NET SDK — Events Store: Replay From Sequence
//
// This example demonstrates replaying events starting from a specific sequence number.
// Useful for resuming consumption from a known checkpoint.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-eventsstore-replay-from-sequence-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Publish some events
for (var i = 1; i <= 10; i++)
{
    await client.SendEventStoreAsync(new EventStoreMessage
    {
        Channel = "csharp-eventsstore.replay-from-sequence",
        Body = Encoding.UTF8.GetBytes($"Event #{i}")
    });
}

Console.WriteLine("Published 10 events. Replaying from sequence 5...");

// Subscribe starting from sequence 5 — skips events 1-4
var cts = new CancellationTokenSource();
var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsStoreAsync(
        new EventStoreSubscription
        {
            Channel = "csharp-eventsstore.replay-from-sequence",
            StartPosition = EventStoreStartPosition.StartAtSequence,
            StartSequence = 5
        }, cts.Token))
    {
        Console.WriteLine($"[Seq={msg.Sequence}] {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
});

await Task.Delay(3000);
cts.Cancel();

Console.WriteLine("Done.");
