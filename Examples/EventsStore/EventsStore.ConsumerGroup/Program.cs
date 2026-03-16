// KubeMQ .NET SDK — Events Store: Consumer Group
//
// This example subscribes to an event store channel using a consumer group.
// Multiple instances in the same group will load-balance messages between them.
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
    ClientId = "csharp-eventsstore-consumer-group-client",
});
await client.ConnectAsync();

var subscription = new EventStoreSubscription
{
    Channel = "csharp-eventsstore.consumer-group",
    Group = "my-consumer-group",
    StartPosition = EventStoreStartPosition.FromFirst,
};

Console.WriteLine("Subscribed with consumer group 'my-consumer-group'. Waiting for events...");
await foreach (var evt in client.SubscribeToEventStoreAsync(subscription))
{
    Console.WriteLine($"[Seq {evt.Sequence}] {Encoding.UTF8.GetString(evt.Body.Span)}");
}
