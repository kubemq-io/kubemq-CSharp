// KubeMQ .NET SDK — QueuesStream: NAckAll via Stream
//
// This example demonstrates receiving messages via the downstream stream API
// and negatively acknowledging all messages using NAckAllDownstreamAsync.
// NAck'd messages are returned to the queue for redelivery.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queuesstream.nack-all" first
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-nack-all-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var downstream = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.nack-all",
    maxItems: 10,
    waitTimeoutMs: 5000);

Console.WriteLine($"Received {downstream.Messages.Count} messages, Transaction: {downstream.TransactionId}");

if (downstream.Messages.Count > 0 && !string.IsNullOrEmpty(downstream.TransactionId))
{
    await client.NAckAllDownstreamAsync(downstream.TransactionId);
    Console.WriteLine("All messages negatively acknowledged (returned to queue).");
}

Console.WriteLine("Done.");
