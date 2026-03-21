// KubeMQ .NET SDK — QueuesStream: Ack Range (Per-Message)
//
// This example demonstrates acknowledging specific messages individually
// using per-message AckAsync() via the downstream receiver API.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-ack-range-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

for (var i = 1; i <= 5; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "csharp-queuesstream.ack-range",
        Body = Encoding.UTF8.GetBytes($"Message #{i}")
    });
}

Console.WriteLine("Sent 5 messages");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.ack-range",
    MaxMessages = 5,
    WaitTimeoutSeconds = 10,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

foreach (var msg in batch.Messages)
{
    var body = Encoding.UTF8.GetString(msg.Body.Span);
    if (body.Contains("#1") || body.Contains("#3"))
    {
        await msg.AckAsync();
        Console.WriteLine($"Acked: {body}");
    }
    else
    {
        Console.WriteLine($"Skipped: {body} (stays in queue)");
    }
}

Console.WriteLine("Selective ack completed. Remaining messages stay in queue.");
Console.WriteLine("Done.");
