// KubeMQ .NET SDK — QueuesStream: Poll Mode
//
// This example demonstrates using the poll-based queue consumption pattern
// with the downstream receiver API. Messages are fetched on demand, giving
// the consumer full control over when to receive and process messages.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-poll-mode-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

for (var i = 1; i <= 10; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "csharp-queuesstream.poll-mode",
        Body = Encoding.UTF8.GetBytes($"Poll Message #{i}")
    });
}

Console.WriteLine("Sent 10 messages");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batchNum = 1;
while (true)
{
    var batch = await receiver.PollAsync(new QueuePollRequest
    {
        Channel = "csharp-queuesstream.poll-mode",
        MaxMessages = 3,
        WaitTimeoutSeconds = 5,
        AutoAck = true,
    });

    if (!batch.HasMessages)
    {
        Console.WriteLine("No more messages. Exiting poll loop.");
        break;
    }

    Console.WriteLine($"Poll batch #{batchNum}: received {batch.Messages.Count} messages");
    foreach (var msg in batch.Messages)
    {
        Console.WriteLine($"  {Encoding.UTF8.GetString(msg.Body.Span)}");
    }

    batchNum++;
}

Console.WriteLine("Done.");
