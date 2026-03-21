// KubeMQ .NET SDK — Queues: Dead Letter Queue
//
// This example demonstrates configuring a dead letter queue (DLQ).
// After MaxReceiveCount failed attempts, the message moves to the DLQ channel.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-dead-letter-queue-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send a message with DLQ configuration
await client.SendQueueMessageAsync(new QueueMessage
{
    Channel = "csharp-queues.dead-letter-queue-source",
    Body = Encoding.UTF8.GetBytes("Order that will fail processing"),
    MaxReceiveCount = 3,
    MaxReceiveQueue = "csharp-queues.dead-letter-queue-destination"
});

Console.WriteLine("Sent message with MaxReceiveCount=3 and DLQ configured");
Console.WriteLine("After 3 failed receive attempts, the message moves to 'csharp-queues.dead-letter-queue-destination'");

// Poll the DLQ for failed messages
var dlqResponse = await client.ReceiveQueueMessagesAsync(new QueuePollRequest
{
    Channel = "csharp-queues.dead-letter-queue-destination",
    MaxMessages = 10,
    WaitTimeoutSeconds = 5
});

if (dlqResponse.HasMessages)
{
    foreach (var msg in dlqResponse.Messages)
    {
        Console.WriteLine($"DLQ message: {Encoding.UTF8.GetString(msg.Body.Span)}");
        await msg.AckAsync();
    }
}
else
{
    Console.WriteLine("No messages in DLQ yet (need to exhaust retries first)");
}

Console.WriteLine("Done.");
