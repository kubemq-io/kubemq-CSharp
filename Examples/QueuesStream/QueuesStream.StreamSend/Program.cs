// KubeMQ .NET SDK — QueuesStream: Stream Send
//
// This example demonstrates sending multiple queue messages via the upstream stream API.
// The upstream stream opens a bidirectional gRPC stream for efficient batch sending.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-stream-send-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var messages = new[]
{
    new QueueMessage { Channel = "csharp-queuesstream.stream-send", Body = Encoding.UTF8.GetBytes("Message 1") },
    new QueueMessage { Channel = "csharp-queuesstream.stream-send", Body = Encoding.UTF8.GetBytes("Message 2") },
    new QueueMessage { Channel = "csharp-queuesstream.stream-send", Body = Encoding.UTF8.GetBytes("Message 3") },
};

var result = await client.SendQueueMessagesUpstreamAsync(messages);
Console.WriteLine($"Upstream send: IsError={result.IsError}, Results={result.Results.Count}");

foreach (var r in result.Results)
{
    Console.WriteLine($"  {r.MessageId}: SentAt={r.SentAt}, IsError={r.IsError}");
}

Console.WriteLine("Done.");
