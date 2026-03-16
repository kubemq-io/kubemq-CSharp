// KubeMQ .NET SDK — QueuesStream: AckAll via Stream
//
// This example demonstrates receiving messages via the downstream stream API
// and acknowledging all messages in a single transaction using AckAllDownstreamAsync.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.ack-all" first
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-ack-all-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var downstream = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.ack-all",
    maxItems: 10,
    waitTimeoutMs: 5000);

Console.WriteLine($"Received {downstream.Messages.Count} messages, Transaction: {downstream.TransactionId}");

if (downstream.Messages.Count > 0 && !string.IsNullOrEmpty(downstream.TransactionId))
{
    await client.AckAllDownstreamAsync(downstream.TransactionId);
    Console.WriteLine("All messages acknowledged.");
}

Console.WriteLine("Done.");
