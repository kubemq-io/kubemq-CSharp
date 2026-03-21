// KubeMQ .NET SDK — QueuesStream: Delay Policy
//
// This example demonstrates sending a queue message with a delivery delay
// and receiving via the downstream receiver API. The message becomes visible
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

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

// Immediate receive — should not find the message yet
var immediate = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.delay-policy",
    MaxMessages = 1,
    WaitTimeoutSeconds = 1,
    AutoAck = true,
});

Console.WriteLine($"Immediate receive: {(immediate.HasMessages ? "found" : "empty (expected)")}");

// Wait for delay to expire, then receive again
Console.WriteLine("Waiting 6 seconds for delay to expire...");
await Task.Delay(6000);

var delayed = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.delay-policy",
    MaxMessages = 1,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

if (delayed.HasMessages)
{
    Console.WriteLine($"Delayed receive: {Encoding.UTF8.GetString(delayed.Messages[0].Body.Span)}");
    await delayed.AckAllAsync();
}

Console.WriteLine("Done.");
