namespace KubeMQ.Sdk.Events;

/// <summary>
/// Identifies the type of subscription for internal routing.
/// </summary>
/// <remarks>
/// Integer values MUST match <c>KubeMQ.Grpc.Subscribe.Types.SubscribeType</c>:
/// Events = 1, EventsStore = 2, Commands = 3, Queries = 4.
/// </remarks>
public enum SubscribeType
{
    /// <summary>Real-time pub/sub events.</summary>
    Events = 1,

    /// <summary>Persistent event store with replay.</summary>
    EventsStore = 2,

    /// <summary>RPC command requests.</summary>
    Commands = 3,

    /// <summary>RPC query requests.</summary>
    Queries = 4,
}
