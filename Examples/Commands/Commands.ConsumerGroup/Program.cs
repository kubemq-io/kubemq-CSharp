// KubeMQ .NET SDK — Commands: Consumer Group Subscription
//
// This example demonstrates subscribing to commands with a consumer group.
// When multiple handlers join the same group, commands are load-balanced across them
// so that only one handler in the group processes each command.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - Run Commands.SendCommand in a separate terminal to send commands
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-commands-consumer-group-client",
});
await client.ConnectAsync();

Console.WriteLine("Subscribed to commands with consumer group 'handler-group'...");

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await foreach (var cmd in client.SubscribeToCommandsAsync(
    new CommandsSubscription { Channel = "csharp-commands.consumer-group", Group = "handler-group" },
    cts.Token))
{
    var body = Encoding.UTF8.GetString(cmd.Body.Span);
    Console.WriteLine($"Command: {cmd.RequestId} — {body}");

    await client.SendCommandResponseAsync(
        requestId: cmd.RequestId,
        replyChannel: cmd.ReplyChannel!,
        executed: true);

    Console.WriteLine("  -> Responded: executed=true");
}

Console.WriteLine("Done.");
