namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown when the reconnect message buffer is full. Classified as Backpressure.
/// Not retryable — wait for reconnection or increase buffer size.
/// </summary>
public class KubeMQBufferFullException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQBufferFullException"/> class.</summary>
    public KubeMQBufferFullException()
        : base(
            "Reconnect buffer full",
            ErrorCode.BufferFull,
            ErrorCategory.Backpressure,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQBufferFullException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQBufferFullException(string message)
        : base(
            message,
            ErrorCode.BufferFull,
            ErrorCategory.Backpressure,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQBufferFullException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQBufferFullException(string message, Exception innerException)
        : base(
            message,
            ErrorCode.BufferFull,
            ErrorCategory.Backpressure,
            isRetryable: false,
            innerException: innerException)
    {
    }

    /// <summary>Gets the current buffer usage in bytes.</summary>
    public long BufferSizeBytes { get; init; }

    /// <summary>Gets the maximum buffer capacity in bytes.</summary>
    public long BufferCapacityBytes { get; init; }
}
