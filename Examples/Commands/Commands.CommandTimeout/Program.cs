// KubeMQ .NET SDK — Commands: Timeout Handling
//
// This example demonstrates how command timeouts work. A command is sent with
// a short timeout and no handler is running, so it times out. The SDK surfaces
// this as a KubeMQTimeoutException.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Do NOT start a command handler — this example expects a timeout
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Exceptions;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "csharp-commands-command-timeout-client",
});
await client.ConnectAsync();
Console.WriteLine("Connected to KubeMQ server");

int[] timeouts = [2, 5];

foreach (var timeout in timeouts)
{
    Console.WriteLine($"\nSending command with {timeout}s timeout (no handler running)...");
    try
    {
        var response = await client.SendCommandAsync(new CommandMessage
        {
            Channel = "csharp-commands.command-timeout",
            Body = Encoding.UTF8.GetBytes($"action-with-{timeout}s-timeout"),
            TimeoutInSeconds = timeout,
        });

        Console.WriteLine($"  Executed: {response.Executed}");
        if (!string.IsNullOrEmpty(response.Error))
        {
            Console.WriteLine($"  Error: {response.Error}");
        }
    }
    catch (KubeMQTimeoutException ex)
    {
        Console.WriteLine($"  Caught KubeMQTimeoutException: {ex.Message}");
    }
    catch (KubeMQException ex)
    {
        Console.WriteLine($"  Caught KubeMQException: {ex.Message}");
    }
}

Console.WriteLine("\nDone.");
