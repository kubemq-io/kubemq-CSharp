// True token-bucket rate limiter with 1-second burst capacity.
// Spec Section 4.1: +/-5% accuracy over 10s windows.

namespace KubeMQ.Burnin;

/// <summary>
/// Token-bucket rate limiter. Bucket size = 1 second of tokens (burst capacity).
/// Uses async wait to avoid spinning. Thread-safe via lock + SemaphoreSlim for async wait.
/// </summary>
public sealed class RateLimiter : IDisposable
{
    private readonly double _rate;
    private readonly double _maxTokens;
    private double _tokens;
    private long _lastRefillTicks;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Create a rate limiter with the given rate in messages per second.
    /// Rate of 0 or less means unlimited (WaitAsync returns immediately).
    /// </summary>
    public RateLimiter(double rate)
    {
        _rate = rate;
        _maxTokens = Math.Max(rate, 1.0);
        _tokens = _maxTokens;
        _lastRefillTicks = Environment.TickCount64;
    }

    /// <summary>
    /// Wait until a token is available or the cancellation token fires.
    /// Returns true if a token was consumed, false if cancelled.
    /// </summary>
    public async Task<bool> WaitAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;
        if (_rate <= 0) return true; // unlimited

        while (!ct.IsCancellationRequested)
        {
            double waitMs;
            lock (_lock)
            {
                Refill();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }
                // Calculate how long to wait for next token.
                waitMs = Math.Max(1.0, ((1.0 - _tokens) / _rate) * 1000.0);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(waitMs), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            // After delay, try again (refill + consume in the next loop iteration).
            lock (_lock)
            {
                Refill();
                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }
            }
        }

        return false;
    }

    private void Refill()
    {
        long now = Environment.TickCount64;
        double elapsedSec = (now - _lastRefillTicks) / 1000.0;
        _tokens = Math.Min(_maxTokens, _tokens + elapsedSec * _rate);
        _lastRefillTicks = now;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
