// KubeMQ .NET SDK — Queues: Batch Send and Receive
//
// This example demonstrates sending and receiving batches of queue messages.
// Batch operations reduce network round trips for high-throughput scenarios.
// Uses ReceiveQueueDownstreamAsync for transactional message settlement.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-batch-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send a batch of messages
var messages = Enumerable.Range(1, 10).Select(i => new QueueMessage
{
    Channel = "csharp-queues.batch",
    Body = Encoding.UTF8.GetBytes($"Batch item #{i}")
}).ToList();

var batchResult = await client.SendQueueMessagesAsync(messages);
Console.WriteLine($"Sent batch of {messages.Count} messages");

// Receive via downstream stream (supports manual settlement)
var downstream = await client.ReceiveQueueDownstreamAsync(
    channel: "csharp-queues.batch",
    maxItems: 10,
    waitTimeoutMs: 10000,
    autoAck: false);

Console.WriteLine($"Received {downstream.Messages.Count} messages:");

foreach (var msg in downstream.Messages)
{
    Console.WriteLine($"  {Encoding.UTF8.GetString(msg.Body.Span)}");
    await msg.AckAsync();
}

Console.WriteLine("Done.");
