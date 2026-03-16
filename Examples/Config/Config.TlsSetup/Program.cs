// KubeMQ .NET SDK — Config: TLS Setup
//
// This example demonstrates connecting to a KubeMQ server with TLS encryption.
// The server must be configured with TLS certificates.
//
// Prerequisites:
//   - KubeMQ server running with TLS enabled
//   - CA certificate file available
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

// TLS with CA certificate verification
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    Tls = new TlsOptions
    {
        Enabled = true,
        CaFile = "/path/to/ca.pem"
    }
});

try
{
    await client.ConnectAsync();
    Console.WriteLine("Connected with TLS encryption");

    var info = await client.PingAsync();
    Console.WriteLine($"Server: {info}");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    Console.WriteLine("Ensure the server has TLS enabled and the CA certificate is valid.");
}

Console.WriteLine("Done.");
