// KubeMQ .NET SDK — Queues: Basic Send and Receive
//
// This example demonstrates sending a queue message and receiving it with acknowledgment.
// Queue messages are pull-based and processed by exactly one consumer.
// Uses QueueDownstreamReceiver.PollAsync for transactional message settlement.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

// TODO: Replace with your KubeMQ server address
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-send-receive-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Send a queue message
var sendResult = await client.SendQueueMessageAsync(new QueueMessage
{
    Channel = "csharp-queues.send-receive",
    Body = Encoding.UTF8.GetBytes("Process order #1234"),
    Tags = new Dictionary<string, string> { ["priority"] = "high" }
});

Console.WriteLine($"Sent message: {sendResult.MessageId}");

// Receive via downstream receiver (supports manual settlement)
await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = "csharp-queues.send-receive",
    MaxMessages = 1,
    WaitTimeoutSeconds = 10,
    AutoAck = false,
});

if (batch.HasMessages)
{
    foreach (var msg in batch.Messages)
    {
        Console.WriteLine($"Received: {Encoding.UTF8.GetString(msg.Body.Span)}");
        await msg.AckAsync();
        Console.WriteLine("Message acknowledged");
    }
}
else
{
    Console.WriteLine("No messages received");
}

Console.WriteLine("Done.");

// Expected output:
// Connected to KubeMQ server
// Sent message: <message-id>
// Received: Process order #1234
// Message acknowledged
// Done.
