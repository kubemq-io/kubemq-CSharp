// KubeMQ .NET SDK — QueuesStream: Ack Range
//
// This example demonstrates acknowledging a specific range of messages
// by their sequence numbers using the downstream stream API.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-ack-range-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

for (var i = 1; i <= 5; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "csharp-queuesstream.ack-range",
        Body = Encoding.UTF8.GetBytes($"Message #{i}")
    });
}

Console.WriteLine("Sent 5 messages");

var result = await client.ReceiveQueueDownstreamAsync(
    channel: "csharp-queuesstream.ack-range",
    maxItems: 5,
    waitTimeoutMs: 10000,
    autoAck: false);

Console.WriteLine($"Received {result.Messages.Count} messages, Transaction: {result.TransactionId}");

var sequencesToAck = result.Messages
    .Where(m => Encoding.UTF8.GetString(m.Body.Span).Contains("#1") ||
                Encoding.UTF8.GetString(m.Body.Span).Contains("#3"))
    .Select(m => m.Sequence)
    .ToList();

Console.WriteLine($"Acking sequences: {string.Join(", ", sequencesToAck)}");
await client.AckRangeDownstreamAsync(result.TransactionId, sequencesToAck);

Console.WriteLine("Ack range completed. Remaining messages stay in queue.");
Console.WriteLine("Done.");
