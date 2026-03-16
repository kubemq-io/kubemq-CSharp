namespace KubeMQ.Sdk.Queues;

/// <summary>Result of receiving queue messages via the simple pull API.</summary>
public sealed record QueueReceiveResult
{
    /// <summary>Gets the request ID.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Gets the received messages.</summary>
    public IReadOnlyList<QueueMessageReceived> Messages { get; init; } = Array.Empty<QueueMessageReceived>();

    /// <summary>Gets the count of messages returned.</summary>
    public int MessagesReceived { get; init; }

    /// <summary>Gets the count of messages that expired during receive.</summary>
    public int MessagesExpired { get; init; }

    /// <summary>Gets a value indicating whether this was a peek response.</summary>
    public bool IsPeak { get; init; }

    /// <summary>Gets a value indicating whether an error occurred.</summary>
    public bool IsError { get; init; }

    /// <summary>Gets the error message.</summary>
    public string Error { get; init; } = string.Empty;
}
