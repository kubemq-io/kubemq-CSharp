// KubeMQ .NET SDK — Queues: Delayed Messages
//
// This example demonstrates sending messages with a delivery delay.
// The message becomes visible to consumers only after the delay expires.
// Uses QueueDownstreamReceiver.PollAsync for transactional message settlement.
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

// Create one receiver and reuse it for both polls
await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

// Immediate receive — should not find the message yet
var immediateBatch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queues.delayed-messages",
    MaxMessages = 1,
    WaitTimeoutSeconds = 1,
    AutoAck = false,
});

Console.WriteLine($"Immediate receive: {(immediateBatch.HasMessages ? "found" : "empty (expected)")}");

if (immediateBatch.HasMessages)
{
    await immediateBatch.AckAllAsync();
}

// Wait for delay to expire, then receive again
Console.WriteLine("Waiting 6 seconds for delay to expire...");
await Task.Delay(6000);

var delayedBatch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queues.delayed-messages",
    MaxMessages = 1,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

if (delayedBatch.HasMessages)
{
    foreach (var msg in delayedBatch.Messages)
    {
        Console.WriteLine($"Delayed receive: {Encoding.UTF8.GetString(msg.Body.Span)}");
        await msg.AckAsync();
    }
}

Console.WriteLine("Done.");
