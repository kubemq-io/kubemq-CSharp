namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Reserved for future batch operations that may partially succeed.
/// Not thrown in v3.0.0 — exists so the type is available if the server
/// adds per-message batch status in a future version.
/// </summary>
public class KubeMQPartialFailureException : KubeMQOperationException
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQPartialFailureException"/> class.</summary>
    public KubeMQPartialFailureException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQPartialFailureException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQPartialFailureException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQPartialFailureException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQPartialFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
