// KubeMQ .NET SDK — Events Store: Start From First
//
// This example subscribes to an event store channel and replays all events
// from the very first message stored, then continues receiving new events.
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
    ClientId = "csharp-eventsstore-start-from-first-client",
});
await client.ConnectAsync();

var subscription = new EventStoreSubscription
{
    Channel = "csharp-eventsstore.start-from-first",
    StartPosition = EventStoreStartPosition.StartFromFirst,
};

Console.WriteLine("Subscribed with StartFromFirst — replaying all stored events...");
await foreach (var evt in client.SubscribeToEventsStoreAsync(subscription))
{
    Console.WriteLine($"[Seq {evt.Sequence}] {Encoding.UTF8.GetString(evt.Body.Span)}");
}
