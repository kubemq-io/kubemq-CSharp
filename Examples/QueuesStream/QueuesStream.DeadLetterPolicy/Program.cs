// KubeMQ .NET SDK — QueuesStream: Dead Letter Policy
//
// This example demonstrates configuring a dead letter queue (DLQ) with the stream API.
// After MaxReceiveCount failed attempts, the message moves to the DLQ channel.
// The downstream stream API is used to receive and inspect DLQ messages.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-dead-letter-policy-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send a message with DLQ configuration
await client.SendQueueMessageAsync(new QueueMessage
{
    Channel = "csharp-queuesstream.dlp-source",
    Body = Encoding.UTF8.GetBytes("Order that will fail processing"),
    MaxReceiveCount = 3,
    MaxReceiveQueue = "csharp-queuesstream.dlp-destination"
});

Console.WriteLine("Sent message with MaxReceiveCount=3 and DLQ configured");
Console.WriteLine("After 3 failed receive attempts, the message moves to 'csharp-queuesstream.dlp-destination'");

// Check the DLQ via downstream stream
var dlqDownstream = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.dlp-destination",
    maxItems: 10,
    waitTimeoutMs: 5000);

if (dlqDownstream.Messages.Count > 0)
{
    Console.WriteLine($"DLQ contains {dlqDownstream.Messages.Count} message(s):");
    foreach (var msg in dlqDownstream.Messages)
    {
        Console.WriteLine($"  {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
    if (!string.IsNullOrEmpty(dlqDownstream.TransactionId))
    {
        await client.AckAllDownstreamAsync(dlqDownstream.TransactionId);
    }
}
else
{
    Console.WriteLine("No messages in DLQ yet (need to exhaust retries first)");
}

Console.WriteLine("Done.");
