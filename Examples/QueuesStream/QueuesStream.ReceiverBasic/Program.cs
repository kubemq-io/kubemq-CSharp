// KubeMQ .NET SDK — QueuesStream: Receiver Basic
//
// This example demonstrates the basic QueueDownstreamReceiver workflow:
// create a receiver, send messages, poll with manual ack, acknowledge the
// entire batch via AckAllAsync, then poll again to confirm the queue is empty.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-receiver-basic-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

const string channel = "csharp-queuesstream.receiver-basic";

for (var i = 1; i <= 5; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes($"Basic Message #{i}")
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

Console.WriteLine($"Poll 1: received {batch.Messages.Count} messages");
foreach (var msg in batch.Messages)
{
    Console.WriteLine($"  {Encoding.UTF8.GetString(msg.Body.Span)}");
}

await batch.AckAllAsync();
Console.WriteLine("All messages acknowledged via AckAllAsync");

var batch2 = await receiver.PollAsync(new QueuePollRequest
{
    Channel = channel,
    MaxMessages = 10,
    WaitTimeoutSeconds = 3,
    AutoAck = false,
});

Console.WriteLine($"Poll 2: received {batch2.Messages.Count} messages (expected 0)");

Console.WriteLine("Done.");
