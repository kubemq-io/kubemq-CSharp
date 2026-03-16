// Send timestamp store for end-to-end latency measurement.
// Maps (producerId, seq) -> Stopwatch.GetTimestamp() value.
// Thread-safe via ConcurrentDictionary.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace KubeMQ.Burnin;

/// <summary>
/// Thread-safe store mapping (producerId, sequence) to high-resolution timestamps.
/// Uses <see cref="Stopwatch.GetTimestamp()"/> for sub-microsecond precision.
/// </summary>
public sealed class TimestampStore
{
    private readonly ConcurrentDictionary<(string ProducerId, long Seq), long> _store = new();

    /// <summary>
    /// Store the current high-resolution timestamp for the given producer and sequence.
    /// </summary>
    public void Store(string producerId, long seq)
    {
        _store[(producerId, seq)] = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Load and atomically delete the timestamp for the given producer and sequence.
    /// Returns null if not found.
    /// </summary>
    public long? LoadAndDelete(string producerId, long seq)
    {
        var key = (producerId, seq);
        if (_store.TryRemove(key, out long value))
        {
            return value;
        }
        return null;
    }

    /// <summary>
    /// Remove entries older than the specified maximum age.
    /// Returns the number of entries purged.
    /// </summary>
    public int Purge(TimeSpan maxAge)
    {
        long cutoff = Stopwatch.GetTimestamp() - (long)(maxAge.TotalSeconds * Stopwatch.Frequency);
        int removed = 0;

        foreach (var kvp in _store)
        {
            if (kvp.Value < cutoff)
            {
                if (_store.TryRemove(kvp.Key, out _))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    /// <summary>
    /// Get the current number of stored timestamps.
    /// </summary>
    public int Count => _store.Count;

    /// <summary>
    /// Convert a Stopwatch timestamp delta to seconds.
    /// </summary>
    public static double TimestampToSeconds(long startTimestamp, long endTimestamp)
    {
        return (double)(endTimestamp - startTimestamp) / Stopwatch.Frequency;
    }

    /// <summary>
    /// Convert a Stopwatch timestamp delta to seconds using the current time as end.
    /// </summary>
    public static double ElapsedSeconds(long startTimestamp)
    {
        return TimestampToSeconds(startTimestamp, Stopwatch.GetTimestamp());
    }
}
