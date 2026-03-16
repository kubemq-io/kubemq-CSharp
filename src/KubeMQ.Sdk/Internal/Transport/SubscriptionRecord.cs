namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// A tracked subscription for transparent re-subscription after reconnection.
/// </summary>
internal sealed record SubscriptionRecord(
    string Channel,
    SubscriptionPattern Pattern,
    object OriginalParams,
    Func<object, CancellationToken, Task> ResubscribeFunc,
    Func<long, object>? AdjustFunc = null)
{
    /// <summary>
    /// Adjusts subscription parameters for reconnection. For Events Store, modifies
    /// the start position to resume from <paramref name="lastSequence"/> + 1.
    /// </summary>
    /// <param name="lastSequence">Last received sequence number.</param>
    /// <returns>Adjusted subscription parameters.</returns>
    internal object AdjustForReconnect(long lastSequence)
    {
        return AdjustFunc is not null
            ? AdjustFunc(lastSequence)
            : OriginalParams;
    }
}
