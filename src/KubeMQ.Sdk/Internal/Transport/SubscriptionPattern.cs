namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Pattern used for subscription recovery semantics.
/// </summary>
internal enum SubscriptionPattern
{
    Events,
    EventsStore,
    Queue,
    Commands,
    Queries,
}
