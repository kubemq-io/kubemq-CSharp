// KubeMQ .NET SDK — QueuesStream: Receiver Concurrent Ack
//
// This example demonstrates safe concurrent per-message acknowledgment using
// Task.WhenAll. All messages in a batch are acknowledged in parallel, showing
// that the settlement delegates are thread-safe.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-receiver-concurrentack-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

const string channel = "csharp-queuesstream.receiver-concurrentack";

for (var i = 1; i <= 10; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes($"Concurrent Message #{i}")
    });
}

Console.WriteLine("Sent 10 messages");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = channel,
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

if (batch.HasMessages)
{
    var ackTasks = batch.Messages.Select(msg => msg.AckAsync()).ToArray();
    await Task.WhenAll(ackTasks);
    Console.WriteLine($"All {batch.Messages.Count} messages acknowledged concurrently via Task.WhenAll");
}

Console.WriteLine("Done.");
