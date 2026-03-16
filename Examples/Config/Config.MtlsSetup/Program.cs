// KubeMQ .NET SDK — Config: Mutual TLS (mTLS) Setup
//
// This example demonstrates connecting with mutual TLS authentication.
// Both server and client present certificates for authentication.
//
// Prerequisites:
//   - KubeMQ server running with mTLS enabled
//   - Client certificate and key files available
//   - CA certificate for server verification
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

// Mutual TLS with client certificate
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    Tls = new TlsOptions
    {
        Enabled = true,
        CaFile = "/path/to/ca.pem",
        CertFile = "/path/to/client.pem",
        KeyFile = "/path/to/client.key"
    }
});

try
{
    await client.ConnectAsync();
    Console.WriteLine("Connected with mutual TLS (mTLS)");

    var info = await client.PingAsync();
    Console.WriteLine($"Server: {info}");
}
catch (Exception ex)
{
    Console.WriteLine($"mTLS connection failed: {ex.Message}");
    Console.WriteLine("Verify client certificate, key, and CA certificate paths.");
}

Console.WriteLine("Done.");
