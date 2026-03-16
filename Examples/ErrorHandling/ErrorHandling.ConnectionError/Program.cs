// KubeMQ .NET SDK — ErrorHandling: Connection Error
//
// This example demonstrates handling connection errors when the KubeMQ server
// is unreachable. The SDK throws KubeMQConnectionException on connection failure.
//
// Prerequisites:
//   - Intentionally NO KubeMQ server running on the specified address
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Exceptions;

var options = new KubeMQClientOptions
{
    Address = "localhost:59999", // Wrong port — server not listening here
    ClientId = "csharp-errorhandling-connection-error-client",
    ConnectionTimeout = TimeSpan.FromSeconds(5),
};

await using var client = new KubeMQClient(options);

try
{
    Console.WriteLine("Attempting to connect to an unreachable server...");
    await client.ConnectAsync();
    Console.WriteLine("Connected (unexpected).");
}
catch (KubeMQConnectionException ex)
{
    Console.WriteLine($"Connection failed (expected): {ex.Message}");
    Console.WriteLine($"  ErrorCode: {ex.ErrorCode}");
    Console.WriteLine($"  IsRetryable: {ex.IsRetryable}");
}
catch (KubeMQException ex)
{
    Console.WriteLine($"KubeMQ error: {ex.Message}");
    Console.WriteLine($"  Category: {ex.Category}");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.GetType().Name} — {ex.Message}");
}

Console.WriteLine("Done.");
