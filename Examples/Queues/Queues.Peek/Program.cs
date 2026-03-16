// KubeMQ .NET SDK — Queues: Peek Messages
//
// This example demonstrates peeking at queue messages without consuming them.
// Peeked messages remain in the queue and can still be received by consumers.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queues.peek" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-peek-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var response = await client.PeekQueueAsync(new QueuePollRequest
{
    Channel = "csharp-queues.peek",
    MaxMessages = 5,
    WaitTimeoutSeconds = 3,
});

Console.WriteLine($"Peeked {response.Messages.Count} messages (not consumed)");

foreach (var msg in response.Messages)
{
    Console.WriteLine($"  {msg.MessageId}: {Encoding.UTF8.GetString(msg.Body.Span)}");
}

Console.WriteLine("Done.");
