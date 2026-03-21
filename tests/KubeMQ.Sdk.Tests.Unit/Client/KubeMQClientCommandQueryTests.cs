using System.Text;
using FluentAssertions;
using Google.Protobuf;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientCommandQueryTests
{
    private static KubeMQ.Grpc.Response DefaultGrpcResponse(
        string requestId = "req-001",
        bool executed = true,
        string? error = null) =>
        new()
        {
            RequestID = requestId,
            Executed = executed,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Error = error ?? string.Empty,
        };

    // --- SendCommandAsync ---

    [Fact]
    public async Task SendCommandAsync_ValidMessage_ReturnsResponse()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendCommandAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultGrpcResponse());

        var message = new CommandMessage
        {
            Channel = "cmd-channel",
            Body = Encoding.UTF8.GetBytes("command-body"),
            TimeoutInSeconds = 10,
        };

        var response = await client.SendCommandAsync(message);

        response.Should().NotBeNull();
        response.RequestId.Should().Be("req-001");
        response.Executed.Should().BeTrue();
        response.Error.Should().BeNull();

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("cmd-channel");
        captured.RequestTypeData.Should().Be(KubeMQ.Grpc.Request.Types.RequestType.Command);
        captured.ClientID.Should().Be("test-client");
    }

    [Fact]
    public async Task SendCommandAsync_NullMessage_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendCommandAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendCommandAsync_UsesDefaultTimeout_WhenNotSpecified()
    {
        var opts = TestClientFactory.DefaultOptions();
        opts.DefaultTimeout = TimeSpan.FromSeconds(7);
        var (client, transport) = TestClientFactory.Create(opts);

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendCommandAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultGrpcResponse());

        var message = new CommandMessage
        {
            Channel = "cmd-channel",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        await client.SendCommandAsync(message);

        captured.Should().NotBeNull();
        captured!.Timeout.Should().Be(7000);
    }

    [Fact]
    public async Task SendCommandAsync_UsesMessageTimeout_WhenSpecified()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendCommandAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultGrpcResponse());

        var message = new CommandMessage
        {
            Channel = "cmd-channel",
            Body = Encoding.UTF8.GetBytes("data"),
            TimeoutInSeconds = 15,
        };

        await client.SendCommandAsync(message);

        captured.Should().NotBeNull();
        captured!.Timeout.Should().Be(15000);
    }

    [Fact]
    public async Task SendCommandAsync_TransportError_Propagates()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendCommandAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("command transport error"));

        var message = new CommandMessage
        {
            Channel = "cmd-channel",
            Body = Encoding.UTF8.GetBytes("data"),
            TimeoutInSeconds = 5,
        };

        Func<Task> act = () => client.SendCommandAsync(message);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("command transport error");
    }

    // --- SendQueryAsync ---

    [Fact]
    public async Task SendQueryAsync_ValidMessage_ReturnsResponse()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = DefaultGrpcResponse();
        grpcResponse.Body = ByteString.CopyFromUtf8("query-result");

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendQueryAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(grpcResponse);

        var message = new QueryMessage
        {
            Channel = "query-channel",
            Body = Encoding.UTF8.GetBytes("query-body"),
            TimeoutInSeconds = 10,
        };

        var response = await client.SendQueryAsync(message);

        response.Should().NotBeNull();
        response.RequestId.Should().Be("req-001");
        response.Executed.Should().BeTrue();
        Encoding.UTF8.GetString(response.Body.Span).Should().Be("query-result");

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("query-channel");
        captured.RequestTypeData.Should().Be(KubeMQ.Grpc.Request.Types.RequestType.Query);
    }

    [Fact]
    public async Task SendQueryAsync_WithCacheKey_SetsGrpcCacheFields()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendQueryAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultGrpcResponse());

        var message = new QueryMessage
        {
            Channel = "cached-query",
            Body = Encoding.UTF8.GetBytes("data"),
            TimeoutInSeconds = 5,
            CacheKey = "my-cache-key",
            CacheTtlSeconds = 120,
        };

        await client.SendQueryAsync(message);

        captured.Should().NotBeNull();
        captured!.CacheKey.Should().Be("my-cache-key");
        captured.CacheTTL.Should().Be(120);
    }

    [Fact]
    public async Task SendQueryAsync_ResponseWithTags_MapsTags()
    {
        var (client, transport) = TestClientFactory.Create();

        var grpcResponse = DefaultGrpcResponse();
        grpcResponse.Tags.Add("resp-key", "resp-value");
        grpcResponse.Tags.Add("another", "tag");

        transport
            .Setup(t => t.SendQueryAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(grpcResponse);

        var message = new QueryMessage
        {
            Channel = "query-channel",
            Body = Encoding.UTF8.GetBytes("data"),
            TimeoutInSeconds = 5,
        };

        var response = await client.SendQueryAsync(message);

        response.Tags.Should().NotBeNull();
        response.Tags.Should().ContainKey("resp-key").WhoseValue.Should().Be("resp-value");
        response.Tags.Should().ContainKey("another").WhoseValue.Should().Be("tag");
    }

    // --- SendCommandResponseAsync ---

    [Fact]
    public async Task SendCommandResponseAsync_ValidInput_CallsTransport()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Response? captured = null;
        transport
            .Setup(t => t.SendCommandResponseAsync(It.IsAny<KubeMQ.Grpc.Response>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Response, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        await client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = "req-123",
            ReplyChannel = "reply-channel",
            Executed = true,
        });

        transport.Verify(
            t => t.SendCommandResponseAsync(It.IsAny<KubeMQ.Grpc.Response>(), It.IsAny<CancellationToken>()),
            Times.Once);

        captured.Should().NotBeNull();
        captured!.RequestID.Should().Be("req-123");
        captured.ReplyChannel.Should().Be("reply-channel");
        captured.Executed.Should().BeTrue();
        captured.Error.Should().BeEmpty();
        captured.ClientID.Should().Be("test-client");
    }

    [Fact]
    public async Task SendCommandResponseAsync_NullRequestId_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendCommandResponseAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- SendQueryResponseAsync ---

    [Fact]
    public async Task SendQueryResponseAsync_ValidInput_CallsTransport()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Response? captured = null;
        transport
            .Setup(t => t.SendQueryResponseAsync(It.IsAny<KubeMQ.Grpc.Response>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Response, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        await client.SendQueryResponseAsync(new QueryResponse
        {
            RequestId = "req-456",
            ReplyChannel = "reply-channel",
            Executed = true,
        });

        transport.Verify(
            t => t.SendQueryResponseAsync(It.IsAny<KubeMQ.Grpc.Response>(), It.IsAny<CancellationToken>()),
            Times.Once);

        captured.Should().NotBeNull();
        captured!.RequestID.Should().Be("req-456");
        captured.ReplyChannel.Should().Be("reply-channel");
        captured.Executed.Should().BeTrue();
    }

    [Fact]
    public async Task SendQueryResponseAsync_WithBodyAndTags_MapsCorrectly()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Response? captured = null;
        transport
            .Setup(t => t.SendQueryResponseAsync(It.IsAny<KubeMQ.Grpc.Response>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Response, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        var tags = new Dictionary<string, string> { ["result"] = "ok" };
        var body = Encoding.UTF8.GetBytes("response-body");

        await client.SendQueryResponseAsync(new QueryResponse
        {
            RequestId = "req-789",
            ReplyChannel = "reply-channel",
            Body = body,
            Executed = true,
            Tags = tags,
            Error = "minor-warning",
        });

        captured.Should().NotBeNull();
        captured!.RequestID.Should().Be("req-789");
        captured.Body.ToByteArray().Should().BeEquivalentTo(body);
        captured.Tags.Should().ContainKey("result").WhoseValue.Should().Be("ok");
        captured.Error.Should().Be("minor-warning");
        captured.Executed.Should().BeTrue();
        captured.ClientID.Should().Be("test-client");
    }

    // --- Additional SendQueryAsync tests ---

    [Fact]
    public async Task SendQueryAsync_NullMessage_ThrowsArgumentNull()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendQueryAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendQueryAsync_TransportError_Propagates()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueryAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("query transport error"));

        var message = new QueryMessage
        {
            Channel = "query-ch",
            Body = Encoding.UTF8.GetBytes("data"),
            TimeoutInSeconds = 5,
        };

        Func<Task> act = () => client.SendQueryAsync(message);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("query transport error");
    }

    [Fact]
    public async Task SendQueryAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendQueryAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var message = new QueryMessage
        {
            Channel = "query-ch",
            Body = Encoding.UTF8.GetBytes("data"),
            TimeoutInSeconds = 5,
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.SendQueryAsync(message, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendCommandAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendCommandAsync(It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var message = new CommandMessage
        {
            Channel = "cmd-ch",
            Body = Encoding.UTF8.GetBytes("data"),
            TimeoutInSeconds = 5,
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.SendCommandAsync(message, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- Additional SendCommandResponseAsync tests ---

    [Fact]
    public async Task SendCommandResponseAsync_NullReplyChannel_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = "req-1",
            ReplyChannel = null!,
            Executed = true,
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendCommandResponseAsync_EmptyReplyChannel_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendCommandResponseAsync(new CommandResponse
        {
            RequestId = "req-1",
            ReplyChannel = "",
            Executed = true,
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // --- Additional SendQueryResponseAsync tests ---

    [Fact]
    public async Task SendQueryResponseAsync_NullRequestId_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendQueryResponseAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendQueryResponseAsync_EmptyRequestId_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendQueryResponseAsync(new QueryResponse
        {
            RequestId = "",
            ReplyChannel = "reply-ch",
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendQueryResponseAsync_NullReplyChannel_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendQueryResponseAsync(new QueryResponse
        {
            RequestId = "req-1",
            ReplyChannel = null!,
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
