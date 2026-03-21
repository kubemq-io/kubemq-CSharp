namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Classification of errors into semantic categories.
/// Determines retryability and recommended caller action.
/// </summary>
public enum ErrorCategory
{
    /// <summary>Server temporarily unavailable or conflict. Auto-retry with backoff.</summary>
    Transient,

    /// <summary>Deadline exceeded. Auto-retry with caution (see operation safety).</summary>
    Timeout,

    /// <summary>Rate limited / resource exhausted. Auto-retry with extended backoff.</summary>
    Throttling,

    /// <summary>Invalid credentials. Do not retry; refresh credentials.</summary>
    Authentication,

    /// <summary>Insufficient permissions. Do not retry; fix permissions.</summary>
    Authorization,

    /// <summary>Bad request / invalid argument / failed precondition. Do not retry; fix input.</summary>
    Validation,

    /// <summary>Resource not found. Do not retry.</summary>
    NotFound,

    /// <summary>Unrecoverable server error. Do not retry.</summary>
    Fatal,

    /// <summary>Operation cancelled by caller. Do not retry.</summary>
    Cancellation,

    /// <summary>Reconnect buffer overflow. Do not retry; wait or increase buffer.</summary>
    Backpressure,
}
