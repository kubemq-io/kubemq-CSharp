// KubeMQ .NET SDK — Events: Cancel Subscription
//
// This example demonstrates cancelling an event subscription after a timeout.
// Uses CancellationTokenSource to auto-cancel after 10 seconds.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-events-cancel-subscription-client",
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

var subscription = new EventsSubscription
{
    Channel = "csharp-events.cancel-subscription",
};

Console.WriteLine("Subscribing for 10 seconds...");
try
{
    await foreach (var evt in client.SubscribeToEventsAsync(subscription, cts.Token))
    {
        Console.WriteLine($"Received on [{evt.Channel}]: {Encoding.UTF8.GetString(evt.Body.Span)}");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Subscription cancelled.");
}
