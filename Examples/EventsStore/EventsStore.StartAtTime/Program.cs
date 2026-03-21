// KubeMQ .NET SDK — Events Store: Start At Time
//
// This example subscribes starting from events stored in the last hour,
// using a specific point-in-time as the start position.
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
    ClientId = "csharp-eventsstore-start-at-time-client",
});
await client.ConnectAsync();

var subscription = new EventStoreSubscription
{
    Channel = "csharp-eventsstore.start-at-time",
    StartPosition = EventStoreStartPosition.StartAtTime,
    StartTime = DateTimeOffset.UtcNow.AddHours(-1),
};

Console.WriteLine("Subscribed with StartAtTime (1 hour ago). Replaying stored events...");
await foreach (var evt in client.SubscribeToEventsStoreAsync(subscription))
{
    Console.WriteLine($"[Seq {evt.Sequence}] {Encoding.UTF8.GetString(evt.Body.Span)}");
}
