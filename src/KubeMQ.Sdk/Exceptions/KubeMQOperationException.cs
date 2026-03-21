namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown when a send, receive, or publish operation fails due to server-side errors.
/// </summary>
public class KubeMQOperationException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQOperationException"/> class.</summary>
    public KubeMQOperationException()
        : base(
            "Operation failed",
            ErrorCode.Internal,
            ErrorCategory.Fatal,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQOperationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQOperationException(string message)
        : base(
            message,
            ErrorCode.Internal,
            ErrorCategory.Fatal,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQOperationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQOperationException(string message, Exception innerException)
        : base(
            message,
            ErrorCode.Internal,
            ErrorCategory.Fatal,
            isRetryable: false,
            innerException: innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQOperationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The specific error code.</param>
    /// <param name="category">The error category.</param>
    /// <param name="isRetryable">Whether the operation error is retryable.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public KubeMQOperationException(
        string message,
        ErrorCode errorCode,
        ErrorCategory category,
        bool isRetryable,
        Exception? innerException = null)
        : base(message, errorCode, category, isRetryable, innerException)
    {
    }
}
