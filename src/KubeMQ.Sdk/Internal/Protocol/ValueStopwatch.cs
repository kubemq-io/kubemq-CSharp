using System.Diagnostics;

namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// Convenience wrapper over <see cref="Stopwatch.GetTimestamp"/>/<see cref="Stopwatch.GetElapsedTime(long)"/>.
/// </summary>
internal readonly struct ValueStopwatch
{
    private readonly long _startTimestamp;

    private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;

    internal static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

    internal TimeSpan GetElapsedTime() =>
        Stopwatch.GetElapsedTime(_startTimestamp);
}
