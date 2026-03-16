// KubeMQ .NET SDK — Config: Standalone Connect
//
// This example demonstrates connecting to a KubeMQ server with explicit options.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-config-connect-client",
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();
var info = await client.PingAsync();
Console.WriteLine($"Connected to {info.Host} v{info.Version}");
