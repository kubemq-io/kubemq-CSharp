namespace KubeMQ.Sdk.Common;

/// <summary>
/// Metadata for a KubeMQ channel returned by list operations.
/// </summary>
public record ChannelInfo
{
    /// <summary>Gets the channel name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the channel type (e.g., "events", "events_store", "queues").</summary>
    public required string Type { get; init; }

    /// <summary>Gets the timestamp of the last activity on this channel (Unix epoch ms).</summary>
    public long LastActivity { get; init; }

    /// <summary>Gets a value indicating whether the channel is currently active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gets the incoming traffic statistics.</summary>
    public ChannelStats? Incoming { get; init; }

    /// <summary>Gets the outgoing traffic statistics.</summary>
    public ChannelStats? Outgoing { get; init; }
}
