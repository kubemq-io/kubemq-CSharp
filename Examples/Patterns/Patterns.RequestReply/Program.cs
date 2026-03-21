// KubeMQ .NET SDK — Patterns: Request/Reply
//
// This example demonstrates the request/reply pattern using commands.
// A handler subscribes and responds, then a sender issues a command and waits
// for acknowledgment. This pattern is useful for synchronous RPC-style calls.
//
// Prerequisites:
//   - KubeMQ server running on localhost:50000
//   - dotnet run

using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "csharp-patterns-request-reply-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

// Start the handler in the background
var cts = new CancellationTokenSource();
var handlerTask = Task.Run(async () =>
{
    await foreach (var cmd in client.SubscribeToCommandsAsync(
        new CommandsSubscription { Channel = "csharp-patterns.request-reply" }, cts.Token))
    {
        var body = Encoding.UTF8.GetString(cmd.Body.Span);
        Console.WriteLine($"[Handler] Received command: {body}");

        await client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = cmd.RequestId,
            ReplyChannel = cmd.ReplyChannel!,
            Executed = true,
        });

        Console.WriteLine("[Handler] Responded: executed=true");
    }
});

// Allow time for subscription to establish
await Task.Delay(1000);

// Send a command (request) and wait for the reply
Console.WriteLine("[Sender] Sending command...");
var response = await client.SendCommandAsync(new CommandMessage
{
    Channel = "csharp-patterns.request-reply",
    Body = Encoding.UTF8.GetBytes("process-order"),
    TimeoutInSeconds = 10,
});

Console.WriteLine($"[Sender] Response received: Executed={response.Executed}");

cts.Cancel();
Console.WriteLine("Done.");
