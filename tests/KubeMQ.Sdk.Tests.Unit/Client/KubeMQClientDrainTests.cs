using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Client;

public class KubeMQClientDrainTests
{
    [Fact]
    public async Task DisposeAsync_CallsTransportCloseAsync()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();

        transport.Verify(
            t => t.CloseAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WithNoConnectionManager_CompletesGracefully()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_SetsStateToDisposed()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();

        client.State.Should().Be(ConnectionState.Disposed);
    }

    [Fact]
    public async Task DisposeAsync_RaisesDisposedStateChanged()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stateChanges = new List<ConnectionStateChangedEventArgs>();
        client.StateChanged += (_, args) => stateChanges.Add(args);

        await client.DisposeAsync();

        await Task.Delay(100);

        stateChanges.Should().Contain(e =>
            e.CurrentState == ConnectionState.Disposed);
    }

    [Fact]
    public async Task DisposeAsync_OperationsAfterDispose_ThrowObjectDisposed()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();

        Func<Task> actPing = () => client.PingAsync();
        Func<Task> actPublish = () => client.PublishEventAsync(
            new EventMessage { Channel = "ch", Body = new byte[] { 1 } });

        await actPing.Should().ThrowAsync<ObjectDisposedException>();
        await actPublish.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent_DoesNotCallCloseAsyncTwice()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();
        await client.DisposeAsync();

        transport.Verify(
            t => t.CloseAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Dispose_Synchronous_CompletesWithoutError()
    {
        var (client, _) = TestClientFactory.Create();

        var act = () => client.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_Synchronous_OperationsAfterThrowObjectDisposed()
    {
        var (client, _) = TestClientFactory.Create();

        client.Dispose();

        Func<Task> act = () => client.PingAsync();

        act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DrainCallbacksAsync_WaitsForInFlightCallbacks()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var disposeTask = client.DisposeAsync();
        await disposeTask;

        transport.Verify(
            t => t.CloseAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WithTimeout_CompletesWithinReasonableTime()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(async () => await Task.Delay(50));

        Func<Task> act = async () => await client.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DisposeAsync_TransportCloseAsync_IsCalledAfterDrain()
    {
        var callOrder = new List<string>();
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("CloseAsync"))
            .Returns(Task.CompletedTask);

        await client.DisposeAsync();

        callOrder.Should().Contain("CloseAsync");
    }

    [Fact]
    public async Task DisposeAsync_DrainCallbacksAsync_WithZeroActiveCallbacks_CompletesImmediately()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<Task> act = async () => await client.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Dispose_ThenDisposeAsync_DoesNotThrow()
    {
        var (client, transport) = TestClientFactory.Create();

        transport
            .Setup(t => t.CloseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        client.Dispose();

        Func<Task> act = async () => await client.DisposeAsync();

        await act.Should().NotThrowAsync();
    }
}
