namespace KubeMQ.Sdk.Common;

/// <summary>
/// Traffic statistics for a channel direction (incoming or outgoing).
/// </summary>
public record ChannelStats
{
    /// <summary>Gets the total number of messages.</summary>
    public long Messages { get; init; }

    /// <summary>Gets the total data volume in bytes.</summary>
    public long Volume { get; init; }

    /// <summary>Gets the number of messages waiting for delivery.</summary>
    public long Waiting { get; init; }

    /// <summary>Gets the number of expired messages.</summary>
    public long Expired { get; init; }

    /// <summary>Gets the number of delayed messages.</summary>
    public long Delayed { get; init; }
}
