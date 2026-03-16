using System.Diagnostics.Metrics;
using FluentAssertions;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Protocol;
using KubeMQ.Sdk.Internal.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Protocol;

public class RetryHandlerTests : IDisposable
{
    private readonly RetryPolicy _fastPolicy = new()
    {
        Enabled = true,
        MaxRetries = 3,
        InitialBackoff = TimeSpan.FromMilliseconds(50),
        MaxBackoff = TimeSpan.FromSeconds(1),
        BackoffMultiplier = 1.5,
        JitterMode = JitterMode.None,
        MaxConcurrentRetries = 0,
    };

    private readonly RetryHandler _handler;

    public RetryHandlerTests()
    {
        _handler = new RetryHandler(_fastPolicy, NullLogger.Instance);
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    [Fact]
    public async Task FirstAttemptSucceeds_NoRetry()
    {
        int calls = 0;

        var result = await _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                return Task.FromResult("ok");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        result.Should().Be("ok");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task SecondAttemptSucceeds_AfterRetryableError()
    {
        int calls = 0;

        var result = await _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new KubeMQConnectionException("transient error");
                }

                return Task.FromResult("recovered");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        result.Should().Be("recovered");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task AllRetriesExhausted_ThrowsRetryExhaustedException()
    {
        int calls = 0;

        var act = () => _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                throw new KubeMQConnectionException("always fails");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<KubeMQRetryExhaustedException>();
        ex.Which.ErrorCode.Should().Be(KubeMQErrorCode.RetryExhausted);
        ex.Which.InnerException.Should().BeOfType<KubeMQConnectionException>();
        ex.Which.Operation.Should().Be("TestOp");
        ex.Which.Channel.Should().Be("test-channel");
        calls.Should().Be(1 + _fastPolicy.MaxRetries);
    }

    [Fact]
    public async Task NonRetryableError_NoRetry_ThrowsImmediately()
    {
        int calls = 0;

        var act = () => _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                throw new KubeMQOperationException("fatal error");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("fatal error");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task DeadlineExceeded_NonIdempotent_NoRetry()
    {
        int calls = 0;

        var act = () => _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                throw new KubeMQTimeoutException("deadline exceeded");
            },
            "TestOp",
            "test-channel",
            isSafeToRetryOnTimeout: false,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQTimeoutException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task DeadlineExceeded_Idempotent_Retries()
    {
        int calls = 0;

        var result = await _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new KubeMQTimeoutException("deadline exceeded");
                }

                return Task.FromResult("ok-after-timeout");
            },
            "TestOp",
            "test-channel",
            isSafeToRetryOnTimeout: true,
            CancellationToken.None);

        result.Should().Be("ok-after-timeout");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task CancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _handler.ExecuteWithRetryAsync<string>(
            ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult("unreachable");
            },
            "TestOp",
            "test-channel",
            true,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RetryDisabled_NoRetry()
    {
        var policy = new RetryPolicy
        {
            Enabled = false,
            MaxRetries = 3,
            InitialBackoff = TimeSpan.FromMilliseconds(50),
            MaxBackoff = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 1.5,
        };
        using var handler = new RetryHandler(policy, NullLogger.Instance);
        int calls = 0;

        var act = () => handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                throw new KubeMQConnectionException("transient");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQConnectionException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task MaxRetriesZero_NoRetry()
    {
        var policy = new RetryPolicy
        {
            Enabled = true,
            MaxRetries = 0,
            InitialBackoff = TimeSpan.FromMilliseconds(50),
            MaxBackoff = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 1.5,
        };
        using var handler = new RetryHandler(policy, NullLogger.Instance);
        int calls = 0;

        var act = () => handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                throw new KubeMQConnectionException("transient");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQConnectionException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task VoidOverload_Works()
    {
        int calls = 0;

        await _handler.ExecuteWithRetryAsync(
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        calls.Should().Be(1);
    }

    [Fact]
    public async Task RetryMetrics_RecordsAttempts()
    {
        long retryAttemptCount = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricRetryAttempts)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            Interlocked.Add(ref retryAttemptCount, measurement);
        });
        listener.Start();

        long baseline = Interlocked.Read(ref retryAttemptCount);

        int calls = 0;
        var result = await _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                if (calls <= 2)
                {
                    throw new KubeMQConnectionException("transient");
                }

                return Task.FromResult("done");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        result.Should().Be("done");

        long delta = Interlocked.Read(ref retryAttemptCount) - baseline;
        delta.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task RetryExhausted_RecordsMetric()
    {
        long exhaustedCount = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "KubeMQ.Sdk" &&
                instrument.Name == SemanticConventions.MetricRetryExhausted)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
        {
            Interlocked.Add(ref exhaustedCount, measurement);
        });
        listener.Start();

        long baseline = Interlocked.Read(ref exhaustedCount);

        var act = () => _handler.ExecuteWithRetryAsync<string>(
            _ => throw new KubeMQConnectionException("always fails"),
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQRetryExhaustedException>();

        long delta = Interlocked.Read(ref exhaustedCount) - baseline;
        delta.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task VoidOverload_WithRetry_Succeeds()
    {
        int calls = 0;

        await _handler.ExecuteWithRetryAsync(
            _ =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new KubeMQConnectionException("transient");
                }

                return Task.CompletedTask;
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        calls.Should().Be(2);
    }

    [Fact]
    public async Task VoidOverload_ExhaustsRetries_ThrowsRetryExhausted()
    {
        int calls = 0;

        var act = () => _handler.ExecuteWithRetryAsync(
            _ =>
            {
                calls++;
                throw new KubeMQConnectionException("always fails");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQRetryExhaustedException>();
        calls.Should().Be(1 + _fastPolicy.MaxRetries);
    }

    [Fact]
    public async Task UnknownError_RetriesOnceAndBreaks()
    {
        int calls = 0;

        var act = () => _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                throw new KubeMQOperationException(
                    "unknown error",
                    KubeMQErrorCode.Unknown,
                    KubeMQErrorCategory.Transient,
                    isRetryable: true);
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQRetryExhaustedException>();
        calls.Should().Be(2, "Unknown errors should retry once then break");
    }

    [Fact]
    public async Task CancellationDuringDelay_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        int calls = 0;

        var act = () => _handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                calls++;
                if (calls == 1)
                {
                    cts.CancelAfter(TimeSpan.FromMilliseconds(10));
                    throw new KubeMQConnectionException("transient");
                }

                return Task.FromResult("unreachable");
            },
            "TestOp",
            "test-channel",
            true,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullPolicy_ThrowsArgumentNull()
    {
        var act = () => new RetryHandler(null!, NullLogger.Instance);

        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("retryPolicy");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        var act = () => new RetryHandler(_fastPolicy, null!);

        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("retryLogger");
    }

    [Fact]
    public async Task ThrottleLimit_ThrowsRetryExhausted_WhenConcurrencyLimitReached()
    {
        var policy = new RetryPolicy
        {
            Enabled = true,
            MaxRetries = 3,
            InitialBackoff = TimeSpan.FromSeconds(2),
            MaxBackoff = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 1.5,
            JitterMode = JitterMode.None,
            MaxConcurrentRetries = 1,
        };
        using var handler = new RetryHandler(policy, NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var firstRetryStarted = new TaskCompletionSource();
        int calls1 = 0;

        var task1 = Task.Run(async () =>
            await handler.ExecuteWithRetryAsync<string>(
                _ =>
                {
                    calls1++;
                    if (calls1 == 1)
                    {
                        firstRetryStarted.TrySetResult();
                        throw new KubeMQConnectionException("transient");
                    }

                    return Task.FromResult("ok");
                },
                "Op1",
                "ch1",
                true,
                cts.Token));

        await firstRetryStarted.Task;
        await Task.Delay(200);

        var act = () => handler.ExecuteWithRetryAsync<string>(
            _ => throw new KubeMQConnectionException("transient"),
            "Op2",
            "ch2",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQRetryExhaustedException>()
            .Where(ex => ex.Message.Contains("throttled"));

        await task1;
    }

    [Fact]
    public async Task BackoffDelay_IncreasesExponentially_WithJitterNone()
    {
        var policy = new RetryPolicy
        {
            Enabled = true,
            MaxRetries = 3,
            InitialBackoff = TimeSpan.FromMilliseconds(100),
            MaxBackoff = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 2.0,
            JitterMode = JitterMode.None,
            MaxConcurrentRetries = 0,
        };
        using var handler = new RetryHandler(policy, NullLogger.Instance);

        var timestamps = new List<DateTimeOffset>();

        var act = () => handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                timestamps.Add(DateTimeOffset.UtcNow);
                throw new KubeMQConnectionException("transient");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQRetryExhaustedException>();

        timestamps.Count.Should().Be(1 + policy.MaxRetries);

        var delay1 = (timestamps[2] - timestamps[1]).TotalMilliseconds;
        var delay0 = (timestamps[1] - timestamps[0]).TotalMilliseconds;
        delay1.Should().BeGreaterThan(delay0 * 1.3,
            "second delay should be noticeably longer than first due to exponential backoff");
    }

    [Fact]
    public async Task BackoffDelay_CappedByMaxBackoff()
    {
        var policy = new RetryPolicy
        {
            Enabled = true,
            MaxRetries = 3,
            InitialBackoff = TimeSpan.FromSeconds(1),
            MaxBackoff = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 3.0,
            JitterMode = JitterMode.None,
            MaxConcurrentRetries = 0,
        };
        using var handler = new RetryHandler(policy, NullLogger.Instance);

        var timestamps = new List<DateTimeOffset>();

        var act = () => handler.ExecuteWithRetryAsync<string>(
            _ =>
            {
                timestamps.Add(DateTimeOffset.UtcNow);
                throw new KubeMQConnectionException("transient");
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<KubeMQRetryExhaustedException>();

        for (int i = 1; i < timestamps.Count; i++)
        {
            var delay = (timestamps[i] - timestamps[i - 1]).TotalMilliseconds;
            delay.Should().BeLessThan(2000,
                "delay should be capped by MaxBackoff of 1 second (with generous tolerance)");
        }
    }

    [Fact]
    public async Task RetryExhausted_ContainsAttemptCount_And_Duration()
    {
        var act = () => _handler.ExecuteWithRetryAsync<string>(
            _ => throw new KubeMQConnectionException("always fails"),
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<KubeMQRetryExhaustedException>();
        ex.Which.AttemptCount.Should().BeGreaterThan(0);
        ex.Which.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        ex.Which.LastException.Should().BeOfType<KubeMQConnectionException>();
    }

    [Fact]
    public async Task GenericOverload_ReturnsCorrectValue_ThroughRetries()
    {
        int calls = 0;

        var result = await _handler.ExecuteWithRetryAsync(
            _ =>
            {
                calls++;
                if (calls <= 2)
                {
                    throw new KubeMQConnectionException("transient");
                }

                return Task.FromResult(42);
            },
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        result.Should().Be(42);
        calls.Should().Be(3);
    }

    [Fact]
    public async Task DisposeHandler_ThenRetryWithThrottle_ThrowsObjectDisposed()
    {
        var policy = new RetryPolicy
        {
            Enabled = true,
            MaxRetries = 3,
            InitialBackoff = TimeSpan.FromMilliseconds(50),
            MaxBackoff = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 1.5,
            JitterMode = JitterMode.None,
            MaxConcurrentRetries = 5,
        };
        var handler = new RetryHandler(policy, NullLogger.Instance);
        handler.Dispose();

        var act = () => handler.ExecuteWithRetryAsync<string>(
            _ => throw new KubeMQConnectionException("transient"),
            "TestOp",
            "test-channel",
            true,
            CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
