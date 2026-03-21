namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown when a bidirectional stream breaks. Carries the list of
/// message IDs that were sent but not acknowledged before the break.
/// </summary>
public class KubeMQStreamBrokenException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQStreamBrokenException"/> class.</summary>
    public KubeMQStreamBrokenException()
        : base(
            "Stream broken",
            ErrorCode.StreamBroken,
            ErrorCategory.Transient,
            isRetryable: true)
    {
        UnackedMessageIds = Array.Empty<string>();
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQStreamBrokenException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQStreamBrokenException(string message)
        : base(
            message,
            ErrorCode.StreamBroken,
            ErrorCategory.Transient,
            isRetryable: true)
    {
        UnackedMessageIds = Array.Empty<string>();
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQStreamBrokenException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQStreamBrokenException(string message, Exception innerException)
        : base(
            message,
            ErrorCode.StreamBroken,
            ErrorCategory.Transient,
            isRetryable: true,
            innerException: innerException)
    {
        UnackedMessageIds = Array.Empty<string>();
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQStreamBrokenException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="unackedMessageIds">IDs of messages that were in-flight when the stream broke.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public KubeMQStreamBrokenException(
        string message,
        IReadOnlyList<string> unackedMessageIds,
        Exception? innerException = null)
        : base(
            message,
            ErrorCode.StreamBroken,
            ErrorCategory.Transient,
            isRetryable: true,
            innerException: innerException)
    {
        UnackedMessageIds = unackedMessageIds ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the message IDs that were in-flight (sent but not acknowledged) when the stream broke.
    /// </summary>
    public IReadOnlyList<string> UnackedMessageIds { get; }
}
