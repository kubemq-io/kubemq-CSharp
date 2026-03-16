// KubeMQ .NET SDK — Config: Custom Timeouts and Retry Policy
//
// This example demonstrates configuring operation timeouts, retry policy,
// keepalive, and reconnection behavior.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-config-custom-timeouts-client",

    // Operation timeouts
    DefaultTimeout = TimeSpan.FromSeconds(30),
    ConnectionTimeout = TimeSpan.FromSeconds(15),

    // Retry policy for transient failures
    Retry = new RetryPolicy
    {
        Enabled = true,
        MaxRetries = 5,
        InitialBackoff = TimeSpan.FromMilliseconds(500),
        MaxBackoff = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 2.0,
        JitterMode = JitterMode.Full
    },

    // gRPC keepalive pings
    Keepalive = new KeepaliveOptions
    {
        PingInterval = TimeSpan.FromSeconds(15),
        PingTimeout = TimeSpan.FromSeconds(5),
        PermitWithoutStream = true
    },

    // Auto-reconnection
    Reconnect = new ReconnectOptions
    {
        Enabled = true,
        MaxAttempts = 0,   // 0 = unlimited
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 2.0
    }
});

try
{
    await client.ConnectAsync();
    Console.WriteLine("Connected with custom configuration");
    Console.WriteLine($"  DefaultTimeout: {client.State}");

    var info = await client.PingAsync();
    Console.WriteLine($"  Server info: {info}");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}

Console.WriteLine("Done.");
