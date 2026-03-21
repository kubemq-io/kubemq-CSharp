// KubeMQ .NET SDK — Events Store: Cancel Subscription
//
// This example demonstrates cancelling an event store subscription after a timeout.
// Uses CancellationTokenSource to auto-cancel after 10 seconds.
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
    ClientId = "csharp-eventsstore-cancel-subscription-client",
});
await client.ConnectAsync();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

var subscription = new EventStoreSubscription
{
    Channel = "csharp-eventsstore.cancel-subscription",
    StartPosition = EventStoreStartPosition.StartFromNew,
};

Console.WriteLine("Subscribing for 10 seconds...");
try
{
    await foreach (var evt in client.SubscribeToEventsStoreAsync(subscription, cts.Token))
    {
        Console.WriteLine($"[Seq {evt.Sequence}] {Encoding.UTF8.GetString(evt.Body.Span)}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Subscription cancelled.");
}
