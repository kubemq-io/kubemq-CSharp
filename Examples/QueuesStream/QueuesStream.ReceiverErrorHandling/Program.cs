// KubeMQ .NET SDK — QueuesStream: Receiver Error Handling
//
// This example demonstrates wiring the OnError event handler on
// QueueDownstreamReceiver to observe settlement errors reported by the server
// (e.g., transaction not found, authorization failure on requeue).
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-queuesstream-receiver-errorhandling-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

const string channel = "csharp-queuesstream.receiver-errorhandling";

await using var receiver = await client.CreateQueueDownstreamReceiverAsync();

receiver.OnError += (sender, args) =>
{
    Console.WriteLine($"[OnError] Transaction: {args.TransactionId}, Error: {args.Error}");
};

Console.WriteLine("OnError handler wired — settlement errors will be printed above");

for (var i = 1; i <= 3; i++)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes($"Error Handling Message #{i}")
    });
}

Console.WriteLine("Sent 3 messages");

var batch = await receiver.PollAsync(new QueuePollRequest
{
    Channel = channel,
    MaxMessages = 10,
    WaitTimeoutSeconds = 5,
    AutoAck = false,
});

Console.WriteLine($"Received {batch.Messages.Count} messages");

if (batch.HasMessages)
{
    await batch.AckAllAsync();
    Console.WriteLine("All messages acknowledged");
}

Console.WriteLine("If the server had rejected a settlement, the OnError handler would have fired.");
Console.WriteLine("Done.");
