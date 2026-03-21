// KubeMQ .NET SDK — QueuesStream: Receiver Per-Message Settlement
//
// This example demonstrates per-message settlement: each message is individually
// acknowledged, rejected (nacked), or requeued to a dead-letter channel based on
// its content.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-receiver-permsg-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

const string channel = "csharp-queuesstream.receiver-permsg";
const string dlqChannel = "csharp-queuesstream.receiver-permsg-dlq";

for (var i = 1; i <= 5; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes($"Message #{i}")
    });
}

Console.WriteLine("Sent 5 messages");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = channel,
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

var acked = 0;
var nacked = 0;
var requeued = 0;

foreach (var msg in batch.Messages)
{
    var body = Encoding.UTF8.GetString(msg.Body.Span);

    if (body.Contains("#1") || body.Contains("#2"))
    {
        await msg.AckAsync();
        Console.WriteLine($"  ACK:     {body}");
        acked++;
    }
    else if (body.Contains("#3"))
    {
        await msg.NackAsync();
        Console.WriteLine($"  NACK:    {body} (will be redelivered)");
        nacked++;
    }
    else
    {
        await msg.ReQueueAsync(dlqChannel);
        Console.WriteLine($"  REQUEUE: {body} → {dlqChannel}");
        requeued++;
    }
}

Console.WriteLine($"\nSummary: {acked} acked, {nacked} nacked, {requeued} requeued");
Console.WriteLine("Done.");
