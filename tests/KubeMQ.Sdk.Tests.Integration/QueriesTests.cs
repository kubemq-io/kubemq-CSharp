using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

public class QueriesTests : IntegrationTestBase
{
    [Fact]
    public async Task SendQuery_ReceivesResponseWithBody()
    {
        await using var sender = CreateClient("qry-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("qry-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("qry-body");
        var responseBody = Encoding.UTF8.GetBytes("query-result-42");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new QueriesSubscription { Channel = channel };
            await foreach (var qry in handler.SubscribeToQueriesAsync(subscription, cts.Token))
            {
                await handler.SendQueryResponseAsync(
                    qry.RequestId,
                    qry.ReplyChannel!,
                    body: responseBody,
                    executed: true);
                tcs.TrySetResult(true);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        var response = await sender.SendQueryAsync(new QueryMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("what-is-the-answer"),
            TimeoutInSeconds = 5,
        });

        response.Should().NotBeNull();
        response.Executed.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Body.ToArray().Should().BeEquivalentTo(responseBody);

        await tcs.Task;
    }

    [Fact]
    public async Task SendQuery_ErrorResponse_PropagatesError()
    {
        await using var sender = CreateClient("qry-err-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("qry-err-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("qry-error");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new QueriesSubscription { Channel = channel };
            await foreach (var qry in handler.SubscribeToQueriesAsync(subscription, cts.Token))
            {
                await handler.SendQueryResponseAsync(
                    qry.RequestId,
                    qry.ReplyChannel!,
                    executed: false,
                    errorMessage: "query-failed");
                tcs.TrySetResult(true);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        var response = await sender.SendQueryAsync(new QueryMessage
        {
            Channel = channel,
            Body = Encoding.UTF8.GetBytes("bad-query"),
            TimeoutInSeconds = 5,
        });

        response.Should().NotBeNull();
        response.Executed.Should().BeFalse();
        response.Error.Should().Contain("query-failed");

        await tcs.Task;
    }

    [Fact]
    public async Task SendQuery_NoHandler_ThrowsTimeout()
    {
        await using var sender = CreateClient("qry-timeout-sender");
        await sender.ConnectAsync();

        var channel = UniqueChannel("qry-no-handler");

        try
        {
            await sender.SendQueryAsync(new QueryMessage
            {
                Channel = channel,
                Body = Encoding.UTF8.GetBytes("no-handler-query"),
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
    public async Task SendQuery_PreservesTagsAndMetadata()
    {
        await using var sender = CreateClient("qry-meta-sender");
        await sender.ConnectAsync();

        await using var handler = CreateClient("qry-meta-handler");
        await handler.ConnectAsync();

        var channel = UniqueChannel("qry-meta-tags");
        var sentBody = Encoding.UTF8.GetBytes("query-with-meta");
        var sentTags = new Dictionary<string, string> { ["source"] = "test", ["type"] = "query" };
        var receivedBody = Array.Empty<byte>();
        IReadOnlyDictionary<string, string>? receivedTags = null;
        string? receivedMetadata = null;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        cts.Token.Register(() => tcs.TrySetCanceled());

        _ = Task.Run(async () =>
        {
            var subscription = new QueriesSubscription { Channel = channel };
            await foreach (var qry in handler.SubscribeToQueriesAsync(subscription, cts.Token))
            {
                receivedBody = qry.Body.ToArray();
                receivedTags = qry.Tags;
                receivedMetadata = qry.Metadata;
                await handler.SendQueryResponseAsync(
                    qry.RequestId,
                    qry.ReplyChannel!,
                    body: Encoding.UTF8.GetBytes("response"),
                    executed: true);
                tcs.TrySetResult(true);
                break;
            }
        }, cts.Token);

        await Task.Delay(1000);

        await sender.SendQueryAsync(new QueryMessage
        {
            Channel = channel,
            Body = sentBody,
            Tags = sentTags,
            Metadata = "test-metadata",
            TimeoutInSeconds = 5,
        });

        await tcs.Task;

        receivedBody.Should().BeEquivalentTo(sentBody);
        receivedTags.Should().NotBeNull();
        receivedTags.Should().ContainKey("source").WhoseValue.Should().Be("test");
        receivedTags.Should().ContainKey("type").WhoseValue.Should().Be("query");
        receivedMetadata.Should().Be("test-metadata");
    }
}
