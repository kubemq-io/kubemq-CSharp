// KubeMQ .NET SDK — Commands: Handle Command
//
// This example demonstrates subscribing to incoming commands and responding.
// Run this before Commands.SendCommand to handle the request.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-commands-handle-command-client",
});
await client.ConnectAsync();

Console.WriteLine("Waiting for commands on 'csharp-demo.commands'...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await foreach (var cmd in client.SubscribeToCommandsAsync(
    new CommandsSubscription { Channel = "csharp-commands.handle-command" }, cts.Token))
{
    var body = Encoding.UTF8.GetString(cmd.Body.Span);
    Console.WriteLine($"Received command: {body}");

    // Process the command and respond
    await client.SendCommandResponseAsync(new CommandResponse
    {
        RequestId = cmd.RequestId,
        ReplyChannel = cmd.ReplyChannel!,
        Executed = true,
    });

    Console.WriteLine("  -> Responded: executed=true");
}

Console.WriteLine("Done.");
