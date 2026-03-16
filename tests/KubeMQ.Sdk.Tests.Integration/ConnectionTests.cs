using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Tests.Integration.Helpers;
using Xunit;

namespace KubeMQ.Sdk.Tests.Integration;

public class ConnectionTests : IntegrationTestBase
{
    [Fact]
    public async Task ConnectAsync_SetsStateToConnected()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public async Task PingAsync_ReturnsValidServerInfo()
    {
        await using var client = CreateClient();
        await client.ConnectAsync();

        var info = await client.PingAsync();

        info.Should().NotBeNull();
        info.Host.Should().NotBeNullOrWhiteSpace();
        info.Version.Should().NotBeNullOrWhiteSpace();
        info.ServerUpTimeSeconds.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task StateChanged_FiresOnConnect()
    {
        await using var client = CreateClient();
        var stateChanges = new List<ConnectionStateChangedEventArgs>();
        client.StateChanged += (_, args) => stateChanges.Add(args);

        await client.ConnectAsync();

        stateChanges.Should().Contain(e => e.CurrentState == ConnectionState.Connected);
    }

    [Fact]
    public async Task DisposeAsync_SetsStateToDisposed()
    {
        var client = CreateClient();
        await client.ConnectAsync();

        await client.DisposeAsync();

        client.State.Should().Be(ConnectionState.Disposed);
    }

    [Fact]
    public async Task MultipleClients_CanConnectSimultaneously()
    {
        await using var client1 = CreateClient("multi-client-1");
        await using var client2 = CreateClient("multi-client-2");
        await using var client3 = CreateClient("multi-client-3");

        await client1.ConnectAsync();
        await client2.ConnectAsync();
        await client3.ConnectAsync();

        client1.State.Should().Be(ConnectionState.Connected);
        client2.State.Should().Be(ConnectionState.Connected);
        client3.State.Should().Be(ConnectionState.Connected);

        var info1 = await client1.PingAsync();
        var info2 = await client2.PingAsync();
        var info3 = await client3.PingAsync();

        info1.Host.Should().NotBeNullOrWhiteSpace();
        info2.Host.Should().NotBeNullOrWhiteSpace();
        info3.Host.Should().NotBeNullOrWhiteSpace();
    }
}
