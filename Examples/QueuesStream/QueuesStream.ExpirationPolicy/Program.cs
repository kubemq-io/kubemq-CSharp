// KubeMQ .NET SDK — QueuesStream: Message Expiration Policy
//
// This example demonstrates sending a queue message with an expiration policy
// and receiving it via the downstream receiver API. Messages that are not consumed
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

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

// Receive via downstream receiver before expiration
var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queuesstream.expiration-policy",
    MaxMessages = 1,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

if (batch.HasMessages)
{
    Console.WriteLine($"Received before expiration: {Encoding.UTF8.GetString(batch.Messages[0].Body.Span)}");
    await batch.AckAllAsync();
}
else
{
    Console.WriteLine("No messages received (may have expired).");
}

Console.WriteLine("Done.");
