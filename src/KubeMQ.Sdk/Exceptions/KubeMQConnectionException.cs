namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown when the SDK cannot connect or loses connection to the KubeMQ server.
/// </summary>
public class KubeMQConnectionException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQConnectionException"/> class.</summary>
    public KubeMQConnectionException()
        : base(
            "Connection error",
            ErrorCode.ConnectionRefused,
            ErrorCategory.Transient,
            isRetryable: true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQConnectionException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQConnectionException(string message)
        : base(
            message,
            ErrorCode.ConnectionRefused,
            ErrorCategory.Transient,
            isRetryable: true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQConnectionException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQConnectionException(string message, Exception innerException)
        : base(
            message,
            ErrorCode.ConnectionRefused,
            ErrorCategory.Transient,
            isRetryable: true,
            innerException: innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQConnectionException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The specific connection error code.</param>
    /// <param name="isRetryable">Whether this connection error is retryable.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public KubeMQConnectionException(
        string message,
        ErrorCode errorCode,
        bool isRetryable,
        Exception? innerException = null)
        : base(message, errorCode, ErrorCategory.Transient, isRetryable, innerException)
    {
    }
}
