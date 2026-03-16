using System.Collections.Concurrent;
using KubeMQ.Sdk.Internal.Logging;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Tracks active subscriptions and their parameters for transparent re-subscription
/// after reconnection.
/// </summary>
internal sealed class StreamManager
{
    private readonly ConcurrentDictionary<string, SubscriptionRecord> _subscriptions = new();
    private readonly ConcurrentDictionary<string, long> _lastSequences = new();
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamManager"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    internal StreamManager(ILogger logger)
    {
        _logger = logger;
    }

    internal void TrackSubscription(string id, SubscriptionRecord record)
    {
        _subscriptions[id] = record;
    }

    internal void UntrackSubscription(string id)
    {
        _subscriptions.TryRemove(id, out _);
    }

    internal void UpdateLastSequence(string subscriptionId, long sequence)
    {
        _lastSequences[subscriptionId] = sequence;
    }

    internal async Task ResubscribeAllAsync(CancellationToken ct)
    {
        foreach (KeyValuePair<string, SubscriptionRecord> kvp in _subscriptions)
        {
            string id = kvp.Key;
            SubscriptionRecord record = kvp.Value;

            try
            {
                await ResubscribeSingleAsync(id, record, ct).ConfigureAwait(false);
                Log.SubscriptionRestored(_logger, record.Channel, record.Pattern.ToString());
            }
            catch (Exception ex)
            {
                Log.SubscriptionRestoreFailed(_logger, record.Channel, ex);
            }
        }
    }

    private async Task ResubscribeSingleAsync(
        string id, SubscriptionRecord record, CancellationToken ct)
    {
        switch (record.Pattern)
        {
            case SubscriptionPattern.EventsStore:
                long lastSeq = _lastSequences.GetValueOrDefault(id, 0);
                object adjustedParams = record.AdjustForReconnect(lastSeq);
                await record.ResubscribeFunc(adjustedParams, ct)
                    .ConfigureAwait(false);
                break;

            case SubscriptionPattern.Events:
            case SubscriptionPattern.Queue:
            case SubscriptionPattern.Commands:
            case SubscriptionPattern.Queries:
                await record.ResubscribeFunc(record.OriginalParams, ct)
                    .ConfigureAwait(false);
                break;
        }
    }
}
