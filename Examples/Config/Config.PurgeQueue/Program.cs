// KubeMQ .NET SDK — Management: Purge Queue
//
// This example demonstrates purging all messages from a queue channel
// using the PurgeQueueAsync API.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-config-purge-queue-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var result = await client.PurgeQueueAsync("csharp-config.purge-queue");
Console.WriteLine($"Purged {result.AffectedMessages} messages");

Console.WriteLine("Done.");
