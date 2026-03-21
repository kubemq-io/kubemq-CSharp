namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown when all retry attempts have been exhausted.
/// Wraps the last exception as InnerException.
/// </summary>
public class KubeMQRetryExhaustedException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQRetryExhaustedException"/> class.</summary>
    public KubeMQRetryExhaustedException()
        : base(
            "All retry attempts exhausted",
            ErrorCode.RetryExhausted,
            ErrorCategory.Transient,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQRetryExhaustedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQRetryExhaustedException(string message)
        : base(
            message,
            ErrorCode.RetryExhausted,
            ErrorCategory.Transient,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQRetryExhaustedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQRetryExhaustedException(string message, Exception innerException)
        : base(
            message,
            ErrorCode.RetryExhausted,
            ErrorCategory.Transient,
            isRetryable: false,
            innerException: innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQRetryExhaustedException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="attemptCount">Total number of attempts made.</param>
    /// <param name="totalDuration">Total wall-clock duration across all attempts.</param>
    /// <param name="lastException">The last exception that caused the final retry to fail.</param>
    public KubeMQRetryExhaustedException(
        string message,
        int attemptCount,
        TimeSpan totalDuration,
        Exception lastException)
        : base(
            message,
            ErrorCode.RetryExhausted,
            ErrorCategory.Transient,
            isRetryable: false,
            innerException: lastException)
    {
        AttemptCount = attemptCount;
        TotalDuration = totalDuration;
    }

    /// <summary>Gets the total number of attempts made (including the initial call).</summary>
    public int AttemptCount { get; }

    /// <summary>Gets the total wall-clock duration across all attempts.</summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>Gets the last exception that caused the final retry to fail.</summary>
    public Exception? LastException => InnerException;
}
