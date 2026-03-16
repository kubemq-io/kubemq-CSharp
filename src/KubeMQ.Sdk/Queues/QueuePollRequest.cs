using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// Configuration for polling messages from a queue channel.
/// </summary>
public class QueuePollRequest
{
    /// <summary>Gets or sets the queue channel to poll from.</summary>
    public required string Channel { get; set; }

    /// <summary>Gets or sets the maximum number of messages to return. Default: 1.</summary>
    public int MaxMessages { get; set; } = 1;

    /// <summary>Gets or sets how long to wait for messages in seconds. Default: 10.</summary>
    public int WaitTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the optional visibility timeout in seconds. Messages not ack'd within this
    /// window are automatically re-queued.
    /// </summary>
    public int? VisibilitySeconds { get; set; }

    /// <summary>Gets or sets a value indicating whether messages are automatically acknowledged upon receipt.</summary>
    public bool AutoAck { get; set; }

    /// <summary>Gets or sets optional metadata to attach to the downstream request.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Validates that all required fields are set and values are within valid ranges.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            throw new KubeMQConfigurationException("QueuePollRequest: Channel is required.");
        }

        if (MaxMessages <= 0)
        {
            throw new KubeMQConfigurationException("QueuePollRequest: MaxMessages must be positive.");
        }

        if (MaxMessages > 1024)
        {
            throw new KubeMQConfigurationException("MaxNumberOfMessages cannot exceed 1024.");
        }

        if (WaitTimeoutSeconds <= 0)
        {
            throw new KubeMQConfigurationException("QueuePollRequest: WaitTimeoutSeconds must be positive.");
        }

        if (WaitTimeoutSeconds > 3600)
        {
            throw new KubeMQConfigurationException("WaitTimeSeconds cannot exceed 3600.");
        }

        if (VisibilitySeconds is <= 0)
        {
            throw new KubeMQConfigurationException("QueuePollRequest: VisibilitySeconds must be positive when set.");
        }
    }
}
