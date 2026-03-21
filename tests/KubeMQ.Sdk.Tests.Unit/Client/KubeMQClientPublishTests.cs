using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientPublishTests
{
    [Fact]
    public async Task SendEventAsync_ValidMessage_CallsTransport()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Event? captured = null;
        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Event, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true });

        var message = new EventMessage
        {
            Channel = "test-channel",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        await client.SendEventAsync(message);

        transport.Verify(
            t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()),
            Times.Once);

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("test-channel");
        captured.Body.ToByteArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("hello"));
        captured.ClientID.Should().Be("test-client");
        captured.Store.Should().BeFalse();
    }

    [Fact]
    public async Task SendEventAsync_ConvenienceOverload_DelegatesToMain()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Event? captured = null;
        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Event, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true });

        var tags = new Dictionary<string, string> { ["key1"] = "val1" };
        var body = Encoding.UTF8.GetBytes("payload");

        await client.SendEventAsync("my-channel", body, tags);

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("my-channel");
        captured.Body.ToByteArray().Should().BeEquivalentTo(body);
        captured.Tags.Should().ContainKey("key1").WhoseValue.Should().Be("val1");
    }

    [Fact]
    public async Task SendEventAsync_NullMessage_ThrowsArgumentNullException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendEventAsync((EventMessage)null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendEventAsync_EmptyChannel_ThrowsValidationException()
    {
        var (client, _) = TestClientFactory.Create();

        var message = new EventMessage
        {
            Channel = "",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        Func<Task> act = () => client.SendEventAsync(message);

        await act.Should().ThrowAsync<KubeMQConfigurationException>();
    }

    [Fact]
    public async Task SendEventAsync_TransportThrows_PropagatesException()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transport failure"));

        var message = new EventMessage
        {
            Channel = "test-channel",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        Func<Task> act = () => client.SendEventAsync(message);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("transport failure");
    }

    [Fact]
    public async Task SendEventAsync_Cancelled_ThrowsOperationCanceledException()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var message = new EventMessage
        {
            Channel = "test-channel",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.SendEventAsync(message, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendEventStoreAsync_ValidMessage_CallsTransportWithStoreTrue()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Event? captured = null;
        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Event, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true });

        var message = new EventStoreMessage
        {
            Channel = "store-channel",
            Body = Encoding.UTF8.GetBytes("persisted"),
        };

        await client.SendEventStoreAsync(message);

        transport.Verify(
            t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()),
            Times.Once);

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("store-channel");
        captured.Store.Should().BeTrue();
        captured.Body.ToByteArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("persisted"));
    }

    [Fact]
    public async Task SendEventStoreAsync_NullMessage_ThrowsArgumentNullException()
    {
        var (client, _) = TestClientFactory.Create();

        Func<Task> act = () => client.SendEventStoreAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendEventStoreAsync_TransportThrows_PropagatesException()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store failure"));

        var message = new EventStoreMessage
        {
            Channel = "store-channel",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        Func<Task> act = () => client.SendEventStoreAsync(message);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("store failure");
    }

    [Fact]
    public async Task SendEventStoreAsync_EmptyChannel_ThrowsValidationException()
    {
        var (client, _) = TestClientFactory.Create();

        var message = new EventStoreMessage
        {
            Channel = "",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        Func<Task> act = () => client.SendEventStoreAsync(message);

        await act.Should().ThrowAsync<KubeMQConfigurationException>();
    }

    [Fact]
    public async Task SendEventStoreAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var message = new EventStoreMessage
        {
            Channel = "store-channel",
            Body = Encoding.UTF8.GetBytes("data"),
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.SendEventStoreAsync(message, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendEventAsync_WithTags_MapsTagsToGrpc()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Event? captured = null;
        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Event, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true });

        var tags = new Dictionary<string, string>
        {
            ["env"] = "production",
            ["region"] = "us-east",
            ["priority"] = "high",
        };

        var message = new EventMessage
        {
            Channel = "tagged-channel",
            Body = Encoding.UTF8.GetBytes("data"),
            Tags = tags,
        };

        await client.SendEventAsync(message);

        captured.Should().NotBeNull();
        captured!.Tags.Should().HaveCount(3);
        captured.Tags.Should().ContainKey("env").WhoseValue.Should().Be("production");
        captured.Tags.Should().ContainKey("region").WhoseValue.Should().Be("us-east");
        captured.Tags.Should().ContainKey("priority").WhoseValue.Should().Be("high");
    }

    [Fact]
    public async Task SendEventStoreAsync_ConvenienceOverload_NotAvailable_UsesMainOverload()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Event? captured = null;
        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Event, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true });

        var message = new EventStoreMessage
        {
            Channel = "store-ch",
            Body = Encoding.UTF8.GetBytes("stored-data"),
            Tags = new Dictionary<string, string> { ["k"] = "v" },
        };

        await client.SendEventStoreAsync(message);

        captured.Should().NotBeNull();
        captured!.Channel.Should().Be("store-ch");
        captured.Store.Should().BeTrue();
        captured.Body.ToByteArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("stored-data"));
        captured.Tags.Should().ContainKey("k").WhoseValue.Should().Be("v");
    }

    [Fact]
    public async Task SendEventAsync_AutoGeneratesEventId_WhenNotProvided()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Event? captured = null;
        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Event, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "auto", Sent = true });

        await client.SendEventAsync(new EventMessage { Channel = "ch", Body = new byte[] { 1 } });

        captured.Should().NotBeNull();
        captured!.EventID.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendEventAsync_CompletesSuccessfully()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "evt-1", Sent = true, Error = "" });

        await client.SendEventAsync(new EventMessage { Channel = "ch", Body = new byte[] { 1 } });

        transport.Verify(
            t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEventStoreAsync_AutoGeneratesEventId_WhenNotProvided()
    {
        var (client, transport) = TestClientFactory.Create();

        KubeMQ.Grpc.Event? captured = null;
        transport
            .Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .Callback<KubeMQ.Grpc.Event, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync(new KubeMQ.Grpc.Result { EventID = "auto", Sent = true });

        await client.SendEventStoreAsync(new EventStoreMessage { Channel = "ch", Body = new byte[] { 1 } });

        captured.Should().NotBeNull();
        captured!.EventID.Should().NotBeNullOrEmpty();
        captured.Store.Should().BeTrue();
    }
}
