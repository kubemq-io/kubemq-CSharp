// KubeMQ .NET SDK — Connection: Ping
//
// This example demonstrates pinging the KubeMQ server to verify connectivity
// and retrieve server information such as host, version, and uptime.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-connection-ping-client",
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();

Console.WriteLine("Pinging KubeMQ server...");
var info = await client.PingAsync();
Console.WriteLine($"Host: {info.Host}");
Console.WriteLine($"Version: {info.Version}");
Console.WriteLine($"Server info: {info}");

Console.WriteLine("Done.");
