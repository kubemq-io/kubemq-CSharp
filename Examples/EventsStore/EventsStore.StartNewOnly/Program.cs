// KubeMQ .NET SDK — Events Store: Start New Only
//
// This example subscribes to an event store channel receiving only new events
// published after the subscription is established (no replay of history).
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-eventsstore-start-new-only-client",
});
await client.ConnectAsync();

var subscription = new EventStoreSubscription
{
    Channel = "csharp-eventsstore.start-new-only",
    StartPosition = EventStoreStartPosition.FromNew,
};

Console.WriteLine("Subscribed with FromNew. Waiting for new events...");
await foreach (var evt in client.SubscribeToEventStoreAsync(subscription))
{
    Console.WriteLine($"[Seq {evt.Sequence}] {Encoding.UTF8.GetString(evt.Body.Span)}");
}
