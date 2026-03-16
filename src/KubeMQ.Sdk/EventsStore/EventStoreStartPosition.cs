namespace KubeMQ.Sdk.EventsStore;

/// <summary>
/// Determines where an EventStore subscription begins reading.
/// </summary>
/// <remarks>
/// These values do NOT map 1:1 to gRPC <c>KubeMQ.Grpc.Subscribe.Types.EventsStoreType</c>.
/// The SDK translates them via an explicit switch in <c>KubeMQClient</c>.
/// gRPC values are: StartNewOnly = 1, StartFromFirst = 2, StartFromLast = 3,
/// StartAtSequence = 4, StartAtTime = 5, StartAtTimeDelta = 6.
/// </remarks>
public enum EventStoreStartPosition
{
    /// <summary>Receive only new messages published after subscription starts.</summary>
    FromNew = 0,

    /// <summary>Replay from the first stored message.</summary>
    FromFirst = 1,

    /// <summary>Start from the most recent stored message.</summary>
    FromLast = 2,

    /// <summary>Start from a specific sequence number (set <see cref="EventStoreSubscription.StartSequence"/>).</summary>
    FromSequence = 3,

    /// <summary>Start from a specific point in time (set <see cref="EventStoreSubscription.StartTime"/>).</summary>
    FromTime = 4,

    /// <summary>Start from a relative time offset in seconds (set <see cref="EventStoreSubscription.StartTimeDeltaSeconds"/>).</summary>
    FromTimeDelta = 5,
}
