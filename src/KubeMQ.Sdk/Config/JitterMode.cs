namespace KubeMQ.Sdk.Config;

/// <summary>
/// Jitter strategy applied to retry backoff delays.
/// </summary>
public enum JitterMode
{
    /// <summary>No jitter. Deterministic delays.</summary>
    None,

    /// <summary>Full jitter: sleep = random(0, calculated_delay). Recommended default.</summary>
    Full,

    /// <summary>Equal jitter: sleep = delay/2 + random(0, delay/2).</summary>
    Equal,
}
