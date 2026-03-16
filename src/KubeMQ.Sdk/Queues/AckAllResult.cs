namespace KubeMQ.Sdk.Queues;

/// <summary>Result of acknowledging all messages in a queue.</summary>
public sealed record AckAllResult
{
    /// <summary>Gets the number of messages acknowledged.</summary>
    public long AffectedMessages { get; init; }

    /// <summary>Gets a value indicating whether an error occurred.</summary>
    public bool IsError { get; init; }

    /// <summary>Gets the error message, empty on success.</summary>
    public string Error { get; init; } = string.Empty;
}
