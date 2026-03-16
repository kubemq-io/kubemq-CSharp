// KubeMQ .NET SDK — Config: Auth Token
//
// This example demonstrates connecting with a static authentication token.
//
// Prerequisites:
//   - KubeMQ server running with authentication enabled
//   - Valid auth token
//   - dotnet run

using KubeMQ.Sdk.Client;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-config-auth-token-client",
    AuthToken = "your-auth-token-here",
};

await using var client = new KubeMQClient(options);
await client.ConnectAsync();
var info = await client.PingAsync();
Console.WriteLine($"Authenticated to {info.Host}");
