// KubeMQ .NET SDK — QueuesStream: AckAll via Batch
//
// This example demonstrates receiving messages via the downstream receiver API
// and acknowledging all messages in a single batch using batch.AckAllAsync().
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.ack-all" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-ack-all-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.ack-all",
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

if (batch.HasMessages)
{
    await batch.AckAllAsync();
    Console.WriteLine("All messages acknowledged.");
}

Console.WriteLine("Done.");
