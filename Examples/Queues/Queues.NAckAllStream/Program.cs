// KubeMQ .NET SDK — Queues: Downstream NAckAll via Stream
//
// This example demonstrates receiving messages via the downstream receiver
// and negatively acknowledging all messages using NackAllAsync.
// Nacked messages are returned to the queue for redelivery.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queues.nack-all-stream" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-nack-all-stream-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queues.nack-all-stream",
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

if (batch.HasMessages)
{
    await batch.NackAllAsync();
    Console.WriteLine("All messages negatively acknowledged (returned to queue).");
}

Console.WriteLine("Done.");
