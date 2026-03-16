namespace KubeMQ.Sdk.Exceptions;

/// <summary>
/// Base exception for all KubeMQ SDK errors. All SDK methods throw exceptions
/// derived from this type. Raw gRPC errors are never exposed to callers.
/// </summary>
public class KubeMQException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="KubeMQException"/> class.</summary>
    public KubeMQException()
        : base()
    {
        ErrorCode = KubeMQErrorCode.Unknown;
        Category = KubeMQErrorCategory.Fatal;
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQException"/> class.</summary>
    /// <param name="message">The error message.</param>
    public KubeMQException(string message)
        : base(message)
    {
        ErrorCode = KubeMQErrorCode.Unknown;
        Category = KubeMQErrorCategory.Fatal;
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public KubeMQException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = KubeMQErrorCode.Unknown;
        Category = KubeMQErrorCategory.Fatal;
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <param name="category">Semantic error category.</param>
    /// <param name="isRetryable">Whether the error is transient and retryable.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public KubeMQException(
        string message,
        KubeMQErrorCode errorCode,
        KubeMQErrorCategory category,
        bool isRetryable,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Category = category;
        IsRetryable = isRetryable;
    }

    /// <summary>Initializes a new instance of the <see cref="KubeMQException"/> class.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <param name="category">Semantic error category.</param>
    /// <param name="isRetryable">Whether the error is transient and retryable.</param>
    /// <param name="requestId">Request ID for server log correlation.</param>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="channel">The target channel/queue.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public KubeMQException(
        string message,
        KubeMQErrorCode errorCode,
        KubeMQErrorCategory category,
        bool isRetryable,
        string? requestId,
        string? operation,
        string? channel,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Category = category;
        IsRetryable = isRetryable;
        RequestId = requestId;
        Operation = operation;
        Channel = channel;
    }

    /// <summary>Gets the machine-readable error code for programmatic handling.</summary>
    public KubeMQErrorCode ErrorCode { get; }

    /// <summary>Gets the semantic category determining retryability and recommended action.</summary>
    public KubeMQErrorCategory Category { get; }

    /// <summary>
    /// Gets the client-generated unique ID for correlating with server logs.
    /// Reserved for future use — always null in v3.0.0 because the KubeMQ server
    /// does not currently return request IDs in error responses. Will be populated
    /// when server-side request ID propagation is implemented.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>Gets which operation failed (e.g., "PublishEvent", "SendQueueMessage").</summary>
    public string? Operation { get; internal set; }

    /// <summary>Gets which channel/queue was targeted.</summary>
    public string? Channel { get; internal set; }

    /// <summary>Gets a value indicating whether this error is transient and may succeed on retry.</summary>
    public bool IsRetryable { get; internal set; }

    /// <summary>Gets which server endpoint returned the error.</summary>
    public string? ServerAddress { get; internal set; }

    /// <summary>Gets the gRPC status code, if this error originated from a gRPC call.</summary>
    public int? GrpcStatusCode { get; internal set; }

    /// <summary>Gets the timestamp when the error occurred.</summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
