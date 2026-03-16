// KubeMQ .NET SDK — Queues: Message Expiration Policy
//
// This example demonstrates sending a queue message with an expiration policy.
// Messages that are not consumed within the expiration window are automatically discarded.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-expiration-policy-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var msg = new QueueMessage
{
    Channel = "csharp-queues.expiration-policy",
    Body = Encoding.UTF8.GetBytes("Expires in 30s"),
    ExpirationSeconds = 30,
};

var result = await client.SendQueueMessageAsync(msg);
Console.WriteLine($"Sent: {result.MessageId}, IsError={result.IsError}");

Console.WriteLine("Done.");
