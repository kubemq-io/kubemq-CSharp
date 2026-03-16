namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown when authentication fails (invalid or expired credentials).
/// Not retryable unless credentials are refreshed.
/// </summary>
public class KubeMQAuthenticationException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQAuthenticationException"/> class.</summary>
    public KubeMQAuthenticationException()
        : base(
            "Authentication failed",
            KubeMQErrorCode.AuthenticationFailed,
            KubeMQErrorCategory.Authentication,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQAuthenticationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQAuthenticationException(string message)
        : base(
            message,
            KubeMQErrorCode.AuthenticationFailed,
            KubeMQErrorCategory.Authentication,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQAuthenticationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQAuthenticationException(string message, Exception innerException)
        : base(
            message,
            KubeMQErrorCode.AuthenticationFailed,
            KubeMQErrorCategory.Authentication,
            isRetryable: false,
            innerException: innerException)
    {
    }
}
