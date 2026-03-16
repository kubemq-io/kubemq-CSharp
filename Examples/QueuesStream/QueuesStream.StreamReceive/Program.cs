// KubeMQ .NET SDK — QueuesStream: Stream Receive
//
// This example demonstrates receiving messages from a queue using the downstream
// stream API with transactional semantics. Messages are received with a transaction ID
// that can be used for subsequent ack/nack/requeue operations.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.stream-receive" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-stream-receive-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send some test messages first
for (var i = 1; i <= 3; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "csharp-queuesstream.stream-receive",
        Body = Encoding.UTF8.GetBytes($"Stream message #{i}"),
    });
}

Console.WriteLine("Sent 3 messages");

// Receive via downstream stream
var downstream = await client.ReceiveQueueDownstreamAsync(
    channel: "csharp-queuesstream.stream-receive",
    maxItems: 10,
    waitTimeoutMs: 5000,
    autoAck: false);

Console.WriteLine($"Received {downstream.Messages.Count} messages, Transaction: {downstream.TransactionId}");

foreach (var msg in downstream.Messages)
{
    Console.WriteLine($"  {msg.MessageId}: {Encoding.UTF8.GetString(msg.Body.Span)}");
}

// Acknowledge all messages in the transaction
if (downstream.Messages.Count > 0 && !string.IsNullOrEmpty(downstream.TransactionId))
{
    await client.AckAllDownstreamAsync(downstream.TransactionId);
    Console.WriteLine("All messages acknowledged.");
}

Console.WriteLine("Done.");
