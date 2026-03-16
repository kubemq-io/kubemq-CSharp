using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Config;

/// <summary>
/// Controls subscription callback behavior including concurrency limits.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is NOT thread-safe. Configure before passing to
/// the client constructor. Do not modify after the client has been created.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="false"/>
public class SubscriptionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of callbacks processed concurrently per subscription.
    /// Default is 1 (sequential processing). Set higher for parallel message processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to 1, messages are processed sequentially in order. When set higher,
    /// messages may be processed out of order. The concurrency limit applies per
    /// subscription, not globally across all subscriptions.
    /// </para>
    /// <para>
    /// <b>Guidance for long-running callbacks:</b> If your message handler performs
    /// I/O-bound work (database writes, HTTP calls), consider setting this to 4-16
    /// to increase throughput. For CPU-bound work, match the number of available
    /// processor cores. Always ensure your handler is thread-safe when using
    /// concurrency greater than 1.
    /// </para>
    /// </remarks>
    public int MaxConcurrentCallbacks { get; set; } = 1;

    /// <summary>
    /// Gets or sets the size of the internal buffer between the gRPC stream reader and callback dispatch.
    /// Default is 256 messages. Increasing this value can absorb short bursts but uses
    /// more memory.
    /// </summary>
    public int CallbackBufferSize { get; set; } = 256;

    /// <summary>
    /// Validates all property values.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">Thrown when validation fails.</exception>
    internal void Validate()
    {
        if (MaxConcurrentCallbacks < 1)
        {
            throw new KubeMQConfigurationException(
                $"MaxConcurrentCallbacks must be >= 1, got {MaxConcurrentCallbacks}.");
        }

        if (MaxConcurrentCallbacks > 1024)
        {
            throw new KubeMQConfigurationException(
                $"MaxConcurrentCallbacks must be <= 1024, got {MaxConcurrentCallbacks}. " +
                "Values above 1024 risk ThreadPool saturation from Task.Run dispatch.");
        }

        if (CallbackBufferSize < 1)
        {
            throw new KubeMQConfigurationException(
                $"CallbackBufferSize must be >= 1, got {CallbackBufferSize}.");
        }
    }
}
