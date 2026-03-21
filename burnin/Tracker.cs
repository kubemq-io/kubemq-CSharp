// Bitset-based sequence tracker with sliding window anchored at highContiguous.
// Detects loss, duplication, and out-of-order delivery. Thread-safe with lock.

namespace KubeMQ.Burnin;

/// <summary>
/// Result of recording a sequence number.
/// </summary>
public readonly record struct RecordResult(bool IsDuplicate, bool IsOutOfOrder);

/// <summary>
/// Per-producer tracking state.
/// </summary>
internal sealed class ProducerState
{
    public long HighContiguous;
    public uint[] Window;
    public int WindowBits;
    public long Received;
    public long Duplicates;
    public long OutOfOrder;
    public long ConfirmedLost;
    public long LastReportedLost;
    public long LastSeen;
    public bool Initialized;

    public ProducerState(int windowBits)
    {
        WindowBits = windowBits;
        Window = new uint[(windowBits + 31) / 32];
    }
}

/// <summary>
/// Bitset sliding window tracker. Same algorithm as the JS implementation:
/// highContiguous pointer, uint[] bit array, slideTo counting gaps, detectGaps returning delta.
/// Thread-safe: all public methods are synchronized with a lock.
/// </summary>
public sealed class Tracker
{
    private readonly int _reorderWindow;
    private readonly Dictionary<string, ProducerState> _producers = new();
    private readonly object _lock = new();

    public Tracker(int reorderWindow = 10_000)
    {
        _reorderWindow = reorderWindow;
    }

    /// <summary>
    /// Record a received sequence number for the given producer.
    /// </summary>
    public RecordResult Record(string producerId, long seq)
    {
        lock (_lock)
        {
            if (!_producers.TryGetValue(producerId, out var state))
            {
                state = new ProducerState(_reorderWindow);
                _producers[producerId] = state;
            }

            if (!state.Initialized)
            {
                state.HighContiguous = seq;
                state.LastSeen = seq;
                state.Initialized = true;
                state.Received++;
                return new RecordResult(false, false);
            }

            state.Received++;

            // Sequence at or below highContiguous is a duplicate.
            if (seq <= state.HighContiguous)
            {
                state.Duplicates++;
                return new RecordResult(true, false);
            }

            long offset = seq - state.HighContiguous - 1;

            // If offset exceeds window, slide forward.
            if (offset >= state.WindowBits)
            {
                SlideTo(state, seq);
            }

            long off2 = seq - state.HighContiguous - 1;
            if (GetBit(state.Window, off2))
            {
                state.Duplicates++;
                return new RecordResult(true, false);
            }

            SetBit(state.Window, off2);

            bool isOOO = seq < state.LastSeen;
            if (isOOO) state.OutOfOrder++;
            state.LastSeen = Math.Max(state.LastSeen, seq);

            // Advance highContiguous through contiguous set bits.
            while (GetBit(state.Window, 0))
            {
                state.HighContiguous++;
                ShiftRight1(state.Window);
            }

            return new RecordResult(false, isOOO);
        }
    }

    /// <summary>
    /// Detect gaps since last call. Returns a dictionary of producerId to new lost count delta.
    /// </summary>
    public Dictionary<string, long> DetectGaps()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, long>();
            foreach (var (pid, state) in _producers)
            {
                if (!state.Initialized) continue;
                long delta = state.ConfirmedLost - state.LastReportedLost;
                if (delta > 0)
                {
                    result[pid] = delta;
                    state.LastReportedLost = state.ConfirmedLost;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Check if a sequence number would be flagged as duplicate without recording it.
    /// </summary>
    public bool Peek(string producerId, long seq)
    {
        lock (_lock)
        {
            if (!_producers.TryGetValue(producerId, out var state))
                return false;
            if (!state.Initialized)
                return false;
            if (seq <= state.HighContiguous)
                return true;
            long offset = seq - state.HighContiguous - 1;
            if (offset >= state.WindowBits)
                return false;
            return GetBit(state.Window, offset);
        }
    }

    public long TotalReceived()
    {
        lock (_lock)
        {
            long total = 0;
            foreach (var s in _producers.Values) total += s.Received;
            return total;
        }
    }

    public long TotalDuplicates()
    {
        lock (_lock)
        {
            long total = 0;
            foreach (var s in _producers.Values) total += s.Duplicates;
            return total;
        }
    }

    public long TotalOutOfOrder()
    {
        lock (_lock)
        {
            long total = 0;
            foreach (var s in _producers.Values) total += s.OutOfOrder;
            return total;
        }
    }

    public long TotalLost()
    {
        lock (_lock)
        {
            long total = 0;
            foreach (var s in _producers.Values) total += s.ConfirmedLost;
            return total;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _producers.Clear();
        }
    }

    /// <summary>
    /// Slide the window forward so that newSeq fits. Counts gaps as confirmed lost.
    /// </summary>
    private static void SlideTo(ProducerState state, long newSeq)
    {
        long targetHC = newSeq - state.WindowBits;
        if (targetHC <= state.HighContiguous) return;
        long advance = targetHC - state.HighContiguous;
        for (long i = 0; i < advance; i++)
        {
            if (!GetBit(state.Window, 0))
            {
                state.ConfirmedLost++;
            }
            state.HighContiguous++;
            ShiftRight1(state.Window);
        }
    }

    private static void SetBit(uint[] arr, long offset)
    {
        int w = (int)(offset >> 5);
        int b = (int)(offset & 31);
        if (w < arr.Length)
        {
            arr[w] |= 1u << b;
        }
    }

    private static bool GetBit(uint[] arr, long offset)
    {
        int w = (int)(offset >> 5);
        int b = (int)(offset & 31);
        return w < arr.Length && (arr[w] & (1u << b)) != 0;
    }

    private static void ShiftRight1(uint[] arr)
    {
        for (int i = 0; i < arr.Length - 1; i++)
        {
            arr[i] = (arr[i] >> 1) | ((arr[i + 1] & 1u) << 31);
        }
        arr[^1] >>= 1;
    }
}
