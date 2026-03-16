// KubeMQ .NET SDK — Queues: Downstream ReQueueAll via Stream
//
// This example demonstrates receiving messages via the downstream stream API
// and re-routing all messages to a different channel using ReQueueAllDownstreamAsync.
// This is useful for dead-letter queue (DLQ) patterns.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queues.requeue-all-stream" first
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-requeue-all-stream-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var downstream = await client.ReceiveQueueDownstreamAsync(
    "csharp-queues.requeue-all-stream",
    maxItems: 10,
    waitTimeoutMs: 5000);

Console.WriteLine($"Received {downstream.Messages.Count} messages, Transaction: {downstream.TransactionId}");

if (downstream.Messages.Count > 0 && !string.IsNullOrEmpty(downstream.TransactionId))
{
    await client.ReQueueAllDownstreamAsync(downstream.TransactionId, "csharp-queues.requeue-all-stream-dlq");
    Console.WriteLine("All messages re-queued to 'csharp-queues.dlq.example'.");
}

Console.WriteLine("Done.");
