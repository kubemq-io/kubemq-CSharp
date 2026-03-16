// KubeMQ .NET SDK — QueuesStream: Delay Policy
//
// This example demonstrates sending a queue message with a delivery delay
// and receiving via the downstream stream API. The message becomes visible
// to consumers only after the delay expires.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-delay-policy-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send a message with 5-second delay
var sendResult = await client.SendQueueMessageAsync(new QueueMessage
{
    Channel = "csharp-queuesstream.delay-policy",
    Body = Encoding.UTF8.GetBytes("Delayed notification"),
    DelaySeconds = 5
});

Console.WriteLine($"Sent delayed message (5s delay): {sendResult.MessageId}");

// Immediate receive — should not find the message yet
var immediate = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.delay-policy",
    maxItems: 1,
    waitTimeoutMs: 1000);

Console.WriteLine($"Immediate receive: {(immediate.Messages.Count > 0 ? "found" : "empty (expected)")}");

// Wait for delay to expire, then receive again
Console.WriteLine("Waiting 6 seconds for delay to expire...");
await Task.Delay(6000);

var delayed = await client.ReceiveQueueDownstreamAsync(
    "csharp-queuesstream.delay-policy",
    maxItems: 1,
    waitTimeoutMs: 5000);

if (delayed.Messages.Count > 0)
{
    Console.WriteLine($"Delayed receive: {Encoding.UTF8.GetString(delayed.Messages[0].Body.Span)}");
    if (!string.IsNullOrEmpty(delayed.TransactionId))
    {
        await client.AckAllDownstreamAsync(delayed.TransactionId);
    }
}

Console.WriteLine("Done.");
