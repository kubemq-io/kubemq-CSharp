// KubeMQ .NET SDK — QueuesStream: ReQueueAll via Batch
//
// This example demonstrates receiving messages via the downstream receiver API
// and re-routing all messages to a different channel using batch.ReQueueAllAsync().
// This is useful for dead-letter queue (DLQ) patterns.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.requeue-all" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-requeue-all-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.requeue-all",
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

if (batch.HasMessages)
{
    await batch.ReQueueAllAsync("csharp-queuesstream.dlq");
    Console.WriteLine("All messages re-queued to 'csharp-queuesstream.dlq'.");
}

Console.WriteLine("Done.");
