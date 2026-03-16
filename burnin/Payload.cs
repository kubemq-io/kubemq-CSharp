// Payload encoding/decoding with CRC32 integrity verification.

using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KubeMQ.Burnin;

/// <summary>
/// Deserialized message payload with all required fields.
/// </summary>
public sealed class MessagePayload
{
    [JsonPropertyName("sdk")]
    public string Sdk { get; set; } = "";

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("producer_id")]
    public string ProducerId { get; set; } = "";

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    [JsonPropertyName("timestamp_ns")]
    public long TimestampNs { get; set; }

    [JsonPropertyName("payload_padding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PayloadPadding { get; set; }
}

/// <summary>
/// Result of encoding a message payload.
/// </summary>
public readonly record struct EncodedPayload(ReadOnlyMemory<byte> Body, string CrcHex);

/// <summary>
/// Payload encoder/decoder with CRC32 IEEE integrity verification and random padding.
/// </summary>
public static class Payload
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Compute CRC32 IEEE hash and return as 8-char lowercase hex string.
    /// </summary>
    public static string Crc32Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[4];
        Crc32.Hash(data, hash);
        // Crc32.Hash returns big-endian bytes; convert to uint for hex formatting.
        uint value = ((uint)hash[0] << 24) | ((uint)hash[1] << 16) | ((uint)hash[2] << 8) | hash[3];
        return value.ToString("x8");
    }

    /// <summary>
    /// Verify that the CRC32 of the body matches the expected hex string.
    /// </summary>
    public static bool VerifyCrc(ReadOnlySpan<byte> body, string expected)
    {
        return string.Equals(Crc32Hex(body), expected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Encode a message payload with optional padding to reach the target size.
    /// </summary>
    public static EncodedPayload Encode(string sdk, string pattern, string producerId, long seq, int targetSize)
    {
        var msg = new MessagePayload
        {
            Sdk = sdk,
            Pattern = pattern,
            ProducerId = producerId,
            Sequence = seq,
            TimestampNs = StopwatchTimestamp.GetNanoseconds(),
        };

        // Serialize without padding to measure base size.
        byte[] baseBytes = JsonSerializer.SerializeToUtf8Bytes(msg, s_jsonOptions);

        if (targetSize > baseBytes.Length + 20)
        {
            int padLen = targetSize - baseBytes.Length - 20;
            if (padLen > 0)
            {
                msg.PayloadPadding = RandomPadding(padLen);
            }
        }

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(msg, s_jsonOptions);
        string crcHex = Crc32Hex(body);
        return new EncodedPayload(body, crcHex);
    }

    /// <summary>
    /// Decode a message payload from raw bytes.
    /// </summary>
    public static MessagePayload Decode(ReadOnlyMemory<byte> body)
    {
        return JsonSerializer.Deserialize<MessagePayload>(body.Span, s_jsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize payload");
    }

    /// <summary>
    /// Generate random printable ASCII padding of the specified length.
    /// Characters range from '!' (33) to '~' (126).
    /// </summary>
    private static string RandomPadding(int length)
    {
        Span<byte> buf = length <= 1024 ? stackalloc byte[length] : new byte[length];
        RandomNumberGenerator.Fill(buf);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = (char)(33 + (buf[i] % 94));
        }
        return new string(chars);
    }
}

/// <summary>
/// Weighted size distribution for message payload sizes.
/// Parses a spec like "256:80,4096:15,65536:5" into weighted random selection.
/// </summary>
public sealed class SizeDistribution
{
    private readonly int[] _sizes;
    private readonly int[] _weights;
    private readonly int _totalWeight;

    public SizeDistribution(string spec)
    {
        var pairs = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _sizes = new int[pairs.Length];
        _weights = new int[pairs.Length];
        _totalWeight = 0;

        for (int i = 0; i < pairs.Length; i++)
        {
            var parts = pairs[i].Split(':');
            _sizes[i] = int.Parse(parts[0]);
            _weights[i] = int.Parse(parts[1]);
            _totalWeight += _weights[i];
        }
    }

    /// <summary>
    /// Select a random size based on the weight distribution.
    /// Thread-safe: uses Random.Shared.
    /// </summary>
    public int SelectSize()
    {
        int r = Random.Shared.Next(1, _totalWeight + 1);
        for (int i = 0; i < _sizes.Length; i++)
        {
            r -= _weights[i];
            if (r <= 0) return _sizes[i];
        }
        return _sizes[^1];
    }
}

/// <summary>
/// High-resolution timestamp utilities using Stopwatch.
/// </summary>
public static class StopwatchTimestamp
{
    private static readonly double s_ticksToNanos = 1_000_000_000.0 / System.Diagnostics.Stopwatch.Frequency;

    /// <summary>
    /// Get current timestamp in nanoseconds (monotonic, high-resolution).
    /// </summary>
    public static long GetNanoseconds()
    {
        return (long)(System.Diagnostics.Stopwatch.GetTimestamp() * s_ticksToNanos);
    }
}
