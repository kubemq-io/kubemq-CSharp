// KubeMQ .NET SDK — QueuesStream: Message Expiration Policy
//
// This example demonstrates sending a queue message with an expiration policy
// and receiving it via the downstream stream API. Messages that are not consumed
// within the expiration window are automatically discarded.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-expiration-policy-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var msg = new QueueMessage
{
    Channel = "csharp-queuesstream.expiration-policy",
    Body = Encoding.UTF8.GetBytes("Expires in 30s"),
    ExpirationSeconds = 30,
};

var sendResult = await client.SendQueueMessageAsync(msg);
Console.WriteLine($"Sent: {sendResult.MessageId}, IsError={sendResult.IsError}");

// Receive via downstream stream before expiration
var downstream = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.expiration-policy",
    maxItems: 1,
    waitTimeoutMs: 5000);

if (downstream.Messages.Count > 0)
{
    Console.WriteLine($"Received before expiration: {Encoding.UTF8.GetString(downstream.Messages[0].Body.Span)}");
    if (!string.IsNullOrEmpty(downstream.TransactionId))
    {
        await client.AckAllDownstreamAsync(downstream.TransactionId);
    }
}
else
{
    Console.WriteLine("No messages received (may have expired).");
}

Console.WriteLine("Done.");
