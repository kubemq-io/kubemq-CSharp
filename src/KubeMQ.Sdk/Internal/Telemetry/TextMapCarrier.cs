using System.Diagnostics;

namespace KubeMQ.Sdk.Internal.Telemetry;

/// <summary>
/// Adapter over KubeMQ message tags (Dictionary&lt;string, string&gt;) for
/// W3C Trace Context injection and extraction.
/// </summary>
internal static class TextMapCarrier
{
    private const string _traceparentKey = "traceparent";
    private const string _tracestateKey = "tracestate";

    internal static Dictionary<string, string> InjectContext(
        IReadOnlyDictionary<string, string>? existingTags,
        Activity? activity = null)
    {
        var tags = existingTags is not null
            ? new Dictionary<string, string>(existingTags)
            : new Dictionary<string, string>();

        var current = activity ?? Activity.Current;
        if (current is null)
        {
            return tags;
        }

        tags[_traceparentKey] = FormatTraceparent(current.TraceId, current.SpanId, current.ActivityTraceFlags);
        if (!string.IsNullOrEmpty(current.TraceStateString))
        {
            tags[_tracestateKey] = current.TraceStateString;
        }

        return tags;
    }

    internal static ActivityContext ExtractContext(
        IReadOnlyDictionary<string, string>? tags)
    {
        if (tags is null)
        {
            return default;
        }

        if (!tags.TryGetValue(_traceparentKey, out string? traceparent) ||
            string.IsNullOrEmpty(traceparent))
        {
            return default;
        }

        if (!TryParseTraceparent(traceparent, out ActivityTraceId traceId, out ActivitySpanId spanId, out ActivityTraceFlags flags))
        {
            return default;
        }

        tags.TryGetValue(_tracestateKey, out string? tracestate);

        return new ActivityContext(traceId, spanId, flags, tracestate, isRemote: true);
    }

    private static string FormatTraceparent(
        ActivityTraceId traceId,
        ActivitySpanId spanId,
        ActivityTraceFlags flags)
    {
        return $"00-{traceId}-{spanId}-{(flags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")}";
    }

    private static bool TryParseTraceparent(
        string traceparent,
        out ActivityTraceId traceId,
        out ActivitySpanId spanId,
        out ActivityTraceFlags flags)
    {
        traceId = default;
        spanId = default;
        flags = ActivityTraceFlags.None;

        string[] parts = traceparent.Split('-');
        if (parts.Length < 4)
        {
            return false;
        }

        if (parts[1].Length != 32 || parts[2].Length != 16)
        {
            return false;
        }

        traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
        spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
        flags = parts[3] == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;
        return true;
    }
}
