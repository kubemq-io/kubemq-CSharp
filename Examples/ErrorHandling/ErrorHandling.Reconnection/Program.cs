// KubeMQ .NET SDK — ErrorHandling: Reconnection
//
// This example demonstrates the auto-reconnection behavior.
// The SDK can automatically reconnect when the connection is lost,
// using configurable backoff. The StateChanged event reports transitions.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run
//   - (Optional) stop/restart the server to observe reconnection behavior

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

var options = new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-errorhandling-reconnection-client",

    // Configure auto-reconnection
    Reconnect = new ReconnectOptions
    {
        Enabled = true,
        MaxAttempts = 10,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(15),
        BackoffMultiplier = 2.0,
    },
};

await using var client = new KubeMQClient(options);

// Subscribe to connection state changes
client.StateChanged += (_, args) =>
{
    Console.WriteLine($"[State] {args.PreviousState} -> {args.CurrentState} at {args.Timestamp:HH:mm:ss}");
    if (args.Error is not null)
    {
        Console.WriteLine($"  Error: {args.Error.Message}");
    }
};

try
{
    await client.ConnectAsync();
    Console.WriteLine($"Connected. Current state: {client.State}");
    Console.WriteLine("Monitoring connection state for 15 seconds...");
    Console.WriteLine("(Stop/restart the KubeMQ server to observe reconnection)");

    await Task.Delay(TimeSpan.FromSeconds(15));
    Console.WriteLine($"Final state: {client.State}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("Done.");
