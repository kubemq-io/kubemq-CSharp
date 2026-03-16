namespace KubeMQ.Sdk.Common;

/// <summary>
/// Event arguments for connection state changes.
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>Gets the previous connection state.</summary>
    public required ConnectionState PreviousState { get; init; }

    /// <summary>Gets the new connection state.</summary>
    public required ConnectionState CurrentState { get; init; }

    /// <summary>Gets the UTC timestamp when the transition occurred.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets the exception that triggered the transition, if any.</summary>
    public Exception? Error { get; init; }
}
