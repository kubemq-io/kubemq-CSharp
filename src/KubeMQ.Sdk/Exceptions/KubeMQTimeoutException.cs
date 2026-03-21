namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown when an operation exceeds its deadline.
/// Retryable with caution — see operation safety rules for non-idempotent operations.
/// </summary>
public class KubeMQTimeoutException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQTimeoutException"/> class.</summary>
    public KubeMQTimeoutException()
        : base(
            "Deadline exceeded",
            ErrorCode.DeadlineExceeded,
            ErrorCategory.Timeout,
            isRetryable: true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQTimeoutException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQTimeoutException(string message)
        : base(
            message,
            ErrorCode.DeadlineExceeded,
            ErrorCategory.Timeout,
            isRetryable: true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQTimeoutException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQTimeoutException(string message, Exception innerException)
        : base(
            message,
            ErrorCode.DeadlineExceeded,
            ErrorCategory.Timeout,
            isRetryable: true,
            innerException: innerException)
    {
    }
}
