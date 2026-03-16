using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class ConnectionManagerTests : IAsyncDisposable
{
    private readonly KubeMQClientOptions _options;
    private readonly Mock<ITransport> _transportMock;
    private readonly StateMachine _stateMachine;
    private readonly StreamManager _streamManager;
    private readonly ConnectionManager _manager;

    public ConnectionManagerTests()
    {
        _options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        _transportMock = new Mock<ITransport>();
        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _transportMock.Setup(t => t.SendBufferedAsync(
                It.IsAny<BufferedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _stateMachine = new StateMachine(NullLogger.Instance);
        _streamManager = new StreamManager(NullLogger.Instance);
        _manager = new ConnectionManager(
            _options, _transportMock.Object, _stateMachine, _streamManager, NullLogger.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _manager.DisposeAsync();
        _stateMachine.Dispose();
    }

    [Fact]
    public void CalculateBackoffDelay_FirstAttempt_ReturnsWithinInitialDelay()
    {
        var delay = _manager.CalculateBackoffDelay(1);

        delay.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        delay.Should().BeLessThanOrEqualTo(_options.Reconnect.InitialDelay);
    }

    [Fact]
    public void CalculateBackoffDelay_IncreasesExponentially()
    {
        double initialMs = _options.Reconnect.InitialDelay.TotalMilliseconds;
        double multiplier = _options.Reconnect.BackoffMultiplier;

        double maxAttempt1 = Enumerable.Range(0, 200)
            .Max(_ => _manager.CalculateBackoffDelay(1).TotalMilliseconds);
        double maxAttempt3 = Enumerable.Range(0, 200)
            .Max(_ => _manager.CalculateBackoffDelay(3).TotalMilliseconds);

        double expectedMaxAttempt3 = initialMs * Math.Pow(multiplier, 2);

        maxAttempt3.Should().BeGreaterThan(maxAttempt1,
            "higher attempts should have larger maximum possible delay");
        maxAttempt3.Should().BeLessThanOrEqualTo(expectedMaxAttempt3 + 1);
    }

    [Fact]
    public void CalculateBackoffDelay_CapsAtMaxDelay()
    {
        var delay = _manager.CalculateBackoffDelay(100);

        delay.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        delay.Should().BeLessThanOrEqualTo(_options.Reconnect.MaxDelay);
    }

    [Fact]
    public async Task WaitForReadyAsync_WhenConnected_CompletesImmediately()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);

        var task = _manager.WaitForReadyAsync(CancellationToken.None);

        task.IsCompleted.Should().BeTrue();
        await task;
    }

    [Fact]
    public async Task WaitForReadyAsync_WhenNotReady_BlocksUntilNotified()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _options.WaitForReady = true;

        var waitTask = _manager.WaitForReadyAsync(CancellationToken.None);

        waitTask.IsCompleted.Should().BeFalse();

        _manager.NotifyReady();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ResetReady_AfterNotify_RequiresNewNotify()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _options.WaitForReady = true;

        _manager.NotifyReady();
        _manager.ResetReady();

        var waitTask = _manager.WaitForReadyAsync(CancellationToken.None);

        waitTask.IsCompleted.Should().BeFalse();

        _manager.NotifyReady();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void DiscardBuffer_EmptyBuffer_ReturnsZero()
    {
        var count = _manager.DiscardBuffer();

        count.Should().Be(0);
    }

    [Fact]
    public void WaitForReadyAsync_WhenNotConnectedAndWaitForReadyFalse_Throws()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _options.WaitForReady = false;

        Action act = () => _manager.WaitForReadyAsync(CancellationToken.None);

        act.Should().Throw<KubeMQConnectionException>()
            .WithMessage("*not connected*WaitForReady*");
    }

    [Fact]
    public async Task BufferOrFailAsync_WhenReconnecting_EnqueuesMessage()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        var msg = new BufferedMessage(new byte[] { 1, 2 }, "ch", "event", 10);

        Func<Task> act = () => _manager.BufferOrFailAsync(msg, CancellationToken.None).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BufferOrFailAsync_WhenNotReconnecting_ThrowsInvalidOperation()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);

        var msg = new BufferedMessage(new byte[] { 1 }, "ch", "event", 5);

        Func<Task> act = () => _manager.BufferOrFailAsync(msg, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Not in reconnecting state*");
    }

    [Fact]
    public async Task FlushBufferAsync_WithBufferedMessages_InvokesSendBufferedOnTransport()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        var msg = new BufferedMessage(new byte[] { 1 }, "ch", "event", 5);
        await _manager.BufferOrFailAsync(msg, CancellationToken.None);

        _stateMachine.TryTransition(ConnectionState.Reconnecting, ConnectionState.Connected);

        await _manager.FlushBufferAsync(CancellationToken.None);

        _transportMock.Verify(
            t => t.SendBufferedAsync(It.IsAny<BufferedMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CancelsReconnection()
    {
        var connectDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                var tcs = new TaskCompletionSource();
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        _options.Reconnect.Enabled = true;
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);

        _manager.OnConnectionLost(new Exception("connection lost"));
        await Task.Delay(100);

        Func<Task> act = async () => await _manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OnConnectionLost_WhenConnected_TransitionsToReconnecting()
    {
        _options.Reconnect.Enabled = true;
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);

        _manager.OnConnectionLost(new Exception("lost"));

        _stateMachine.Current.Should().Be(ConnectionState.Reconnecting);
    }

    [Fact]
    public void OnConnectionLost_WhenReconnectDisabled_DoesNotTransition()
    {
        _options.Reconnect.Enabled = false;
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);

        _manager.OnConnectionLost(new Exception("lost"));

        _stateMachine.Current.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public void OnConnectionLost_WhenDisposed_DoesNotTransition()
    {
        _options.Reconnect.Enabled = true;
        _stateMachine.ForceDisposed();

        _manager.OnConnectionLost(new Exception("lost"));

        _stateMachine.Current.Should().Be(ConnectionState.Disposed);
    }

    [Fact]
    public void StateTransitionCallback_IsInvokedOnConnectionLost()
    {
        _options.Reconnect.Enabled = true;
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);

        ConnectionState? fromState = null;
        ConnectionState? toState = null;
        Exception? capturedException = null;

        _manager.StateTransitionCallback = (from, to, ex) =>
        {
            fromState = from;
            toState = to;
            capturedException = ex;
        };

        var exception = new Exception("test error");
        _manager.OnConnectionLost(exception);

        fromState.Should().Be(ConnectionState.Connected);
        toState.Should().Be(ConnectionState.Reconnecting);
        capturedException.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task ReconnectLoopAsync_SuccessfulReconnect_TransitionsToConnected()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        await _manager.ReconnectLoopAsync(CancellationToken.None);

        _stateMachine.Current.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public async Task ReconnectLoopAsync_ConnectSucceeds_InvokesStateTransitionCallback()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        ConnectionState? fromState = null;
        ConnectionState? toState = null;
        _manager.StateTransitionCallback = (from, to, _) =>
        {
            fromState = from;
            toState = to;
        };

        await _manager.ReconnectLoopAsync(CancellationToken.None);

        fromState.Should().Be(ConnectionState.Reconnecting);
        toState.Should().Be(ConnectionState.Connected);
    }

    [Fact]
    public async Task ReconnectLoopAsync_MaxAttemptsExhausted_ThrowsAndForcesDisposed()
    {
        _options.Reconnect.MaxAttempts = 2;
        _options.Reconnect.InitialDelay = TimeSpan.FromMilliseconds(10);
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connect failed"));

        Func<Task> act = () => _manager.ReconnectLoopAsync(CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQConnectionException>()
            .WithMessage("*Failed to reconnect*after 2 attempts*");

        _stateMachine.Current.Should().Be(ConnectionState.Disposed);
    }

    [Fact]
    public async Task ReconnectLoopAsync_CancellationRequested_StopsWithoutException()
    {
        using var cts = new CancellationTokenSource();
        int attempt = 0;

        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                Interlocked.Increment(ref attempt);
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
            });

        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        Func<Task> act = () => _manager.ReconnectLoopAsync(cts.Token);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FlushBufferAsync_EmptyBuffer_DoesNotCallTransport()
    {
        await _manager.FlushBufferAsync(CancellationToken.None);

        _transportMock.Verify(
            t => t.SendBufferedAsync(It.IsAny<BufferedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReconnectLoopAsync_FlushesBufferAfterReconnect()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        await _manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 1 }, "ch", "event", 5), CancellationToken.None);

        await _manager.ReconnectLoopAsync(CancellationToken.None);

        _transportMock.Verify(
            t => t.SendBufferedAsync(It.IsAny<BufferedMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconnectLoopAsync_ResubscribesAfterReconnect()
    {
        int resubCount = 0;
        _streamManager.TrackSubscription("sub-1", new SubscriptionRecord(
            "ch", SubscriptionPattern.Events, new object(),
            (_, _) => { Interlocked.Increment(ref resubCount); return Task.CompletedTask; }));

        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Connected);
        _stateMachine.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        await _manager.ReconnectLoopAsync(CancellationToken.None);

        resubCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_WhenNoReconnectIsHappening_CompletesCleanly()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = false,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };
        var transportMock = new Mock<ITransport>();
        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        Func<Task> act = async () => await manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
        stateMachine.Dispose();
    }

    [Fact]
    public async Task WaitForReadyCoreAsync_BlocksUntilConnected()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _options.WaitForReady = true;

        var waitTask = _manager.WaitForReadyAsync(CancellationToken.None);

        waitTask.IsCompleted.Should().BeFalse();

        await Task.Delay(50);
        _manager.NotifyReady();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(3));
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForReadyCoreAsync_TimesOut_ThrowsKubeMQTimeoutException()
    {
        var shortTimeoutOptions = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(200),
        };
        var transportMock = new Mock<ITransport>();
        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            shortTimeoutOptions, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);

        Func<Task> act = () => manager.WaitForReadyAsync(CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQTimeoutException>()
            .WithMessage("*Timed out*");

        await manager.DisposeAsync();
        stateMachine.Dispose();
    }

    [Fact]
    public async Task WaitForReadyAsync_CancellationToken_Respected()
    {
        _stateMachine.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting);
        _options.WaitForReady = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Func<Task> act = () => _manager.WaitForReadyAsync(cts.Token);

        await act.Should().ThrowAsync<Exception>();
    }
}
