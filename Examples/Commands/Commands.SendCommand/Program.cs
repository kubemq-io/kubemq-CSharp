// KubeMQ .NET SDK — Commands: Send Command
//
// This example demonstrates sending a command and waiting for execution confirmation.
// Commands are request/reply: the sender waits for the handler to respond.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Run Commands.HandleCommand in a separate terminal first
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Exceptions;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-commands-send-command-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

try
{
    var response = await client.SendCommandAsync(new CommandMessage
    {
        Channel = "csharp-commands.send-command",
        Body = Encoding.UTF8.GetBytes("restart-service"),
        TimeoutInSeconds = 10
    });

    Console.WriteLine($"Command executed: {response.Executed}");
    if (!string.IsNullOrEmpty(response.Error))
    {
        Console.WriteLine($"Error: {response.Error}");
    }
}
catch (KubeMQTimeoutException)
{
    Console.WriteLine("Command timed out — no handler responded within 10 seconds");
}
catch (KubeMQOperationException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Command timed out — server reported: {ex.Message}");
}

Console.WriteLine("Done.");

// Expected output:
// Connected to KubeMQ server
// Command executed: True
// Done.
