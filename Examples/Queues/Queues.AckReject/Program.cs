// KubeMQ .NET SDK — Queues: Acknowledge, Reject, and Requeue
//
// This example demonstrates different message settlement options:
// - AckAsync(): Successfully processed, remove from queue
// - RejectAsync(): Processing failed, discard message
// - RequeueAsync(): Return message to queue for retry
//
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

// Receive via downstream stream (supports manual settlement)
var downstream = await client.ReceiveQueueDownstreamAsync(
    channel: "csharp-queues.ack-reject",
    maxItems: 3,
    waitTimeoutMs: 10000,
    autoAck: false);

foreach (var msg in downstream.Messages)
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
        await msg.RejectAsync();
        Console.WriteLine("  -> Rejected (discarded)");
    }
    else
    {
        await msg.RequeueAsync();
        Console.WriteLine("  -> Requeued (will retry)");
    }
}

Console.WriteLine("Done.");
