// KubeMQ .NET SDK — Events Store: Stream Publish
//
// This example demonstrates high-throughput persistent event publishing via stream.
// Each send awaits server confirmation of persistence.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.EventsStore;
using System.Text;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-eventsstore-stream-publish-client",
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();

await using var stream = await client.CreateEventStoreStreamAsync();

for (int i = 0; i < 10; i++)
{
    var msg = new EventStoreMessage
    {
        Channel = "csharp-eventsstore.stream-publish",
        Body = Encoding.UTF8.GetBytes($"Persistent event #{i}"),
    };
    var result = await stream.SendAsync(msg, options.ClientId!);
    Console.WriteLine($"Event {result.EventId}: Sent={result.Sent}");
}

await stream.CloseAsync();
