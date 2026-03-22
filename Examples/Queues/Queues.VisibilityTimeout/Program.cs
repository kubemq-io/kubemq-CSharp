// KubeMQ .NET SDK — Queues: Visibility Timeout
//
// This example demonstrates visibility timeout behavior in queue consumption.
// When a message is received without AutoAck, it becomes invisible to other
// consumers until it is settled (ack/nack/requeue) or the WaitTimeout expires.
//
// Pattern:
// 1. Send messages to the queue
// 2. Poll with AutoAck=false (manual settlement — messages are "locked")
// 3. Simulate slow processing with a delay
// 4. Settle messages within the visibility window
// 5. Verify no duplicate delivery
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-visibility-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = $"csharp-queues.visibility-{Guid.NewGuid():N}";

// Step 1: Send test messages
for (var i = 1; i <= 3; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes($"Visibility Message #{i}"),
    });
}

Console.WriteLine("Sent 3 messages");

// Step 2: Poll with manual settlement (messages become invisible to others)
await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = channel,
    MaxMessages = 3,
    WaitTimeoutSeconds = 10,
    AutoAck = false, // Manual settlement — messages are "locked" until settled
});

Console.WriteLine($"Received {batch.Messages.Count} messages (locked for processing)");

// Step 3: Simulate slow processing — messages remain invisible during this time
foreach (var msg in batch.Messages)
{
    var body = Encoding.UTF8.GetString(msg.Body.Span);
    Console.WriteLine($"Processing: {body} (receive count: {msg.ReceiveCount})");

    // Simulate work
    await Task.Delay(500);

    // Step 4: Acknowledge within the visibility window
    await msg.AckAsync();
    Console.WriteLine($"  -> Acknowledged");
}

// Step 5: Verify no messages remain (all were acked within the visibility window)
var remaining = await receiver.PollAsync(new QueuePollRequest
{
    Channel = channel,
    MaxMessages = 10,
    WaitTimeoutSeconds = 2,
    AutoAck = true,
});

Console.WriteLine($"Remaining messages after ack: {remaining.Messages.Count}");
Console.WriteLine("Done.");
