// KubeMQ .NET SDK — QueuesStream: Stream Receive
//
// This example demonstrates receiving messages from a queue using the downstream
// receiver API with manual acknowledgment. Messages are polled and then
// acknowledged as a batch using batch.AckAllAsync().
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

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.stream-receive",
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

foreach (var msg in batch.Messages)
{
    Console.WriteLine($"  {msg.MessageId}: {Encoding.UTF8.GetString(msg.Body.Span)}");
}

if (batch.HasMessages)
{
    await batch.AckAllAsync();
    Console.WriteLine("All messages acknowledged.");
}

Console.WriteLine("Done.");
