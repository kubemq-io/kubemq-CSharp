using System.Diagnostics;
using System.Text;
using Google.Protobuf;

namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// Serializes and deserializes W3C TraceContext into the proto Span (bytes) field
/// for cross-service distributed tracing through KubeMQ.
/// </summary>
internal static class SpanContextSerializer
{
    /// <summary>
    /// Captures the current <see cref="Activity"/> trace context into a byte array
    /// suitable for the proto <c>Span</c> field. Returns empty if no activity is active.
    /// </summary>
    /// <param name="activity">The activity to serialize, or null.</param>
    /// <returns>A <see cref="ByteString"/> containing the serialized trace context.</returns>
    internal static ByteString Serialize(Activity? activity)
    {
        if (activity == null)
        {
            return ByteString.Empty;
        }

        var traceParent = activity.Id;
        if (string.IsNullOrEmpty(traceParent))
        {
            return ByteString.Empty;
        }

        var traceState = activity.TraceStateString ?? string.Empty;
        var payload = $"{traceParent}\n{traceState}";
        return ByteString.CopyFrom(payload, Encoding.UTF8);
    }

    /// <summary>
    /// Extracts W3C traceparent and tracestate from the proto <c>Span</c> bytes.
    /// Returns null values if the span is empty or malformed.
    /// </summary>
    /// <param name="span">The serialized span bytes to deserialize.</param>
    /// <returns>A tuple of traceparent and tracestate strings.</returns>
    internal static (string? TraceParent, string? TraceState) Deserialize(ByteString? span)
    {
        if (span == null || span.IsEmpty)
        {
            return (null, null);
        }

        var payload = span.ToString(Encoding.UTF8);
        var newlineIdx = payload.IndexOf('\n');
        if (newlineIdx < 0)
        {
            return (payload, null);
        }

        var traceParent = payload[..newlineIdx];
        var traceState = payload[(newlineIdx + 1)..];
        return (traceParent, string.IsNullOrEmpty(traceState) ? null : traceState);
    }
}
