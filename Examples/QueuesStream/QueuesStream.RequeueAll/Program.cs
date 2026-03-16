// KubeMQ .NET SDK — QueuesStream: ReQueueAll via Stream
//
// This example demonstrates receiving messages via the downstream stream API
// and re-routing all messages to a different channel using ReQueueAllDownstreamAsync.
// This is useful for dead-letter queue (DLQ) patterns.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.requeue-all" first
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-requeue-all-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var downstream = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.requeue-all",
    maxItems: 10,
    waitTimeoutMs: 5000);

Console.WriteLine($"Received {downstream.Messages.Count} messages, Transaction: {downstream.TransactionId}");

if (downstream.Messages.Count > 0 && !string.IsNullOrEmpty(downstream.TransactionId))
{
    await client.ReQueueAllDownstreamAsync(downstream.TransactionId, "csharp-queuesstream.dlq");
    Console.WriteLine("All messages re-queued to 'csharp-queuesstream.dlq'.");
}

Console.WriteLine("Done.");
