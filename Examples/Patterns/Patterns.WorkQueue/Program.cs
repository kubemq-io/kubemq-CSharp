// KubeMQ .NET SDK — Patterns: Work Queue
//
// This example demonstrates the competing consumers (work queue) pattern using queues.
// Multiple workers poll the same queue, and each message is delivered to exactly one worker.
// This provides load balancing across workers.
// Uses ReceiveQueueDownstreamAsync for transactional message settlement.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-patterns-work-queue-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Enqueue work items
for (var i = 1; i <= 6; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "csharp-patterns.work-queue",
        Body = Encoding.UTF8.GetBytes($"Task #{i}"),
    });
}

Console.WriteLine("Enqueued 6 tasks");

// Simulate 2 workers pulling from the queue
for (int worker = 1; worker <= 2; worker++)
{
    var downstream = await client.ReceiveQueueDownstreamAsync(
        channel: "csharp-patterns.work-queue",
        maxItems: 3,
        waitTimeoutMs: 5000,
        autoAck: false);

    Console.WriteLine($"\n[Worker-{worker}] Received {downstream.Messages.Count} tasks:");
    foreach (var msg in downstream.Messages)
    {
        Console.WriteLine($"  Processing: {Encoding.UTF8.GetString(msg.Body.Span)}");
        await msg.AckAsync();
    }
}

Console.WriteLine("\nDone.");
