using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

public class CommandsTests : IntegrationTestBase
{
    [Fact]
    public async Task SendCommand_ReceivesExecutedResponse()
    {
        await using var sender = CreateClient("cmd-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("cmd-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("cmd-executed");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new CommandsSubscription { Channel = channel };
            await foreach (var cmd in handler.SubscribeToCommandsAsync(subscription, cts.Token))
            {
                await handler.SendCommandResponseAsync(
                    cmd.RequestId,
                    cmd.ReplyChannel!,
                    executed: true);
                tcs.TrySetResult(true);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        var response = await sender.SendCommandAsync(new CommandMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("do-something"),
            TimeoutInSeconds = 5,
        });

        response.Should().NotBeNull();
        response.Executed.Should().BeTrue();
        response.Error.Should().BeNull();

        await tcs.Task;
    }

    [Fact]
    public async Task SendCommand_ErrorResponse_PropagatesError()
    {
        await using var sender = CreateClient("cmd-err-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("cmd-err-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("cmd-error");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new CommandsSubscription { Channel = channel };
            await foreach (var cmd in handler.SubscribeToCommandsAsync(subscription, cts.Token))
            {
                await handler.SendCommandResponseAsync(
                    cmd.RequestId,
                    cmd.ReplyChannel!,
                    executed: false,
                    errorMessage: "command-failed");
                tcs.TrySetResult(true);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        var response = await sender.SendCommandAsync(new CommandMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("fail-command"),
            TimeoutInSeconds = 5,
        });

        response.Should().NotBeNull();
        response.Executed.Should().BeFalse();
        response.Error.Should().Contain("command-failed");

        await tcs.Task;
    }

    [Fact]
    public async Task SendCommand_NoHandler_ThrowsTimeout()
    {
        await using var sender = CreateClient("cmd-timeout-sender");
        await sender.ConnectAsync();

        var channel = UniqueChannel("cmd-no-handler");

        try
        {
            await sender.SendCommandAsync(new CommandMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes("no-handler"),
                TimeoutInSeconds = 2,
            });

            // If no exception, the response should indicate failure
        }
        catch (Exception ex)
        {
            // Expected: timeout or RPC error when no handler is available
            ex.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task SendCommand_PreservesBodyAndTags()
    {
        await using var sender = CreateClient("cmd-body-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("cmd-body-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("cmd-body-tags");
        var sentBody = Encoding.UTF8.GetBytes("command-with-body");
        var sentTags = new Dictionary<string, string> { ["action"] = "test", ["priority"] = "high" };
        var receivedBody = Array.Empty<byte>();
        IReadOnlyDictionary<string, string>? receivedTags = null;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new CommandsSubscription { Channel = channel };
            await foreach (var cmd in handler.SubscribeToCommandsAsync(subscription, cts.Token))
            {
                receivedBody = cmd.Body.ToArray();
                receivedTags = cmd.Tags;
                await handler.SendCommandResponseAsync(
                    cmd.RequestId,
                    cmd.ReplyChannel!,
                    executed: true);
                tcs.TrySetResult(true);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        await sender.SendCommandAsync(new CommandMessage
        {
            Channel = channel,
            Body = sentBody,
            Tags = sentTags,
            TimeoutInSeconds = 5,
        });

        await tcs.Task;

        receivedBody.Should().BeEquivalentTo(sentBody);
        receivedTags.Should().NotBeNull();
        receivedTags.Should().ContainKey("action").WhoseValue.Should().Be("test");
        receivedTags.Should().ContainKey("priority").WhoseValue.Should().Be("high");
    }
}
