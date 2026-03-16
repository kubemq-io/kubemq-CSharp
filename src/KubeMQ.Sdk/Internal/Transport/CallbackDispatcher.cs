using System.Threading.Channels;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Internal.Logging;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Manages the pipeline between gRPC stream reads and user callback dispatch.
/// Uses a bounded <see cref="Channel{T}"/> for backpressure and a <see cref="SemaphoreSlim"/> for
/// concurrency control. Retained as an internal utility for potential future callback-based
/// subscription overloads; not wired into <c>IAsyncEnumerable</c> subscription methods.
/// </summary>
internal sealed class CallbackDispatcher<T> : IAsyncDisposable
{
    private readonly Channel<T> _channel;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly InFlightCallbackTracker _tracker;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _dispatchCts = new();
    private Task? _dispatchTask;
    private volatile bool _disposed;

    internal CallbackDispatcher(
        SubscriptionOptions options,
        InFlightCallbackTracker tracker,
        ILogger logger)
    {
        _tracker = tracker;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(
            options.MaxConcurrentCallbacks,
            options.MaxConcurrentCallbacks);
        _channel = Channel.CreateBounded<T>(
            new BoundedChannelOptions(options.CallbackBufferSize)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = options.MaxConcurrentCallbacks == 1,
                SingleWriter = true,
            });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _channel.Writer.TryComplete();
        await _dispatchCts.CancelAsync().ConfigureAwait(false);

        if (_dispatchTask != null)
        {
            try
            {
                await _dispatchTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        _dispatchCts.Dispose();
        _concurrencyLimiter.Dispose();
    }

    /// <summary>
    /// Enqueue a message from the gRPC stream reader.
    /// Blocks (async) if the buffer is full — applies backpressure to the gRPC stream.
    /// </summary>
    internal async ValueTask EnqueueAsync(T item, CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(item, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Start the dispatch loop that reads from the channel and invokes the handler.
    /// </summary>
    internal void StartDispatching(
        Func<T, CancellationToken, Task> handler,
        CancellationToken externalCt)
    {
        _dispatchTask = Task.Run(
            () => DispatchLoopAsync(handler, externalCt),
            _dispatchCts.Token);
    }

    /// <summary>
    /// Signal that no more items will be enqueued.
    /// </summary>
    internal void Complete()
    {
        _channel.Writer.TryComplete();
    }

    private async Task DispatchLoopAsync(
        Func<T, CancellationToken, Task> handler,
        CancellationToken externalCt)
    {
        using var linkedCts = CancellationTokenSource
            .CreateLinkedTokenSource(externalCt, _dispatchCts.Token);
        CancellationToken ct = linkedCts.Token;

        try
        {
            await foreach (T item in _channel.Reader
                .ReadAllAsync(ct)
                .ConfigureAwait(false))
            {
                await _concurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);

                _ = Task.Run(
                    async () =>
                    {
                        long callbackId = _tracker.TrackStart();
                        try
                        {
                            await handler(item, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            // Expected during shutdown
                        }
                        catch (Exception ex)
                        {
                            Log.CallbackError(_logger, ex);
                        }
                        finally
                        {
                            _tracker.TrackComplete(callbackId);
                            _concurrencyLimiter.Release();
                        }
                    },
                    CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
    }
}
