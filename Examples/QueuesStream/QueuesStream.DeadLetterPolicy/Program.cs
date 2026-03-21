// KubeMQ .NET SDK — QueuesStream: Dead Letter Policy
//
// This example demonstrates configuring a dead letter queue (DLQ) with the downstream
// receiver API. After MaxReceiveCount failed attempts, the message moves to the DLQ
// channel. The receiver is used to poll and inspect DLQ messages.
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

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

// Check the DLQ via downstream receiver
var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.dlp-destination",
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
});

if (batch.HasMessages)
{
    Console.WriteLine($"DLQ contains {batch.Messages.Count} message(s):");
    foreach (var msg in batch.Messages)
    {
        Console.WriteLine($"  {Encoding.UTF8.GetString(msg.Body.Span)}");
    }
    await batch.AckAllAsync();
}
else
{
    Console.WriteLine("No messages in DLQ yet (need to exhaust retries first)");
}

Console.WriteLine("Done.");
