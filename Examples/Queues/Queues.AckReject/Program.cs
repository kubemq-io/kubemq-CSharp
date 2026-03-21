// KubeMQ .NET SDK — Queues: Acknowledge, Nack, and Requeue
//
// This example demonstrates different message settlement options:
// - AckAsync(): Successfully processed, remove from queue
// - NackAsync(): Processing failed, reject message
// - ReQueueAsync(): Return message to queue for retry
//
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
    ClientId = "csharp-queues-ack-reject-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send test messages
for (var i = 1; i <= 3; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "csharp-queues.ack-reject",
        Body = Encoding.UTF8.GetBytes($"Message #{i}")
    });
}

Console.WriteLine("Sent 3 messages");

// Receive via downstream receiver (supports manual settlement)
await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queues.ack-reject",
    MaxMessages = 3,
    WaitTimeoutSeconds = 10,
    AutoAck = false,
});

foreach (var msg in batch.Messages)
{
    var body = Encoding.UTF8.GetString(msg.Body.Span);
    Console.WriteLine($"Processing: {body}");

    if (body.Contains("#1"))
    {
        await msg.AckAsync();
        Console.WriteLine("  -> Acknowledged (success)");
    }
    else if (body.Contains("#2"))
    {
        await msg.NackAsync();
        Console.WriteLine("  -> Nacked (rejected)");
    }
    else
    {
        await msg.ReQueueAsync();
        Console.WriteLine("  -> Requeued (will retry)");
    }
}

Console.WriteLine("Done.");
