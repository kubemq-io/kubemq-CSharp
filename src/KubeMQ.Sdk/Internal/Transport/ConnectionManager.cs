using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Logging;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Orchestrates reconnection with exponential backoff, message buffering,
/// subscription recovery, and WaitForReady blocking semantics.
/// </summary>
internal sealed class ConnectionManager : IAsyncDisposable
{
    private readonly KubeMQClientOptions _options;
    private readonly ITransport _transport;
    private readonly StateMachine _stateMachine;
    private readonly ReconnectBuffer _buffer;
    private readonly StreamManager _streamManager;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly object _readyLock = new();
    private Task? _reconnectTask;
    private volatile TaskCompletionSource _readyTcs = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionManager"/> class.
    /// </summary>
    /// <param name="options">Client configuration.</param>
    /// <param name="transport">The gRPC transport.</param>
    /// <param name="stateMachine">Shared state machine.</param>
    /// <param name="streamManager">Subscription tracker.</param>
    /// <param name="logger">Logger instance.</param>
    internal ConnectionManager(
        KubeMQClientOptions options,
        ITransport transport,
        StateMachine stateMachine,
        StreamManager streamManager,
        ILogger logger)
    {
        _options = options;
        _transport = transport;
        _stateMachine = stateMachine;
        _streamManager = streamManager;
        _logger = logger;
        _buffer = new ReconnectBuffer(options.Reconnect);
    }

    /// <summary>
    /// Gets or sets the callback invoked when the connection manager transitions state.
    /// The owning client uses this to fire <c>StateChanged</c> events.
    /// </summary>
    internal Action<ConnectionState, ConnectionState, Exception?>? StateTransitionCallback { get; set; }

    /// <summary>
    /// Disposes the connection manager, cancelling any active reconnection.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        if (_reconnectTask != null)
        {
            try
            {
                await _reconnectTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (KubeMQConnectionException)
            {
            }
        }

        int discarded = _buffer.DiscardAll();
        if (discarded > 0)
        {
            Log.BufferDiscardedOnClose(_logger, discarded);
        }

        _shutdownCts.Dispose();
        _buffer.Dispose();
    }

    /// <summary>
    /// Called when a connection loss is detected. Transitions to <c>Reconnecting</c>
    /// and starts the background reconnection loop.
    /// </summary>
    /// <param name="ex">The exception that caused the connection loss.</param>
    internal void OnConnectionLost(Exception? ex)
    {
        if (!_options.Reconnect.Enabled ||
            _stateMachine.Current == ConnectionState.Disposed)
        {
            return;
        }

        if (_stateMachine.TryTransition(
            ConnectionState.Connected, ConnectionState.Reconnecting))
        {
            lock (_readyLock)
            {
                _readyTcs = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            StateTransitionCallback?.Invoke(
                ConnectionState.Connected, ConnectionState.Reconnecting, ex);
            _reconnectTask = Task.Run(
                () => ReconnectLoopAsync(_shutdownCts.Token));
        }
    }

    /// <summary>
    /// Buffers a message during the <c>Reconnecting</c> state.
    /// </summary>
    /// <param name="message">The message to buffer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the async operation.</returns>
    internal async ValueTask BufferOrFailAsync(
        BufferedMessage message, CancellationToken ct)
    {
        if (_stateMachine.Current != ConnectionState.Reconnecting)
        {
            throw new InvalidOperationException("Not in reconnecting state");
        }

        await _buffer.EnqueueAsync(message, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Blocks until the connection is <c>Connected</c>, or fails immediately
    /// if <see cref="KubeMQClientOptions.WaitForReady"/> is false.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the connection is ready.</returns>
    internal Task WaitForReadyAsync(CancellationToken ct)
    {
        if (_stateMachine.Current == ConnectionState.Connected)
        {
            return Task.CompletedTask;
        }

        if (!_options.WaitForReady)
        {
            throw new KubeMQConnectionException(
                "Client is not connected and WaitForReady is disabled");
        }

        return WaitForReadyCoreAsync(ct);
    }

    /// <summary>
    /// Notifies the ready TCS so that WaitForReady callers unblock.
    /// </summary>
    internal void NotifyReady()
    {
        _readyTcs.TrySetResult();
    }

    /// <summary>
    /// Resets the ready TCS so that WaitForReady callers block again.
    /// </summary>
    internal void ResetReady()
    {
        lock (_readyLock)
        {
            _readyTcs = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>
    /// Discards all buffered messages.
    /// </summary>
    /// <returns>The number of discarded messages.</returns>
    internal int DiscardBuffer()
    {
        return _buffer.DiscardAll();
    }

    /// <summary>
    /// Flushes all buffered messages by sending them through the transport.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the flush operation.</returns>
    internal async Task FlushBufferAsync(CancellationToken ct)
    {
        await _buffer.FlushAsync(
            async (msg, token) =>
            {
                await _transport.SendBufferedAsync(msg, token).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculates the backoff delay for a reconnection attempt using exponential backoff
    /// with full jitter.
    /// </summary>
    /// <param name="attempt">The 1-based attempt number.</param>
    /// <returns>The computed delay.</returns>
    internal TimeSpan CalculateBackoffDelay(int attempt)
    {
        double baseMs = _options.Reconnect.InitialDelay.TotalMilliseconds;
        double maxMs = _options.Reconnect.MaxDelay.TotalMilliseconds;
        double multiplier = _options.Reconnect.BackoffMultiplier;

        double exponentialMs = Math.Min(
            baseMs * Math.Pow(multiplier, attempt - 1), maxMs);

        double jitteredMs = Random.Shared.NextDouble() * exponentialMs;

        return TimeSpan.FromMilliseconds(jitteredMs);
    }

    internal async Task ReconnectLoopAsync(CancellationToken ct)
    {
        int attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            attempt++;

            try
            {
                await _transport.ConnectAsync(ct).ConfigureAwait(false);

                Log.Reconnected(_logger, _options.Address, attempt);

                if (_stateMachine.TryTransition(
                    ConnectionState.Reconnecting, ConnectionState.Connected))
                {
                    StateTransitionCallback?.Invoke(
                        ConnectionState.Reconnecting, ConnectionState.Connected, null);
                }

                _readyTcs.TrySetResult();

                await FlushBufferAsync(ct).ConfigureAwait(false);
                await _streamManager.ResubscribeAllAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (_options.Reconnect.MaxAttempts > 0 &&
                    attempt >= _options.Reconnect.MaxAttempts)
                {
                    Log.ReconnectExhausted(_logger, _options.Address, attempt);

                    ConnectionState previous = _stateMachine.ForceDisposed();
                    StateTransitionCallback?.Invoke(previous, ConnectionState.Disposed, ex);

                    int discarded = _buffer.DiscardAll();
                    if (discarded > 0)
                    {
                        Log.BufferDiscardedOnClose(_logger, discarded);
                    }

                    throw new KubeMQConnectionException(
                        $"Failed to reconnect to {_options.Address} after {attempt} attempts", ex);
                }

                TimeSpan delay = CalculateBackoffDelay(attempt);
                Log.ReconnectAttempt(_logger, _options.Address, attempt, delay);

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task WaitForReadyCoreAsync(CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_options.DefaultTimeout);

        try
        {
            await _readyTcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw new KubeMQTimeoutException(
                $"Timed out waiting for connection ({_options.DefaultTimeout})");
        }
    }
}
