// KubeMQ .NET SDK — Queues: Delayed Messages
//
// This example demonstrates sending messages with a delivery delay.
// The message becomes visible to consumers only after the delay expires.
// Uses ReceiveQueueDownstreamAsync for transactional message settlement.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-delayed-messages-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send a message with 5-second delay
var sendResult = await client.SendQueueMessageAsync(new QueueMessage
{
    Channel = "csharp-queues.delayed-messages",
    Body = Encoding.UTF8.GetBytes("Delayed notification"),
    DelaySeconds = 5
});

Console.WriteLine($"Sent delayed message (5s delay): {sendResult.MessageId}");

// Immediate receive — should not find the message yet
var immediate = await client.ReceiveQueueDownstreamAsync(
    channel: "csharp-queues.delayed-messages",
    maxItems: 1,
    waitTimeoutMs: 1000,
    autoAck: false);

Console.WriteLine($"Immediate receive: {(immediate.Messages.Count > 0 ? "found" : "empty (expected)")}");

// Ack any unexpected messages from the immediate check
if (immediate.Messages.Count > 0 && !string.IsNullOrEmpty(immediate.TransactionId))
{
    await client.AckAllDownstreamAsync(immediate.TransactionId);
}

// Wait for delay to expire, then receive again
Console.WriteLine("Waiting 6 seconds for delay to expire...");
await Task.Delay(6000);

var delayed = await client.ReceiveQueueDownstreamAsync(
    channel: "csharp-queues.delayed-messages",
    maxItems: 1,
    waitTimeoutMs: 5000,
    autoAck: false);

if (delayed.Messages.Count > 0)
{
    foreach (var msg in delayed.Messages)
    {
        Console.WriteLine($"Delayed receive: {Encoding.UTF8.GetString(msg.Body.Span)}");
        await msg.AckAsync();
    }
}

Console.WriteLine("Done.");
