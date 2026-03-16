// KubeMQ .NET SDK — Queues: Simple AckAll
//
// This example demonstrates acknowledging all pending messages in a queue channel
// using the simple AckAllQueueMessagesAsync API (no stream transaction required).
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queues.ack-all-simple" first
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-ack-all-simple-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var result = await client.AckAllQueueMessagesAsync("csharp-queues.ack-all-simple");
Console.WriteLine($"Acknowledged {result.AffectedMessages} messages");

Console.WriteLine("Done.");
