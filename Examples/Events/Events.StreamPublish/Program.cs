// KubeMQ .NET SDK — Events: Stream Publish
//
// This example demonstrates high-throughput event publishing using a bidirectional stream.
// Stream publishing reuses a single gRPC stream for many events, reducing overhead.
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
    ClientId = "csharp-events-stream-publish-client",
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();

await using var stream = await client.CreateEventStreamAsync(
    onError: ex => Console.WriteLine($"Stream error: {ex.Message}"));

for (int i = 0; i < 100; i++)
{
    var msg = new EventMessage
    {
        Channel = "csharp-events.stream-publish",
        Body = Encoding.UTF8.GetBytes($"Event #{i}"),
    };
    await stream.SendAsync(msg, options.ClientId!);
}

await stream.CloseAsync();
Console.WriteLine("Sent 100 events via stream.");
