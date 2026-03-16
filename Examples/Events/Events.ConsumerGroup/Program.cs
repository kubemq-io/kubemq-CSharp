// KubeMQ .NET SDK — Events: Consumer Group Subscription
//
// This example demonstrates subscribing to events with a consumer group.
// When multiple subscribers join the same group, events are load-balanced
// across them so that only one subscriber in the group receives each event.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-events-consumer-group-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var subscribeTask = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "csharp-events.consumer-group", Group = "my-group" },
        cts.Token))
    {
        var body = Encoding.UTF8.GetString(msg.Body.Span);
        Console.WriteLine($"Received event: {body}");
    }
});

await Task.Delay(1000);

for (var i = 1; i <= 5; i++)
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "csharp-events.consumer-group",
        Body = Encoding.UTF8.GetBytes($"Group Event #{i}"),
    });
    Console.WriteLine($"Published event #{i}");
}

await Task.Delay(2000);
cts.Cancel();

Console.WriteLine("Done.");
