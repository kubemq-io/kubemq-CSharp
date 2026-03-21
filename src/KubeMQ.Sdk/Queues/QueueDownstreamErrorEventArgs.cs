namespace KubeMQ.Sdk.Queues;

/// <summary>
/// Event arguments for downstream settlement errors reported by the server.
/// </summary>
public sealed class QueueDownstreamErrorEventArgs : EventArgs
{
    /// <summary>Gets the transaction ID associated with the error.</summary>
    public required string TransactionId { get; init; }

    /// <summary>Gets the error message from the server.</summary>
    public required string Error { get; init; }
}
