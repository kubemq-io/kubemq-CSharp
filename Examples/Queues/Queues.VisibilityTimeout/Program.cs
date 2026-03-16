// KubeMQ .NET SDK — Queues: Visibility Timeout
//
// This example demonstrates visibility timeout behavior.
// When a message is received with a visibility timeout, it becomes invisible
// to other consumers for the specified duration. The consumer must Ack the
// message within that window to prevent redelivery.
//
// Note: ExtendVisibility is not supported at the protocol level. Plan your
// visibility timeout to be long enough for processing, and Ack promptly.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queues-visibility-timeout-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = "csharp-queues.visibility-timeout";

// Send a message
await client.SendQueueMessageAsync(new QueueMessage
{
    Channel = channel,
    Body = Encoding.UTF8.GetBytes("Long-running task"),
});
Console.WriteLine("Sent message to queue");

// Receive via downstream stream with manual ack and a visibility timeout.
// The message becomes invisible to other consumers for the specified duration.
var downstream = await client.ReceiveQueueDownstreamAsync(
    channel: channel,
    maxItems: 1,
    waitTimeoutMs: 10000,
    autoAck: false);

if (downstream.Messages.Count > 0)
{
    foreach (var msg in downstream.Messages)
    {
        Console.WriteLine($"Received: {Encoding.UTF8.GetString(msg.Body.Span)}");

        // Simulate processing within the visibility window
        Console.WriteLine("Processing message...");
        await Task.Delay(2000);

        // Acknowledge before the visibility timeout expires so the message
        // is not redelivered to another consumer.
        await msg.AckAsync();
        Console.WriteLine("Message acknowledged — it will not be redelivered");
    }
}
else
{
    Console.WriteLine("No messages received");
}

Console.WriteLine("Done.");
