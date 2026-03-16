namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Tracks the number of in-flight callbacks to support graceful shutdown.
/// Uses a SemaphoreSlim-based signaling pattern that can be re-signaled,
/// avoiding the one-shot TaskCompletionSource race condition where a TCS
/// fires prematurely if a callback completes before drain begins.
/// </summary>
internal sealed class InFlightCallbackTracker : IDisposable
{
    private readonly SemaphoreSlim _zeroSignal = new(0, 1);
    private int _activeCount;

    /// <summary>
    /// Gets the number of currently executing callbacks.
    /// </summary>
    internal int ActiveCount => Volatile.Read(ref _activeCount);

    /// <inheritdoc />
    public void Dispose()
    {
        _zeroSignal.Dispose();
    }

    /// <summary>
    /// Record that a callback has started processing.
    /// Returns a tracking ID (unused in current impl but available for future diagnostics).
    /// </summary>
    internal long TrackStart()
    {
        Interlocked.Increment(ref _activeCount);
        return 0;
    }

    /// <summary>
    /// Record that a callback has finished processing.
    /// Signals the drain waiter when the count reaches zero.
    /// </summary>
    internal void TrackComplete(long callbackId)
    {
        if (Interlocked.Decrement(ref _activeCount) == 0)
        {
            try
            {
                if (_zeroSignal.CurrentCount == 0)
                {
                    _zeroSignal.Release();
                }
            }
            catch (SemaphoreFullException)
            {
                // Benign race: another thread already signaled
            }
        }
    }

    /// <summary>
    /// Wait for all tracked callbacks to complete, with timeout.
    /// Re-checks the count in a loop to handle the race where a new
    /// callback starts between the signal and the drain check.
    /// </summary>
    internal async Task WaitForAllAsync(CancellationToken cancellationToken)
    {
        while (Volatile.Read(ref _activeCount) > 0)
        {
            await _zeroSignal.WaitAsync(TimeSpan.FromMilliseconds(100), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
