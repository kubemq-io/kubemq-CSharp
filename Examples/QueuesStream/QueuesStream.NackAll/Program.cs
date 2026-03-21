// KubeMQ .NET SDK — QueuesStream: NackAll via Batch
//
// This example demonstrates receiving messages via the downstream receiver API
// and negatively acknowledging all messages using batch.NackAllAsync().
// NACKed messages are returned to the queue for redelivery.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.nack-all" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-nack-all-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.nack-all",
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
