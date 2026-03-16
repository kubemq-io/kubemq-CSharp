// KubeMQ .NET SDK — Connection: Connect
//
// This example demonstrates connecting to a KubeMQ server with explicit options.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000", // TODO: Replace with your KubeMQ server address
    ClientId = "csharp-connection-connect-client",
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();
var info = await client.PingAsync();
Console.WriteLine($"Connected to {info.Host} v{info.Version}");

// Expected output:
// Connected to localhost v<version>
