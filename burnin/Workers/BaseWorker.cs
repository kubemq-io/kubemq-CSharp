// BaseWorker: shared state, counters, 2-phase shutdown via CancellationTokenSource, dual tracking.
// All recording methods are thread-safe for multi-producer/consumer concurrency.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace KubeMQ.Burnin.Workers;

/// <summary>
/// Abstract base class for all 6 messaging pattern workers.
/// Provides 2-phase CTS shutdown, all counters, tracking, rate limiting,
/// latency measurement, and backpressure support.
/// Uses existing types: Tracker, PeakRateTracker, LatencyAccumulator,
/// TimestampStore, RateLimiter, SizeDistribution from the shared project.
/// </summary>
public abstract class BaseWorker : IDisposable
{
    public string Pattern { get; }
    public BurninConfig Config { get; }
    public string ChannelName { get; }

    // 2-phase shutdown (Go#2): producer stops first, consumer drains, then stops.
    protected readonly CancellationTokenSource ProducerCts = new();
    protected readonly CancellationTokenSource ConsumerCts = new();

    // Tracking
    public readonly Tracker Tracker;
    public readonly LatencyAccumulator LatencyAccum = new();
    public readonly LatencyAccumulator RpcLatencyAccum = new();
    public readonly PeakRateTracker PeakRate = new();
    public readonly TimestampStore TsStore = new();

    // Rate limiting
    protected readonly RateLimiter Limiter;
    protected readonly SizeDistribution? SizeDist;

    // In-process counters (dual tracking, Go#5) -- use Interlocked for thread safety.
    private long _sent;
    private long _received;
    private long _corrupted;
    private long _errors;
    private long _reconnections;
    private long _rpcSuccess;
    private long _rpcTimeout;
    private long _rpcError;

    public long Sent => Interlocked.Read(ref _sent);
    public long Received => Interlocked.Read(ref _received);
    public long Corrupted => Interlocked.Read(ref _corrupted);
    public long Errors => Interlocked.Read(ref _errors);
    public long Reconnections => Interlocked.Read(ref _reconnections);
    public long RpcSuccess => Interlocked.Read(ref _rpcSuccess);
    public long RpcTimeout => Interlocked.Read(ref _rpcTimeout);
    public long RpcError => Interlocked.Read(ref _rpcError);

    // Downtime tracking
    private long _downtimeStartTicks; // 0 means not in downtime
    private double _downtimeTotal;
    private readonly object _downtimeLock = new();

    // Reconnection duplicate cooldown (Go lesson)
    private volatile bool _recentlyReconnected;
    private int _reconnDupCooldown;

    // Backpressure warning (GAP-N)
    private volatile bool _backpressureLogged;

    // Per-consumer message counts for group balance (GAP-17)
    public readonly ConcurrentDictionary<string, long> ConsumerCounts = new();

    protected BaseWorker(string pattern, BurninConfig config, string channelName, double rate)
    {
        Pattern = pattern;
        Config = config;
        ChannelName = channelName;
        Tracker = new Tracker(config.Message.ReorderWindow);
        Limiter = new RateLimiter(rate);
        SizeDist = config.Message.SizeMode == "distribution"
            ? new SizeDistribution(config.Message.SizeDistribution)
            : null;
    }

    /// <summary>
    /// Get the next message size based on configuration.
    /// </summary>
    public int MessageSize()
    {
        return SizeDist?.SelectSize() ?? Config.Message.SizeBytes;
    }

    /// <summary>
    /// Check if backpressure should be applied. Returns true if producer should pause.
    /// Logs a warning once when entering backpressure state.
    /// </summary>
    public bool BackpressureCheck()
    {
        long lag = Sent - Received;
        bool active = lag > Config.Queue.MaxDepth;
        if (active && !_backpressureLogged)
        {
            Console.Error.WriteLine(
                $"WARNING: {Pattern} producer paused -- consumer lag {lag} exceeds max_depth {Config.Queue.MaxDepth}");
            _backpressureLogged = true;
        }
        if (!active)
        {
            _backpressureLogged = false;
        }
        return active;
    }

    /// <summary>
    /// Wait for rate limiter. Returns false if cancelled.
    /// </summary>
    public Task<bool> WaitForRate(CancellationToken ct)
    {
        return Limiter.WaitAsync(ct);
    }

    /// <summary>
    /// Record a successful send. Call only after the SDK confirms success (Go#7).
    /// </summary>
    public void RecordSend(string producerId, long seq, int byteCount = 0)
    {
        Interlocked.Increment(ref _sent);
        TsStore.Store(producerId, seq);
        PeakRate.Record();
        Metrics.IncSent(Pattern, producerId, byteCount);
    }

    /// <summary>
    /// Record a received message with CRC verification, sequence tracking, and latency measurement.
    /// </summary>
    public void RecordReceive(string consumerId, ReadOnlyMemory<byte> body, string crcTag,
        string producerId, long seq)
    {
        Interlocked.Increment(ref _received);
        ConsumerCounts.AddOrUpdate(consumerId, 1, (_, v) => v + 1);
        Metrics.IncReceived(Pattern, consumerId, body.Length);

        // CRC verification
        if (!Payload.VerifyCrc(body.Span, crcTag))
        {
            Interlocked.Increment(ref _corrupted);
            Metrics.IncCorrupted(Pattern);
            return;
        }

        // Sequence tracking
        var result = Tracker.Record(producerId, seq);
        if (result.IsDuplicate)
        {
            Metrics.IncDuplicated(Pattern);
            if (_recentlyReconnected)
            {
                Metrics.IncReconnDuplicates(Pattern);
                if (Interlocked.Increment(ref _reconnDupCooldown) >= 100)
                {
                    _recentlyReconnected = false;
                    Interlocked.Exchange(ref _reconnDupCooldown, 0);
                }
            }
            return;
        }
        if (result.IsOutOfOrder)
            Metrics.IncOutOfOrder(Pattern);

        // Latency measurement
        long? sendTime = TsStore.LoadAndDelete(producerId, seq);
        if (sendTime.HasValue)
        {
            double latency = TimestampStore.ElapsedSeconds(sendTime.Value);
            LatencyAccum.Record(latency);
            Metrics.ObserveLatency(Pattern, latency);
        }
    }

    /// <summary>
    /// Record an error with the given type.
    /// </summary>
    public void RecordError(string errorType)
    {
        Interlocked.Increment(ref _errors);
        Metrics.IncError(Pattern, errorType);
    }

    /// <summary>
    /// Increment the reconnection counter with duplicate cooldown tracking.
    /// </summary>
    public void IncReconnection()
    {
        Interlocked.Increment(ref _reconnections);
        _recentlyReconnected = true;
        Interlocked.Exchange(ref _reconnDupCooldown, 0);
        Metrics.IncReconnections(Pattern);
    }

    public void IncRpcSuccess()
    {
        Interlocked.Increment(ref _rpcSuccess);
        Metrics.IncRpcResponse(Pattern, "success");
    }

    public void IncRpcTimeout()
    {
        Interlocked.Increment(ref _rpcTimeout);
        Metrics.IncRpcResponse(Pattern, "timeout");
    }

    public void IncRpcError()
    {
        Interlocked.Increment(ref _rpcError);
        Metrics.IncRpcResponse(Pattern, "error");
    }

    /// <summary>
    /// Mark the start of a downtime period.
    /// </summary>
    public void StartDowntime()
    {
        lock (_downtimeLock)
        {
            if (_downtimeStartTicks == 0)
                _downtimeStartTicks = Environment.TickCount64;
        }
    }

    /// <summary>
    /// Mark the end of a downtime period, accumulating elapsed time.
    /// </summary>
    public void StopDowntime()
    {
        lock (_downtimeLock)
        {
            if (_downtimeStartTicks != 0)
            {
                _downtimeTotal += (Environment.TickCount64 - _downtimeStartTicks) / 1000.0;
                _downtimeStartTicks = 0;
            }
        }
    }

    /// <summary>
    /// Get total downtime in seconds (including any currently active downtime).
    /// </summary>
    public double DowntimeSeconds
    {
        get
        {
            lock (_downtimeLock)
            {
                double t = _downtimeTotal;
                if (_downtimeStartTicks != 0)
                    t += (Environment.TickCount64 - _downtimeStartTicks) / 1000.0;
                return t;
            }
        }
    }

    // --- Lifecycle ---

    /// <summary>
    /// Start all consumer tasks/subscriptions. Called before producers.
    /// </summary>
    public abstract Task StartConsumersAsync(KubeMQ.Sdk.Client.KubeMQClient client);

    /// <summary>
    /// Start all producer tasks. Called after consumers are running.
    /// </summary>
    public abstract Task StartProducersAsync(KubeMQ.Sdk.Client.KubeMQClient client);

    /// <summary>
    /// Stop all producers (phase 1 of shutdown).
    /// </summary>
    public virtual void StopProducers()
    {
        if (!ProducerCts.IsCancellationRequested)
            ProducerCts.Cancel();
    }

    /// <summary>
    /// Stop all consumers (phase 2 of shutdown).
    /// </summary>
    public virtual void StopConsumers()
    {
        if (!ConsumerCts.IsCancellationRequested)
            ConsumerCts.Cancel();
    }

    /// <summary>
    /// Reset all counters and tracking after warmup period.
    /// </summary>
    public void ResetAfterWarmup()
    {
        Interlocked.Exchange(ref _sent, 0);
        Interlocked.Exchange(ref _received, 0);
        Interlocked.Exchange(ref _corrupted, 0);
        Interlocked.Exchange(ref _errors, 0);
        Interlocked.Exchange(ref _reconnections, 0);
        Interlocked.Exchange(ref _rpcSuccess, 0);
        Interlocked.Exchange(ref _rpcTimeout, 0);
        Interlocked.Exchange(ref _rpcError, 0);

        lock (_downtimeLock)
        {
            _downtimeTotal = 0;
            _downtimeStartTicks = 0;
        }

        Tracker.Reset();
        LatencyAccum.Reset();
        RpcLatencyAccum.Reset();
        PeakRate.Reset();
        TsStore.Purge(TimeSpan.Zero); // clear warmup entries
        ConsumerCounts.Clear();
    }

    public virtual void Dispose()
    {
        ProducerCts.Dispose();
        ConsumerCts.Dispose();
        Limiter.Dispose();
        GC.SuppressFinalize(this);
    }
}
