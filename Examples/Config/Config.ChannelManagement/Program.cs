// KubeMQ .NET SDK — Management: Channel Create, List, and Delete
//
// This example demonstrates the channel management API: creating a channel,
// listing channels of a given type, and deleting a channel.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-config-channel-management-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

await client.CreateChannelAsync("csharp-config.channel-management", "events");
Console.WriteLine("Channel 'csharp-my-events-channel' created.");

var channels = await client.ListChannelsAsync("events");
foreach (var ch in channels)
{
    Console.WriteLine($"  {ch.Name} ({ch.Type}) active={ch.IsActive}");
}

await client.DeleteChannelAsync("csharp-config.channel-management", "events");
Console.WriteLine("Channel 'csharp-my-events-channel' deleted.");

Console.WriteLine("Done.");
