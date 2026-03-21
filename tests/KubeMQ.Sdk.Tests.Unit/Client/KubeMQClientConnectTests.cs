using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientConnectTests
{
    [Fact]
    public async Task ConnectAsync_CallsTransportConnect()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5.0" });

        await client.ConnectAsync();

        transport.Verify(
            t => t.ConnectAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_TransitionsToConnected()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5.0" });

        client.State.Should().Be(ConnectionState.Idle);

        await client.ConnectAsync();

        client.State.Should().Be(ConnectionState.Ready);
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ThrowsInvalidOperation()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5.0" });

        await client.ConnectAsync();

        Func<Task> act = () => client.ConnectAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot connect*");
    }

    [Fact]
    public async Task ConnectAsync_TransportThrows_RevertsToDisconnected()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection refused"));

        Func<Task> act = () => client.ConnectAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("connection refused");

        client.State.Should().Be(ConnectionState.Idle);
    }

    [Fact]
    public async Task ConnectAsync_WhenDisposed_ThrowsObjectDisposed()
    {
        var (client, _) = TestClientFactory.Create();

        await client.DisposeAsync();

        Func<Task> act = () => client.ConnectAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ConnectAsync_RaisesStateChangedEvent()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        transport
            .Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "h", Version = "3.5.0" });

        var stateChanges = new List<ConnectionStateChangedEventArgs>();
        client.StateChanged += (_, args) => stateChanges.Add(args);

        await client.ConnectAsync();

        await Task.Delay(100);

        stateChanges.Should().Contain(e =>
            e.PreviousState == ConnectionState.Idle &&
            e.CurrentState == ConnectionState.Connecting);
        stateChanges.Should().Contain(e =>
            e.PreviousState == ConnectionState.Connecting &&
            e.CurrentState == ConnectionState.Ready);
    }

    [Fact]
    public async Task ConnectAsync_Cancellation_ThrowsOperationCanceled()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => client.ConnectAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
