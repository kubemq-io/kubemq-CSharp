// KubeMQ .NET SDK — QueuesStream: Auto-Ack on Downstream Receive
//
// This example demonstrates receiving messages via the downstream stream API
// with autoAck enabled. Messages are automatically acknowledged upon receipt,
// so no manual ack/nack is needed.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.auto-ack" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-auto-ack-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var downstream = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.auto-ack",
    maxItems: 5,
    waitTimeoutMs: 5000,
    autoAck: true);

foreach (var msg in downstream.Messages)
{
    Console.WriteLine($"Auto-acked: {msg.MessageId} — {Encoding.UTF8.GetString(msg.Body.Span)}");
}

Console.WriteLine("Done.");
