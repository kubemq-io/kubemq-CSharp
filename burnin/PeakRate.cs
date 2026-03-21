// Peak rate tracking and HdrHistogram-based latency accumulation.
// Thread-safe with lock.

using HdrHistogram;

namespace KubeMQ.Burnin;

/// <summary>
/// 30-second sliding window rate tracker for actual_rate computation.
/// Thread-safe via lock.
/// </summary>
public sealed class SlidingRateTracker
{
    private const int WindowSize = 30;
    private readonly long[] _buckets = new long[WindowSize];
    private int _idx;
    private int _totalSlots;
    private readonly object _lock = new();

    public void Record()
    {
        lock (_lock) { _buckets[_idx]++; }
    }

    public void Advance()
    {
        lock (_lock)
        {
            _idx = (_idx + 1) % WindowSize;
            _buckets[_idx] = 0;
            if (_totalSlots < WindowSize) _totalSlots++;
        }
    }

    public double Rate
    {
        get
        {
            lock (_lock)
            {
                if (_totalSlots == 0) return 0;
                int filled = Math.Min(_totalSlots, WindowSize);
                long sum = 0;
                for (int i = 0; i < WindowSize; i++) sum += _buckets[i];
                return (double)sum / filled;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_buckets);
            _totalSlots = 0;
        }
    }
}

/// <summary>
/// 10-bucket sliding window peak rate tracker.
/// Each bucket represents one second. advance() is called once per second.
/// Thread-safe via lock.
/// </summary>
public sealed class PeakRateTracker
{
    private const int WindowSize = 10;

    private readonly long[] _buckets = new long[WindowSize];
    private int _idx;
    private double _peak;
    private readonly object _lock = new();

    /// <summary>
    /// Record one event in the current bucket.
    /// </summary>
    public void Record()
    {
        lock (_lock)
        {
            _buckets[_idx]++;
        }
    }

    /// <summary>
    /// Advance the window by one second. Computes average over the window
    /// and updates peak if the new average exceeds it.
    /// </summary>
    public void Advance()
    {
        lock (_lock)
        {
            long total = 0;
            for (int i = 0; i < WindowSize; i++) total += _buckets[i];
            double avg = (double)total / WindowSize;
            if (avg > _peak) _peak = avg;
            _idx = (_idx + 1) % WindowSize;
            _buckets[_idx] = 0;
        }
    }

    /// <summary>
    /// Get the peak average rate observed.
    /// </summary>
    public double Peak
    {
        get
        {
            lock (_lock) return _peak;
        }
    }

    /// <summary>
    /// Reset all buckets and peak.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_buckets);
            _peak = 0;
        }
    }
}

/// <summary>
/// Latency accumulator using HdrHistogram for accurate percentile computation.
/// Records latencies in microseconds internally; reports percentiles in milliseconds.
/// Thread-safe via lock.
/// </summary>
public sealed class LatencyAccumulator
{
    // Track microseconds: 1 us to 60 seconds (60,000,000 us).
    private LongHistogram _hist = new(1, 60_000_000L, 3);
    private readonly object _lock = new();

    /// <summary>
    /// Record a latency in seconds.
    /// </summary>
    public void Record(double durationSec)
    {
        long micros = Math.Clamp((long)Math.Round(durationSec * 1_000_000.0), 1, 60_000_000);
        lock (_lock)
        {
            _hist.RecordValue(micros);
        }
    }

    /// <summary>
    /// Get the value at the given percentile, in milliseconds.
    /// </summary>
    public double PercentileMs(double percentile)
    {
        lock (_lock)
        {
            if (_hist.TotalCount == 0) return 0;
            return _hist.GetValueAtPercentile(percentile) / 1000.0;
        }
    }

    /// <summary>
    /// Get the total number of recorded values.
    /// </summary>
    public long Count
    {
        get
        {
            lock (_lock) return _hist.TotalCount;
        }
    }

    /// <summary>
    /// Reset the histogram.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _hist.Reset();
        }
    }
}
