// KubeMQ .NET SDK — Patterns: Work Queue
//
// This example demonstrates the competing consumers (work queue) pattern using queues.
// Multiple workers poll the same queue, and each message is delivered to exactly one worker.
// This provides load balancing across workers.
// Uses QueueDownstreamReceiver.PollAsync for transactional message settlement.
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

// Simulate 2 workers pulling from the queue, each with its own receiver
for (int worker = 1; worker <= 2; worker++)
{
    await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

    var batch = await receiver.PollAsync(new QueuePollRequest
    {
        Channel = "csharp-patterns.work-queue",
        MaxMessages = 3,
        WaitTimeoutSeconds = 5,
        AutoAck = false,
    });

    Console.WriteLine($"\n[Worker-{worker}] Received {batch.Messages.Count} tasks:");
    foreach (var msg in batch.Messages)
    {
        Console.WriteLine($"  Processing: {Encoding.UTF8.GetString(msg.Body.Span)}");
        await msg.AckAsync();
    }
}

Console.WriteLine("\nDone.");
