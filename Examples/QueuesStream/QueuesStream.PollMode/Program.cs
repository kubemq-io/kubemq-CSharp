// KubeMQ .NET SDK — QueuesStream: Poll Mode
//
// This example demonstrates using the poll-based queue consumption pattern
// with the downstream stream API. Messages are fetched on demand, giving
// the consumer full control over when to receive and process messages.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-poll-mode-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

for (var i = 1; i <= 10; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "csharp-queuesstream.poll-mode",
        Body = Encoding.UTF8.GetBytes($"Poll Message #{i}")
    });
}

Console.WriteLine("Sent 10 messages");

var batch = 1;
while (true)
{
    var downstream = await client.ReceiveQueueDownstreamAsync(
        channel: "csharp-queuesstream.poll-mode",
        maxItems: 3,
        waitTimeoutMs: 5000,
        autoAck: true);

    if (downstream.Messages.Count == 0)
    {
        Console.WriteLine("No more messages. Exiting poll loop.");
        break;
    }

    Console.WriteLine($"Poll batch #{batch}: received {downstream.Messages.Count} messages");
    foreach (var msg in downstream.Messages)
    {
        Console.WriteLine($"  {Encoding.UTF8.GetString(msg.Body.Span)}");
    }

    batch++;
}

Console.WriteLine("Done.");
