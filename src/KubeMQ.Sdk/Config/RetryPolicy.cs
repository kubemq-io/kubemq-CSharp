using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Config;

/// <summary>
/// Configures automatic retry behavior for transient operation failures.
/// Immutable after client construction — to change retry behavior, create a new client.
/// </summary>
/// <threadsafety static="true" instance="false"/>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is NOT thread-safe. Configure before passing to
/// the client constructor. Do not modify after the client has been created.
/// </para>
/// <para>
/// <b>Worst-case latency calculation (defaults):</b>
/// With 3 retries, 5s timeout, 500ms initial backoff, 2.0 multiplier, no jitter:
/// 5s + 0.5s + 5s + 1s + 5s + 2s + 5s = 23.5s worst case.
/// </para>
/// <para>
/// Operation retry backoff (this class) and connection reconnection backoff
/// (ReconnectOptions, spec 02) are independent policies with independent configuration.
/// </para>
/// <para>
/// Properties use <c>{ get; set; }</c> (not <c>{ get; init; }</c>) per the
/// .NET Options pattern convention. Immutability is enforced at the client constructor
/// level — <c>options.Validate()</c> is called once and the options object is not re-read
/// after construction. Using <c>{ get; init; }</c> would prevent the standard
/// <c>Configure&lt;T&gt;</c> delegate pattern from working in DI scenarios.
/// </para>
/// </remarks>
public sealed class RetryPolicy
{
    /// <summary>Gets or sets a value indicating whether retry is enabled. Default: true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the maximum number of retry attempts. 0 disables retry. Range: 0–10. Default: 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Gets or sets the delay before the first retry. Range: 50ms–5s. Default: 500ms.</summary>
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets or sets the maximum delay between retries. Range: 1s–120s. Default: 30s.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the multiplier applied to backoff after each attempt. Range: 1.5–3.0. Default: 2.0.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>Gets or sets the jitter strategy. Default: Full.</summary>
    public JitterMode JitterMode { get; set; } = JitterMode.Full;

    /// <summary>
    /// Gets or sets the maximum concurrent retry attempts across the client to prevent retry storms.
    /// 0 = unlimited (not recommended). Range: 0–100. Default: 10.
    /// </summary>
    public int MaxConcurrentRetries { get; set; } = 10;

    /// <summary>
    /// Validates all property values are within acceptable ranges.
    /// Throws <see cref="KubeMQConfigurationException"/> on invalid values.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">One or more property values are out of range.</exception>
    public void Validate()
    {
        if (MaxRetries < 0 || MaxRetries > 10)
        {
            throw new KubeMQConfigurationException($"RetryPolicy.MaxRetries must be 0\u201310, got {MaxRetries}.");
        }

        if (InitialBackoff < TimeSpan.FromMilliseconds(50) || InitialBackoff > TimeSpan.FromSeconds(5))
        {
            throw new KubeMQConfigurationException($"RetryPolicy.InitialBackoff must be 50ms\u20135s, got {InitialBackoff}.");
        }

        if (MaxBackoff < TimeSpan.FromSeconds(1) || MaxBackoff > TimeSpan.FromSeconds(120))
        {
            throw new KubeMQConfigurationException($"RetryPolicy.MaxBackoff must be 1s\u2013120s, got {MaxBackoff}.");
        }

        if (MaxBackoff < InitialBackoff)
        {
            throw new KubeMQConfigurationException("RetryPolicy.MaxBackoff must be >= InitialBackoff.");
        }

        if (BackoffMultiplier < 1.5 || BackoffMultiplier > 3.0)
        {
            throw new KubeMQConfigurationException($"RetryPolicy.BackoffMultiplier must be 1.5\u20133.0, got {BackoffMultiplier}.");
        }

        if (MaxConcurrentRetries < 0 || MaxConcurrentRetries > 100)
        {
            throw new KubeMQConfigurationException($"RetryPolicy.MaxConcurrentRetries must be 0\u2013100, got {MaxConcurrentRetries}.");
        }
    }
}
