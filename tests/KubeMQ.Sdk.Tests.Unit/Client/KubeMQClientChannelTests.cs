using System.Text;
using System.Text.Json;
using FluentAssertions;
using Google.Protobuf;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientChannelTests
{
    private static KubeMQ.Grpc.Response DefaultManagementResponse() =>
        new()
        {
            RequestID = "mgmt-001",
            Executed = true,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Error = string.Empty,
        };

    [Fact]
    public async Task ListChannelsAsync_CallsTransport()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListChannelsAsync("events");

        transport.Verify(
            t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListChannelsAsync_SetsCorrectMetadataAndTags()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListChannelsAsync("queues");

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("kubemq.cluster.internal.requests");
        captured.Metadata.Should().Be("list-channels");
        captured.RequestTypeData.Should().Be(KubeMQ.Grpc.Request.Types.RequestType.Query);
        captured.Tags.Should().ContainKey("channel_type").WhoseValue.Should().Be("queues");
    }

    [Fact]
    public async Task ListChannelsAsync_WithSearchPattern_SetsChannelSearchTag()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListChannelsAsync("events", "orders.*");

        captured.Should().NotBeNull();
        captured!.Tags.Should().ContainKey("channel_search").WhoseValue.Should().Be("orders.*");
    }

    [Fact]
    public async Task ListChannelsAsync_WithoutSearchPattern_DoesNotSetChannelSearchTag()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.ListChannelsAsync("events");

        captured.Should().NotBeNull();
        captured!.Tags.Should().NotContainKey("channel_search");
    }

    [Fact]
    public async Task CreateChannelAsync_NullName_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.CreateChannelAsync(null!, "events");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateChannelAsync_SetsCorrectMetadataAndTags()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.CreateChannelAsync("new-channel", "events");

        transport.Verify(
            t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()),
            Times.Once);

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("kubemq.cluster.internal.requests");
        captured.Metadata.Should().Be("create-channel");
        captured.RequestTypeData.Should().Be(KubeMQ.Grpc.Request.Types.RequestType.Query);
        captured.ClientID.Should().Be("test-client");
        captured.Tags.Should().ContainKey("channel_type").WhoseValue.Should().Be("events");
        captured.Tags.Should().ContainKey("channel").WhoseValue.Should().Be("new-channel");
        captured.Tags.Should().ContainKey("client_id").WhoseValue.Should().Be("test-client");
    }

    [Fact]
    public async Task DeleteChannelAsync_NullName_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.DeleteChannelAsync(null!, "events");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteChannelAsync_SetsCorrectMetadataAndTags()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Request? captured = null;
        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Request, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(DefaultManagementResponse());

        await client.DeleteChannelAsync("old-channel", "queues");

        transport.Verify(
            t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()),
            Times.Once);

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("kubemq.cluster.internal.requests");
        captured.Metadata.Should().Be("delete-channel");
        captured.RequestTypeData.Should().Be(KubeMQ.Grpc.Request.Types.RequestType.Query);
        captured.Tags.Should().ContainKey("channel_type").WhoseValue.Should().Be("queues");
        captured.Tags.Should().ContainKey("channel").WhoseValue.Should().Be("old-channel");
    }

    [Fact]
    public async Task ListChannelsAsync_ParsesResponseBody_WithChannelInfo()
    {
        var (client, transport) = TestClientFactory.Create();

        var channelData = new[]
        {
            new
            {
                name = "events.orders",
                type = "events",
                lastActivity = 1700000000L,
                isActive = true,
                incoming = new { messages = 100L, volume = 5000L, waiting = 0L, expired = 0L, delayed = 0L },
                outgoing = new { messages = 95L, volume = 4750L, waiting = 5L, expired = 2L, delayed = 1L },
            },
        };
        var json = JsonSerializer.Serialize(channelData);

        var response = new KubeMQ.Grpc.Response
        {
            RequestID = "mgmt-002",
            Executed = true,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Error = string.Empty,
            Body = ByteString.CopyFrom(Encoding.UTF8.GetBytes(json)),
        };

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var channels = await client.ListChannelsAsync("events");

        channels.Should().HaveCount(1);
        channels[0].Name.Should().Be("events.orders");
        channels[0].Type.Should().Be("events");
        channels[0].IsActive.Should().BeTrue();
        channels[0].LastActivity.Should().Be(1700000000L);
        var incoming = channels[0].Incoming;
        incoming.Should().NotBeNull();
        incoming!.Messages.Should().Be(100);
        incoming.Volume.Should().Be(5000);
        var outgoing = channels[0].Outgoing;
        outgoing.Should().NotBeNull();
        outgoing!.Messages.Should().Be(95);
        outgoing.Waiting.Should().Be(5);
        outgoing.Expired.Should().Be(2);
        outgoing.Delayed.Should().Be(1);
    }

    [Fact]
    public async Task ListChannelsAsync_EmptyBody_ReturnsEmpty()
    {
        var (client, transport) = TestClientFactory.Create();

        var response = new KubeMQ.Grpc.Response
        {
            RequestID = "mgmt-003",
            Executed = true,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Error = string.Empty,
        };

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var channels = await client.ListChannelsAsync("events");

        channels.Should().BeEmpty();
    }

    [Fact]
    public async Task ListChannelsAsync_InvalidJson_ReturnsEmpty()
    {
        var (client, transport) = TestClientFactory.Create();

        var response = new KubeMQ.Grpc.Response
        {
            RequestID = "mgmt-004",
            Executed = true,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Error = string.Empty,
            Body = ByteString.CopyFromUtf8("not valid json {{["),
        };

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var channels = await client.ListChannelsAsync("events");

        channels.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateChannelAsync_EmptyName_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.CreateChannelAsync("", "events");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateChannelAsync_WhitespaceName_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.CreateChannelAsync("   ", "events");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteChannelAsync_EmptyName_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.DeleteChannelAsync("", "events");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteChannelAsync_WhitespaceName_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.DeleteChannelAsync("   ", "queues");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateChannelAsync_TransportError_Propagates()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transport down"));

        Func<Task> act = () => client.CreateChannelAsync("ch", "events");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("transport down");
    }

    [Fact]
    public async Task DeleteChannelAsync_TransportError_Propagates()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transport error"));

        Func<Task> act = () => client.DeleteChannelAsync("ch", "events");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("transport error");
    }

    [Fact]
    public async Task ListChannelsAsync_TransportError_Propagates()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("list error"));

        Func<Task> act = () => client.ListChannelsAsync("events");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("list error");
    }

    [Fact]
    public async Task CreateChannelAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.CreateChannelAsync("ch", "events", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DeleteChannelAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.DeleteChannelAsync("ch", "queues", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ListChannelsAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendChannelManagementRequestAsync(
                It.IsAny<KubeMQ.Grpc.Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.ListChannelsAsync("events", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ListChannelsAsync_NullChannelType_ThrowsArgumentException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.ListChannelsAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
