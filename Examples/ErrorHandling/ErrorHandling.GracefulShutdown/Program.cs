// KubeMQ .NET SDK — ErrorHandling: Graceful Shutdown
//
// This example demonstrates gracefully shutting down a client that has
// active subscriptions. The client drains in-flight callbacks, cancels
// subscriptions, and disposes the gRPC channel cleanly.
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
    ClientId = "csharp-errorhandling-graceful-shutdown-client",
    CallbackDrainTimeout = TimeSpan.FromSeconds(10),
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Start a subscription
var cts = new CancellationTokenSource();
var subscribeTask = Task.Run(async () =>
{
    try
    {
        await foreach (var msg in client.SubscribeToEventsAsync(
            new EventsSubscription { Channel = "csharp-errorhandling.graceful-shutdown" }, cts.Token))
        {
            Console.WriteLine($"Received: {Encoding.UTF8.GetString(msg.Body.Span)}");
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Subscription cancelled gracefully.");
    }
});

await Task.Delay(1000);

// Publish a few events
for (var i = 1; i <= 3; i++)
{
    await client.SendEventAsync(new EventMessage
    {
        Channel = "csharp-errorhandling.graceful-shutdown",
        Body = Encoding.UTF8.GetBytes($"Event #{i}"),
    });
}

await Task.Delay(1000);

// Graceful shutdown: cancel subscription, then dispose
Console.WriteLine("Initiating graceful shutdown...");
cts.Cancel();

// Wait briefly for subscription to exit
await Task.WhenAny(subscribeTask, Task.Delay(5000));

// DisposeAsync will drain in-flight callbacks and close the gRPC channel
await client.DisposeAsync();
Console.WriteLine("Client disposed. Shutdown complete.");

Console.WriteLine("Done.");
