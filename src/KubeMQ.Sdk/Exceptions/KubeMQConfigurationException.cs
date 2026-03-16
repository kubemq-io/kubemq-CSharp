namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Thrown during client construction when configuration is invalid.
/// Never retryable — fix the configuration.
/// </summary>
public class KubeMQConfigurationException : KubeMQException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQConfigurationException"/> class.</summary>
    public KubeMQConfigurationException()
        : base(
            "Configuration invalid",
            KubeMQErrorCode.ConfigurationInvalid,
            KubeMQErrorCategory.Validation,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQConfigurationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQConfigurationException(string message)
        : base(
            message,
            KubeMQErrorCode.ConfigurationInvalid,
            KubeMQErrorCategory.Validation,
            isRetryable: false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQConfigurationException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQConfigurationException(string message, Exception innerException)
        : base(
            message,
            KubeMQErrorCode.ConfigurationInvalid,
            KubeMQErrorCategory.Validation,
            isRetryable: false,
            innerException: innerException)
    {
    }
}
