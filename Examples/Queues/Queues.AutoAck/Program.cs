// KubeMQ .NET SDK — Queues: Auto-Ack on Downstream Receive
//
// This example demonstrates receiving messages via the downstream receiver
// with AutoAck enabled. Messages are automatically acknowledged upon receipt,
// so no manual ack/nack is needed.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Send some messages to "csharp-queues.auto-ack" first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-auto-ack-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queues.auto-ack",
    MaxMessages = 5,
    WaitTimeoutSeconds = 5,
    AutoAck = true,
});

foreach (var msg in batch.Messages)
{
    Console.WriteLine($"Auto-acked: {msg.MessageId} — {Encoding.UTF8.GetString(msg.Body.Span)}");
}

Console.WriteLine("Done.");
