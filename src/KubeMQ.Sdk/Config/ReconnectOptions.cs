namespace KubeMQ.Sdk.Config;

/// <summary>
/// Configures automatic reconnection behavior with exponential backoff and message buffering.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is NOT thread-safe. Configure before passing to
/// the client constructor. Do not modify after the client has been created.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="false"/>
public sealed class ReconnectOptions
{
    /// <summary>Gets or sets a value indicating whether auto-reconnection is enabled. Default: true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the maximum reconnection attempts. 0 = unlimited. Default: 0.</summary>
    public int MaxAttempts { get; set; }

    /// <summary>Gets or sets the initial delay before the first reconnection attempt. Default: 1 s.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum delay between reconnection attempts. Default: 30 s.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the multiplier applied to the delay after each attempt. Default: 2.0.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>Gets or sets the maximum reconnect buffer size in bytes. Default: 8,388,608 (8 MB).</summary>
    public int BufferSize { get; set; } = 8 * 1024 * 1024;

    /// <summary>Gets or sets the behavior when the reconnect buffer is full. Default: Block.</summary>
    public BufferFullMode BufferFullMode { get; set; } = BufferFullMode.Block;
}
