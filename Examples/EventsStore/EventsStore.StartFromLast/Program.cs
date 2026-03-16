// KubeMQ .NET SDK — Events Store: Start From Last
//
// This example subscribes starting from the most recent stored event,
// then continues to receive new events as they arrive.
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
    ClientId = "csharp-eventsstore-start-from-last-client",
});
await client.ConnectAsync();

var subscription = new EventStoreSubscription
{
    Channel = "csharp-eventsstore.start-from-last",
    StartPosition = EventStoreStartPosition.FromLast,
};

Console.WriteLine("Subscribed with FromLast. Replaying from most recent event...");
await foreach (var evt in client.SubscribeToEventStoreAsync(subscription))
{
    Console.WriteLine($"[Seq {evt.Sequence}] {Encoding.UTF8.GetString(evt.Body.Span)}");
}
