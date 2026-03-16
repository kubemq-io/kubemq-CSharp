using System.Diagnostics;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Telemetry;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// Orchestrates retry logic with exponential backoff, jitter, and concurrency throttling.
/// </summary>
internal sealed partial class RetryHandler : IDisposable
{
    private readonly RetryPolicy _policy;
    private readonly SemaphoreSlim? _throttle;
    private readonly ILogger _logger;
    private bool _disposed;

    internal RetryHandler(RetryPolicy retryPolicy, ILogger retryLogger)
    {
        _policy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = retryLogger ?? throw new ArgumentNullException(nameof(retryLogger));

        _throttle = _policy.MaxConcurrentRetries > 0
            ? new SemaphoreSlim(_policy.MaxConcurrentRetries, _policy.MaxConcurrentRetries)
            : null;
    }

    /// <summary>
    /// Disposes the throttle semaphore. Must be called only after all in-flight
    /// operations have completed.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _throttle?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Executes <paramref name="operation"/> with retry according to the configured policy.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="operationName">Name for logging (e.g., "PublishEvent").</param>
    /// <param name="channel">Target channel for error context.</param>
    /// <param name="isSafeToRetryOnTimeout">
    /// False for non-idempotent operations (Queue Send, Command/Query).
    /// When false, DEADLINE_EXCEEDED is returned immediately without retry.
    /// </param>
    /// <param name="cancellationToken">Caller's cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    internal async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        string? channel,
        bool isSafeToRetryOnTimeout,
        CancellationToken cancellationToken)
    {
        if (!_policy.Enabled || _policy.MaxRetries == 0)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        var sw = Stopwatch.StartNew();
        Exception? lastException = null;
        int attempt = 0;
        bool throttleHeld = false;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    T result = await operation(cancellationToken).ConfigureAwait(false);
                    return result;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (KubeMQException ex) when (ShouldRetry(ex, isSafeToRetryOnTimeout))
                {
                    lastException = ex;

                    if (attempt > _policy.MaxRetries)
                    {
                        break;
                    }

                    if (ex.ErrorCode == KubeMQErrorCode.Unknown && attempt > 1)
                    {
                        break;
                    }

                    if (_throttle is not null && !throttleHeld)
                    {
                        if (!await _throttle.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
                        {
                            throw new KubeMQRetryExhaustedException(
                                $"{operationName} retry throttled on channel \"{channel}\": " +
                                $"concurrent retry limit ({_policy.MaxConcurrentRetries}) reached. " +
                                "Suggestion: Reduce request rate or increase MaxConcurrentRetries.",
                                attempt,
                                sw.Elapsed,
                                ex)
                            { Operation = operationName, Channel = channel };
                        }

                        throttleHeld = true;
                    }

                    var delay = CalculateDelay(attempt);

                    string errorType = KubeMQMetrics.MapErrorType(ex.Category);
                    KubeMQMetrics.RecordRetryAttempt(operationName, errorType);

                    LogRetryAttempt(
                        _logger,
                        attempt,
                        _policy.MaxRetries,
                        operationName,
                        channel,
                        delay.TotalMilliseconds,
                        ex.ErrorCode);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (KubeMQException)
                {
                    throw;
                }
            }

            sw.Stop();
            if (lastException is KubeMQException lastKe)
            {
                KubeMQMetrics.RecordRetryExhausted(operationName, KubeMQMetrics.MapErrorType(lastKe.Category));
            }

            throw new KubeMQRetryExhaustedException(
                $"{operationName} failed on channel \"{channel}\": all {_policy.MaxRetries} retry attempts " +
                $"exhausted over {sw.Elapsed.TotalSeconds:F1}s. " +
                $"Suggestion: Check server connectivity. Last error: {lastException?.Message}",
                attempt,
                sw.Elapsed,
                lastException!)
            { Operation = operationName, Channel = channel };
        }
        finally
        {
            if (throttleHeld)
            {
                _throttle!.Release();
            }
        }
    }

    /// <summary>Void overload for operations that return nothing.</summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="operationName">Name for logging.</param>
    /// <param name="channel">Target channel for error context.</param>
    /// <param name="isSafeToRetryOnTimeout">False for non-idempotent operations.</param>
    /// <param name="cancellationToken">Caller's cancellation token.</param>
    internal async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string? channel,
        bool isSafeToRetryOnTimeout,
        CancellationToken cancellationToken)
    {
        await ExecuteWithRetryAsync(
            async ct =>
            {
                await operation(ct).ConfigureAwait(false);
                return true;
            },
            operationName,
            channel,
            isSafeToRetryOnTimeout,
            cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Debug,
        Message = "Retry attempt {Attempt}/{MaxRetries} for {Operation} on channel \"{Channel}\" after {DelayMs}ms. Error: {ErrorCode}")]
    private static partial void LogRetryAttempt(
        ILogger logger,
        int attempt,
        int maxRetries,
        string operation,
        string? channel,
        double delayMs,
        KubeMQErrorCode errorCode);

    private static bool ShouldRetry(KubeMQException ex, bool isSafeToRetryOnTimeout)
    {
        if (!ex.IsRetryable)
        {
            return false;
        }

        if (ex.Category == KubeMQErrorCategory.Timeout && !isSafeToRetryOnTimeout)
        {
            return false;
        }

        return true;
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        double baseMs = _policy.InitialBackoff.TotalMilliseconds;
        double maxMs = _policy.MaxBackoff.TotalMilliseconds;
        double exponential = Math.Min(maxMs, baseMs * Math.Pow(_policy.BackoffMultiplier, attempt - 1));

        return _policy.JitterMode switch
        {
            JitterMode.Full => TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * exponential),
            JitterMode.Equal => TimeSpan.FromMilliseconds(
                (exponential / 2.0) + (Random.Shared.NextDouble() * (exponential / 2.0))),
            JitterMode.None => TimeSpan.FromMilliseconds(exponential),
            _ => TimeSpan.FromMilliseconds(exponential),
        };
    }
}
