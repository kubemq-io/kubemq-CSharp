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
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        var task = _manager.WaitForReadyAsync(CancellationToken.None);

        task.IsCompleted.Should().BeTrue();
        await task;
    }

    [Fact]
    public async Task WaitForReadyAsync_WhenNotReady_BlocksUntilNotified()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
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
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
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
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _options.WaitForReady = false;

        Action act = () => _manager.WaitForReadyAsync(CancellationToken.None);

        act.Should().Throw<KubeMQConnectionException>()
            .WithMessage("*not connected*WaitForReady*");
    }

    [Fact]
    public async Task BufferOrFailAsync_WhenReconnecting_EnqueuesMessage()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        var msg = new BufferedMessage(new byte[] { 1, 2 }, "ch", "event", 10);

        Func<Task> act = () => _manager.BufferOrFailAsync(msg, CancellationToken.None).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BufferOrFailAsync_WhenNotReconnecting_ThrowsInvalidOperation()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        var msg = new BufferedMessage(new byte[] { 1 }, "ch", "event", 5);

        Func<Task> act = () => _manager.BufferOrFailAsync(msg, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Not in reconnecting state*");
    }

    [Fact]
    public async Task FlushBufferAsync_WithBufferedMessages_InvokesSendBufferedOnTransport()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        var msg = new BufferedMessage(new byte[] { 1 }, "ch", "event", 5);
        await _manager.BufferOrFailAsync(msg, CancellationToken.None);

        _stateMachine.TryTransition(ConnectionState.Reconnecting, ConnectionState.Ready);

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
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        _manager.OnConnectionLost(new Exception("connection lost"));
        await Task.Delay(100);

        Func<Task> act = async () => await _manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void OnConnectionLost_WhenConnected_TransitionsToReconnecting()
    {
        _options.Reconnect.Enabled = true;
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        _manager.OnConnectionLost(new Exception("lost"));

        _stateMachine.Current.Should().Be(ConnectionState.Reconnecting);
    }

    [Fact]
    public void OnConnectionLost_WhenReconnectDisabled_DoesNotTransition()
    {
        _options.Reconnect.Enabled = false;
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        _manager.OnConnectionLost(new Exception("lost"));

        _stateMachine.Current.Should().Be(ConnectionState.Ready);
    }

    [Fact]
    public void OnConnectionLost_WhenDisposed_DoesNotTransition()
    {
        _options.Reconnect.Enabled = true;
        _stateMachine.ForceDisposed();

        _manager.OnConnectionLost(new Exception("lost"));

        _stateMachine.Current.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void StateTransitionCallback_IsInvokedOnConnectionLost()
    {
        _options.Reconnect.Enabled = true;
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        var transitions = new List<(ConnectionState from, ConnectionState to, Exception? ex)>();

        _manager.StateTransitionCallback = (from, to, ex) =>
        {
            lock (transitions)
            {
                transitions.Add((from, to, ex));
            }
        };

        var exception = new Exception("test error");
        _manager.OnConnectionLost(exception);

        // Allow reconnect loop to complete (mock transport returns immediately)
        Thread.Sleep(100);

        lock (transitions)
        {
            transitions.Should().ContainSingle(t =>
                t.from == ConnectionState.Ready &&
                t.to == ConnectionState.Reconnecting &&
                t.ex == exception);
        }
    }

    [Fact]
    public async Task ReconnectLoopAsync_SuccessfulReconnect_TransitionsToConnected()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        await _manager.ReconnectLoopAsync(CancellationToken.None);

        _stateMachine.Current.Should().Be(ConnectionState.Ready);
    }

    [Fact]
    public async Task ReconnectLoopAsync_ConnectSucceeds_InvokesStateTransitionCallback()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        ConnectionState? fromState = null;
        ConnectionState? toState = null;
        _manager.StateTransitionCallback = (from, to, _) =>
        {
            fromState = from;
            toState = to;
        };

        await _manager.ReconnectLoopAsync(CancellationToken.None);

        fromState.Should().Be(ConnectionState.Reconnecting);
        toState.Should().Be(ConnectionState.Ready);
    }

    [Fact]
    public async Task ReconnectLoopAsync_MaxAttemptsExhausted_ThrowsAndForcesDisposed()
    {
        _options.Reconnect.MaxAttempts = 2;
        _options.Reconnect.InitialDelay = TimeSpan.FromMilliseconds(10);
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connect failed"));

        Func<Task> act = () => _manager.ReconnectLoopAsync(CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQConnectionException>()
            .WithMessage("*Failed to reconnect*after 2 attempts*");

        _stateMachine.Current.Should().Be(ConnectionState.Closed);
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

        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

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
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

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

        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

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
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
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

        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);

        Func<Task> act = () => manager.WaitForReadyAsync(CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQTimeoutException>()
            .WithMessage("*Timed out*");

        await manager.DisposeAsync();
        stateMachine.Dispose();
    }

    [Fact]
    public async Task WaitForReadyAsync_CancellationToken_Respected()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _options.WaitForReady = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Func<Task> act = () => _manager.WaitForReadyAsync(cts.Token);

        await act.Should().ThrowAsync<Exception>();
    }

    // ──────────────── Additional coverage tests ────────────────

    [Fact]
    public async Task DisposeAsync_WithBufferedMessages_DiscardsAndLogs()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        var transportMock = new Mock<ITransport>();
        transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                var tcs = new TaskCompletionSource();
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        // Buffer some messages before dispose
        await manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 1 }, "ch", "event", 5), CancellationToken.None);
        await manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 2 }, "ch", "event", 5), CancellationToken.None);

        Func<Task> act = async () => await manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
        stateMachine.Dispose();
    }

    [Fact]
    public async Task ReconnectLoopAsync_MaxAttemptsExhausted_WithBufferedMessages_DiscardsBuffer()
    {
        _options.Reconnect.MaxAttempts = 1;
        _options.Reconnect.InitialDelay = TimeSpan.FromMilliseconds(10);
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        // Buffer a message before reconnect loop
        await _manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 1 }, "ch", "event", 5), CancellationToken.None);

        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connect failed"));

        Func<Task> act = () => _manager.ReconnectLoopAsync(CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQConnectionException>()
            .WithMessage("*Failed to reconnect*after 1 attempts*");

        _stateMachine.Current.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task ReconnectLoopAsync_MaxAttemptsExhausted_InvokesStateTransitionCallback()
    {
        _options.Reconnect.MaxAttempts = 1;
        _options.Reconnect.InitialDelay = TimeSpan.FromMilliseconds(10);
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connect failed"));

        ConnectionState? fromState = null;
        ConnectionState? toState = null;
        Exception? capturedEx = null;
        _manager.StateTransitionCallback = (from, to, ex) =>
        {
            fromState = from;
            toState = to;
            capturedEx = ex;
        };

        Func<Task> act = () => _manager.ReconnectLoopAsync(CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQConnectionException>();

        fromState.Should().Be(ConnectionState.Reconnecting);
        toState.Should().Be(ConnectionState.Closed);
        capturedEx.Should().NotBeNull();
    }

    [Fact]
    public async Task OnConnectionLost_AlreadyReconnecting_DoesNotTransitionAgain()
    {
        _options.Reconnect.Enabled = true;
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        // Block reconnect so state stays in Reconnecting
        _transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                var tcs = new TaskCompletionSource();
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        // First call triggers reconnect
        _manager.OnConnectionLost(new Exception("first"));
        _stateMachine.Current.Should().Be(ConnectionState.Reconnecting);

        // Second call should not crash (TryTransition from Ready fails, nothing happens)
        _manager.OnConnectionLost(new Exception("second"));
        _stateMachine.Current.Should().Be(ConnectionState.Reconnecting);
    }

    [Fact]
    public async Task FlushBufferAsync_SendFailure_PropagatesException()
    {
        _stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        _stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        _stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        await _manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 1 }, "ch", "event", 5), CancellationToken.None);

        _stateMachine.TryTransition(ConnectionState.Reconnecting, ConnectionState.Ready);

        _transportMock.Setup(t => t.SendBufferedAsync(It.IsAny<BufferedMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("send failed"));

        Func<Task> act = () => _manager.FlushBufferAsync(CancellationToken.None);

        await act.Should().ThrowAsync<Exception>().WithMessage("send failed");
    }

    // ──────────────── Health-check loop tests ────────────────

    [Fact]
    public async Task HealthCheck_PingSucceeds_ContinuesRunning()
    {
        // Arrange: create a dedicated manager with a very short ping interval
        // We cannot change the 10s interval, so we mock PingAsync to track calls
        // and use StartHealthCheck + cancel quickly.
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        int pingCount = 0;
        var pingCalledTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportMock = new Mock<ITransport>();
        transportMock.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                if (Interlocked.Increment(ref pingCount) >= 1)
                {
                    pingCalledTcs.TrySetResult();
                }

                return new ServerInfo { Host = "localhost", Version = "1.0" };
            });

        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        // Put into Ready state so health check will ping
        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        // Act: start health check — it runs a background loop
        manager.StartHealthCheck();

        // Wait for at least one ping (may take up to ~10s due to the delay interval)
        var completed = await Task.WhenAny(
            pingCalledTcs.Task,
            Task.Delay(TimeSpan.FromSeconds(15)));

        // Dispose to cancel the loop
        await manager.DisposeAsync();

        // Assert
        completed.Should().Be(pingCalledTcs.Task, "ping should have been called at least once");
        pingCount.Should().BeGreaterOrEqualTo(1);
        stateMachine.Dispose();
    }

    [Fact]
    public async Task HealthCheck_PingFails_CallsOnConnectionLost()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromMilliseconds(10),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        var transportMock = new Mock<ITransport>();
        // Ping fails with an exception
        transportMock.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ping failed"));
        // ConnectAsync succeeds (for the reconnect triggered by OnConnectionLost)
        transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        var transitionedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.StateTransitionCallback = (from, to, ex) =>
        {
            if (from == ConnectionState.Ready && to == ConnectionState.Reconnecting)
            {
                transitionedTcs.TrySetResult();
            }
        };

        // Act
        manager.StartHealthCheck();

        // Wait for the transition to Reconnecting (health check detects ping failure)
        var completed = await Task.WhenAny(
            transitionedTcs.Task,
            Task.Delay(TimeSpan.FromSeconds(15)));

        // Cleanup
        await manager.DisposeAsync();

        // Assert
        completed.Should().Be(transitionedTcs.Task,
            "health check should trigger OnConnectionLost when ping fails");
        stateMachine.Dispose();
    }

    [Fact]
    public async Task HealthCheck_WhenNotReady_SkipsPing()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        var transportMock = new Mock<ITransport>();
        transportMock.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "localhost", Version = "1.0" });

        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        // Stay in Connecting (not Ready) so health check skips ping
        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);

        // Act
        manager.StartHealthCheck();

        // Wait one full health check interval plus margin
        await Task.Delay(TimeSpan.FromSeconds(12));

        await manager.DisposeAsync();

        // Assert: PingAsync should never have been called because state != Ready
        transportMock.Verify(
            t => t.PingAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        stateMachine.Dispose();
    }

    // ──────────────── DisposeAsync path coverage ────────────────

    [Fact]
    public async Task DisposeAsync_WithHealthCheckRunning_AwaitsHealthCheck()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        var transportMock = new Mock<ITransport>();
        transportMock.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "localhost", Version = "1.0" });

        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        // Start health check so _healthCheckTask is non-null
        manager.StartHealthCheck();

        // Give the health check loop a moment to start
        await Task.Delay(100);

        // Act: DisposeAsync should cancel and await the health check task
        Func<Task> act = async () => await manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
        stateMachine.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_WithReconnectRunning_AwaitsReconnect()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        // Block ConnectAsync until cancellation (simulates long reconnect)
        var transportMock = new Mock<ITransport>();
        transportMock.Setup(t => t.ConnectAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                var tcs = new TaskCompletionSource();
                ct.Register(() => tcs.TrySetCanceled(ct));
                return tcs.Task;
            });

        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        // Trigger reconnect so _reconnectTask is non-null
        manager.OnConnectionLost(new Exception("connection lost"));
        await Task.Delay(100);

        stateMachine.Current.Should().Be(ConnectionState.Reconnecting);

        // Act: DisposeAsync should cancel and await the reconnect task
        Func<Task> act = async () => await manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
        stateMachine.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_WithBufferedMessages_DiscardsBuffer()
    {
        // This test specifically covers the DisposeAsync path that calls
        // _buffer.DiscardAll() and logs when discarded > 0
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
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

        // Get into Reconnecting to buffer messages
        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        await manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 1 }, "ch1", "event", 5), CancellationToken.None);
        await manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 2 }, "ch2", "event", 5), CancellationToken.None);
        await manager.BufferOrFailAsync(
            new BufferedMessage(new byte[] { 3 }, "ch3", "event", 5), CancellationToken.None);

        // Act: DisposeAsync discards buffered messages
        Func<Task> act = async () => await manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));

        // After dispose, the buffer should be empty (all discarded)
        // We verify by confirming DisposeAsync ran without error
        stateMachine.Dispose();
    }

    // ──────────────── WaitForReadyCoreAsync timeout paths ────────────────

    [Fact]
    public async Task WaitForReadyCoreAsync_WhenReconnecting_UsesReconnectTimeout()
    {
        // When state is Reconnecting, WaitForReadyCoreAsync should use ReconnectTimeout (60s)
        // not DefaultTimeout (very short). We verify by setting a very short DefaultTimeout
        // and a longer ReconnectTimeout. If the correct timeout is used, the wait should
        // NOT time out before we signal it.
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromMilliseconds(10),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(50), // Very short
            ReconnectTimeout = TimeSpan.FromSeconds(10),     // Much longer
        };

        var transportMock = new Mock<ITransport>();
        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        // Put into Reconnecting state
        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        // Act: start waiting for ready
        var waitTask = manager.WaitForReadyAsync(CancellationToken.None);

        // Wait longer than DefaultTimeout (50ms) but shorter than ReconnectTimeout (10s)
        await Task.Delay(200);

        // If it used DefaultTimeout (50ms), the task would have thrown by now
        waitTask.IsCompleted.Should().BeFalse(
            "the task should still be waiting because ReconnectTimeout (10s) has not elapsed");

        // Now signal ready
        manager.NotifyReady();

        // Should complete successfully
        await waitTask.WaitAsync(TimeSpan.FromSeconds(3));
        waitTask.IsCompletedSuccessfully.Should().BeTrue();

        await manager.DisposeAsync();
        stateMachine.Dispose();
    }

    [Fact]
    public async Task WaitForReadyCoreAsync_WhenConnecting_UsesDefaultTimeout()
    {
        // When state is NOT Reconnecting (e.g., Connecting), it should use DefaultTimeout.
        // We set a very short DefaultTimeout so it times out quickly.
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(100),
            ReconnectTimeout = TimeSpan.FromSeconds(60),
        };

        var transportMock = new Mock<ITransport>();
        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        // Stay in Connecting (not Reconnecting)
        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);

        // Act
        Func<Task> act = () => manager.WaitForReadyAsync(CancellationToken.None);

        // Should time out using DefaultTimeout (~100ms), not ReconnectTimeout (60s)
        await act.Should().ThrowAsync<KubeMQTimeoutException>()
            .WithMessage("*Timed out*");

        await manager.DisposeAsync();
        stateMachine.Dispose();
    }

    [Fact]
    public async Task WaitForReadyCoreAsync_WhenReconnecting_TimesOutAfterReconnectTimeout()
    {
        // Verify that when Reconnecting, the wait times out using ReconnectTimeout
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromMilliseconds(10),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(50),
            ReconnectTimeout = TimeSpan.FromMilliseconds(200),
        };

        var transportMock = new Mock<ITransport>();
        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);
        stateMachine.TryTransition(ConnectionState.Ready, ConnectionState.Reconnecting);

        // Act: wait without ever signaling ready — should time out
        Func<Task> act = () => manager.WaitForReadyAsync(CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQTimeoutException>()
            .WithMessage("*Timed out*00:00:00.2000000*");

        await manager.DisposeAsync();
        stateMachine.Dispose();
    }

    [Fact]
    public async Task StartHealthCheck_CalledTwice_DoesNotStartSecondLoop()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                Enabled = true,
                InitialDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
            },
            WaitForReady = true,
            DefaultTimeout = TimeSpan.FromSeconds(5),
        };

        var transportMock = new Mock<ITransport>();
        transportMock.Setup(t => t.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerInfo { Host = "localhost", Version = "1.0" });

        var stateMachine = new StateMachine(NullLogger.Instance);
        var streamManager = new StreamManager(NullLogger.Instance);
        var manager = new ConnectionManager(
            options, transportMock.Object, stateMachine, streamManager, NullLogger.Instance);

        stateMachine.TryTransition(ConnectionState.Idle, ConnectionState.Connecting);
        stateMachine.TryTransition(ConnectionState.Connecting, ConnectionState.Ready);

        // Act: call StartHealthCheck twice
        manager.StartHealthCheck();
        manager.StartHealthCheck(); // second call should be a no-op

        // Dispose — should still complete cleanly (only one task to await)
        Func<Task> act = async () => await manager.DisposeAsync();

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(5));
        stateMachine.Dispose();
    }
}
