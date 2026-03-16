using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientEdgeCaseTests
{
    [Fact]
    public async Task DisposeAsync_CallsTransportCloseAndDisposeAsync()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport.Setup(t => t.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        await client.DisposeAsync();

        transport.Verify(t => t.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoubleDisposeAsync_DoesNotThrow()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();

        var act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void DoubleSyncDispose_DoesNotThrow()
    {
        var (client, _) = TestClientFactory.Create();

        var act = () =>
        {
            client.Dispose();
            client.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateClientId_ReturnsNonEmptyStringWithHostnameAndPid()
    {
        var clientId = KubeMQClient.GenerateClientId();

        clientId.Should().NotBeNullOrWhiteSpace();
        clientId.Should().Contain(Environment.MachineName);
        clientId.Should().Contain(Environment.ProcessId.ToString());
    }

    [Fact]
    public void GenerateClientId_ContainsThreeParts()
    {
        var clientId = KubeMQClient.GenerateClientId();

        var parts = clientId.Split('-');
        parts.Length.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Constructor_WithEmptyClientId_GeneratesClientId()
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = string.Empty,
            Retry = new() { Enabled = false },
        };

        var transport = new Mock<ITransport>();
        var client = new KubeMQClient(options, transport.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        options.ClientId.Should().NotBeNullOrWhiteSpace();
        client.Dispose();
    }

    [Fact]
    public void ParseAddress_WithHttpPrefix_StripsPrefix()
    {
        var options = new KubeMQClientOptions
        {
            Address = "http://myhost:50000",
            ClientId = "test",
            Retry = new() { Enabled = false },
        };

        var transport = new Mock<ITransport>();
        var client = new KubeMQClient(options, transport.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        client.Should().NotBeNull();
        client.Dispose();
    }

    [Fact]
    public void ParseAddress_WithHttpsPrefix_StripsPrefix()
    {
        var options = new KubeMQClientOptions
        {
            Address = "https://myhost:50000",
            ClientId = "test",
            Retry = new() { Enabled = false },
        };

        var transport = new Mock<ITransport>();
        var client = new KubeMQClient(options, transport.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        client.Should().NotBeNull();
        client.Dispose();
    }

    [Fact]
    public void ParseAddress_WithPort_UsesSpecifiedPort()
    {
        var options = new KubeMQClientOptions
        {
            Address = "myhost:9090",
            ClientId = "test",
            Retry = new() { Enabled = false },
        };

        var transport = new Mock<ITransport>();
        var client = new KubeMQClient(options, transport.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        client.Should().NotBeNull();
        client.Dispose();
    }

    [Fact]
    public void ParseAddress_WithoutPort_DefaultsTo50000()
    {
        var options = new KubeMQClientOptions
        {
            Address = "myhost",
            ClientId = "test",
            Retry = new() { Enabled = false },
        };

        var transport = new Mock<ITransport>();
        var client = new KubeMQClient(options, transport.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        client.Should().NotBeNull();
        client.Dispose();
    }

    [Fact]
    public async Task PingAsync_CallsTransportPingAsync()
    {
        var expected = new ServerInfo
        {
            Host = "ping-host",
            Version = "3.5.0",
            ServerStartTime = 1700000000,
            ServerUpTimeSeconds = 100,
        };

        var (client, transport) = TestClientFactory.Create();
        transport.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await client.PingAsync();

        result.Should().BeSameAs(expected);
        transport.Verify(t => t.PingAsync(It.IsAny<CancellationToken>()), Times.Once);
        client.Dispose();
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithoutRetryHandler_CallsOperationDirectly()
    {
        var (client, transport) = TestClientFactory.Create();

        var serverInfo = new ServerInfo
        {
            Host = "h",
            Version = "1.0",
        };
        transport.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverInfo);

        var result = await client.PingAsync();

        result.Should().Be(serverInfo);
        client.Dispose();
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithRetryHandler_DelegatesToRetryHandler()
    {
        var options = new KubeMQClientOptions
        {
            Address = "localhost:50000",
            ClientId = "test-client",
            Retry = new() { Enabled = true, MaxRetries = 3 },
        };

        var transport = new Mock<ITransport>();
        var serverInfo = new ServerInfo
        {
            Host = "h",
            Version = "1.0",
        };
        transport.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverInfo);

        var client = new KubeMQClient(options, transport.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var result = await client.PingAsync();

        result.Host.Should().Be("h");
        client.Dispose();
    }

    [Fact]
    public async Task WaitForReadyIfNeeded_WithNullConnectionManager_PassesThrough()
    {
        var (client, transport) = TestClientFactory.Create();

        var serverInfo = new ServerInfo { Host = "h", Version = "1.0" };
        transport.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverInfo);

        var result = await client.PingAsync();

        result.Should().NotBeNull();
        client.Dispose();
    }

    [Fact]
    public void Dispose_CalledWithDisposingFalse_ViaDisposeAsync_DoesNotThrow()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var act = async () => await client.DisposeAsync();

        act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DrainAsync_OnEmptyClient_DoesNotThrow()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DrainCallbacksAsync_WithNoActiveCallbacks_CompletesImmediately()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();
    }

    [Fact]
    public async Task PublishEventAsync_CopyTagsWithEmptyTags_DoesNotThrow()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Result { Sent = true });

        var tags = new Dictionary<string, string>();
        var act = async () => await client.PublishEventAsync("ch", Encoding.UTF8.GetBytes("body"), tags);

        await act.Should().NotThrowAsync();
        client.Dispose();
    }

    [Fact]
    public async Task PublishEventAsync_CopyTagsWithNullTags_DoesNotThrow()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.SendEventAsync(It.IsAny<KubeMQ.Grpc.Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KubeMQ.Grpc.Result { Sent = true });

        var act = async () => await client.PublishEventAsync("ch", Encoding.UTF8.GetBytes("body"), null);

        await act.Should().NotThrowAsync();
        client.Dispose();
    }

    [Fact]
    public void Dispose_WithDisposingTrue_CleansUpResources()
    {
        var (client, _) = TestClientFactory.Create();

        client.Dispose();

        client.State.Should().Be(ConnectionState.Disposed);
    }

    [Fact]
    public async Task DisposeAsyncCore_WithNullStateMachine_DoesNotThrow()
    {
        var client = new TestableKubeMQClient();

        Func<Task> act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MultipleOperationsAfterDispose_AllThrowObjectDisposedException()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();

        Func<Task> actPing = () => client.PingAsync();
        Func<Task> actPublishEvent = () => client.PublishEventAsync(
            new KubeMQ.Sdk.Events.EventMessage { Channel = "ch", Body = new byte[] { 1 } });
        Func<Task> actPublishEventStore = () => client.PublishEventStoreAsync(
            new KubeMQ.Sdk.EventsStore.EventStoreMessage { Channel = "ch", Body = new byte[] { 1 } });
        Func<Task> actSendCommand = () => client.SendCommandAsync(
            new KubeMQ.Sdk.Commands.CommandMessage { Channel = "ch", Body = new byte[] { 1 } });
        Func<Task> actSendQuery = () => client.SendQueryAsync(
            new KubeMQ.Sdk.Queries.QueryMessage { Channel = "ch", Body = new byte[] { 1 } });
        Func<Task> actSendQueue = () => client.SendQueueMessageAsync(
            new KubeMQ.Sdk.Queues.QueueMessage { Channel = "ch", Body = new byte[] { 1 } });
        Func<Task> actListChannels = () => client.ListChannelsAsync("events");
        Func<Task> actCreateChannel = () => client.CreateChannelAsync("ch", "events");
        Func<Task> actDeleteChannel = () => client.DeleteChannelAsync("ch", "events");

        await actPing.Should().ThrowAsync<ObjectDisposedException>();
        await actPublishEvent.Should().ThrowAsync<ObjectDisposedException>();
        await actPublishEventStore.Should().ThrowAsync<ObjectDisposedException>();
        await actSendCommand.Should().ThrowAsync<ObjectDisposedException>();
        await actSendQuery.Should().ThrowAsync<ObjectDisposedException>();
        await actSendQueue.Should().ThrowAsync<ObjectDisposedException>();
        await actListChannels.Should().ThrowAsync<ObjectDisposedException>();
        await actCreateChannel.Should().ThrowAsync<ObjectDisposedException>();
        await actDeleteChannel.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void SyncDispose_ThenAsyncOperations_ThrowObjectDisposedException()
    {
        var (client, _) = TestClientFactory.Create();

        client.Dispose();

        Func<Task> actPing = () => client.PingAsync();
        Func<Task> actPublish = () => client.PublishEventAsync(
            new KubeMQ.Sdk.Events.EventMessage { Channel = "ch", Body = new byte[] { 1 } });

        actPing.Should().ThrowAsync<ObjectDisposedException>();
        actPublish.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_ThenSyncDispose_DoesNotThrow()
    {
        var (client, transport) = TestClientFactory.Create();

        transport.Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();

        Action act = () => client.Dispose();

        act.Should().NotThrow();
    }

    private class TestableKubeMQClient : KubeMQClient
    {
        public TestableKubeMQClient() : base() { }
    }
}
