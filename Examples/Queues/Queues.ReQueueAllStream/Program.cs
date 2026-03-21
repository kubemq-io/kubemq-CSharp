// KubeMQ .NET SDK — Queues: Downstream ReQueueAll via Stream
//
// This example demonstrates receiving messages via the downstream receiver
// and re-routing all messages to a different channel using ReQueueAllAsync.
// This is useful for dead-letter queue (DLQ) patterns.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queues.requeue-all-stream" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-requeue-all-stream-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queues.requeue-all-stream",
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

if (batch.HasMessages)
{
    await batch.ReQueueAllAsync("csharp-queues.requeue-all-stream-dlq");
    Console.WriteLine("All messages re-queued to 'csharp-queues.requeue-all-stream-dlq'.");
}

Console.WriteLine("Done.");
