using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientLifecycleTests
{
    [Fact]
    public async Task PingAsync_ReturnsServerInfo()
    {
        var expected = new ServerInfo
        {
            Host = "test-host",
            Version = "3.5.0",
            ServerStartTime = 1700000000,
            ServerUpTimeSeconds = 3600,
        };

        var (client, transport) = TestClientFactory.Create();
        transport.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        await using (client)
        {
            var result = await client.PingAsync();

            result.Host.Should().Be("test-host");
            result.Version.Should().Be("3.5.0");
            result.ServerUpTimeSeconds.Should().Be(3600);
        }
    }

    [Fact]
    public async Task OperationsAfterDispose_ThrowObjectDisposedException()
    {
        var (client, _) = TestClientFactory.Create();

        await client.DisposeAsync();

        var act = () => client.PublishEventAsync(
            new EventMessage { Channel = "ch", Body = new byte[] { 1 } });

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var (client, _) = TestClientFactory.Create();

        var act = async () =>
        {
            await client.DisposeAsync();
            await client.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_IsIdempotent()
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
    public void State_DefaultsToDisconnected()
    {
        var (client, _) = TestClientFactory.Create();

        using (client)
        {
            client.State.Should().Be(ConnectionState.Disconnected);
        }
    }
}
