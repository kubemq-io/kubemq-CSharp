// KubeMQ .NET SDK — Management: List Channels with Search Filter
//
// This example demonstrates listing channels filtered by a regex pattern.
// The searchPattern parameter maps to the channel_search tag and is evaluated
// server-side as a regular expression.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-config-list-channels-with-filter-client",
});
await client.ConnectAsync();
Console.WriteLine("Connected to KubeMQ server");

await client.CreateChannelAsync("csharp-demo.orders.us", "events");
await client.CreateChannelAsync("csharp-demo.orders.eu", "events");
await client.CreateChannelAsync("csharp-demo.payments", "events");
Console.WriteLine("Created sample channels");

Console.WriteLine("\n--- All 'events' channels ---");
var all = await client.ListChannelsAsync("events");
foreach (var ch in all)
{
    Console.WriteLine($"  {ch.Name}");
}

Console.WriteLine("\n--- Filtered: 'csharp-demo\\.orders.*' ---");
var filtered = await client.ListChannelsAsync("events", @"csharp-demo\.orders.*");
foreach (var ch in filtered)
{
    Console.WriteLine($"  {ch.Name}");
}

await client.DeleteChannelAsync("csharp-demo.orders.us", "events");
await client.DeleteChannelAsync("csharp-demo.orders.eu", "events");
await client.DeleteChannelAsync("csharp-demo.payments", "events");
Console.WriteLine("\nCleaned up sample channels");
Console.WriteLine("Done.");
