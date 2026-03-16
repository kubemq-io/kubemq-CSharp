using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Logging;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Telemetry;

/// <summary>
/// Centralized metrics instrumentation using System.Diagnostics.Metrics.Meter.
/// No OTel NuGet dependency — Meter is built into .NET since 6.0.
/// </summary>
internal static class KubeMQMetrics
{
    internal static readonly Meter InternalMeter = new("KubeMQ.Sdk", KubeMQSdkInfo.Version);

    internal static readonly Histogram<double> OperationDuration =
        InternalMeter.CreateHistogram<double>(
            SemanticConventions.MetricOperationDuration,
            unit: "s",
            description: "Duration of each messaging operation");

    internal static readonly Counter<long> MessagesSent =
        InternalMeter.CreateCounter<long>(
            SemanticConventions.MetricSentMessages,
            unit: "{message}",
            description: "Total messages sent");

    internal static readonly Counter<long> MessagesConsumed =
        InternalMeter.CreateCounter<long>(
            SemanticConventions.MetricConsumedMessages,
            unit: "{message}",
            description: "Total messages consumed");

    internal static readonly UpDownCounter<long> ConnectionCount =
        InternalMeter.CreateUpDownCounter<long>(
            SemanticConventions.MetricConnectionCount,
            unit: "{connection}",
            description: "Active connections");

    internal static readonly Counter<long> Reconnections =
        InternalMeter.CreateCounter<long>(
            SemanticConventions.MetricReconnections,
            unit: "{attempt}",
            description: "Reconnection attempts");

    internal static readonly Counter<long> RetryAttempts =
        InternalMeter.CreateCounter<long>(
            SemanticConventions.MetricRetryAttempts,
            unit: "{attempt}",
            description: "Retry attempts");

    internal static readonly Counter<long> RetryExhausted =
        InternalMeter.CreateCounter<long>(
            SemanticConventions.MetricRetryExhausted,
            unit: "{attempt}",
            description: "Retries exhausted");

    private static readonly ConcurrentDictionary<string, byte> _knownChannels = new();
    private static int _cardinalityThreshold = 100;
    private static ImmutableHashSet<string>? _allowlist;
    private static ILogger? _cardinalityLogger;
    private static volatile bool _cardinalityWarningEmitted;

    internal static void ConfigureCardinality(
        int threshold = 100,
        IEnumerable<string>? channelAllowlist = null,
        ILogger? logger = null)
    {
        _cardinalityThreshold = threshold;
        _allowlist = channelAllowlist is not null
            ? ImmutableHashSet.CreateRange(StringComparer.Ordinal, channelAllowlist)
            : null;
        _cardinalityLogger = logger;
        _cardinalityWarningEmitted = false;
    }

    internal static bool ShouldIncludeChannel(string channelName)
    {
        ImmutableHashSet<string>? snapshot = _allowlist;
        if (snapshot is not null && snapshot.Contains(channelName))
        {
            return true;
        }

        if (_knownChannels.ContainsKey(channelName))
        {
            return true;
        }

        if (_knownChannels.Count >= _cardinalityThreshold)
        {
            if (!_cardinalityWarningEmitted)
            {
                _cardinalityWarningEmitted = true;
                if (_cardinalityLogger is not null)
                {
                    Log.CardinalityThresholdExceeded(_cardinalityLogger, _cardinalityThreshold);
                }
            }

            return false;
        }

        _knownChannels.TryAdd(channelName, 0);
        return true;
    }

    internal static void RecordOperationDuration(
        double durationSeconds,
        string operationName,
        string channelName,
        string? errorType = null)
    {
        TagList tags = CreateTags(operationName, channelName, errorType);
        OperationDuration.Record(durationSeconds, tags);
    }

    internal static void RecordMessageSent(
        string operationName,
        string channelName,
        int count = 1)
    {
        TagList tags = CreateTags(operationName, channelName);
        MessagesSent.Add(count, tags);
    }

    internal static void RecordMessageConsumed(
        string operationName,
        string channelName,
        int count = 1)
    {
        TagList tags = CreateTags(operationName, channelName);
        MessagesConsumed.Add(count, tags);
    }

    internal static void RecordRetryAttempt(string operationName, string errorType)
    {
        RetryAttempts.Add(
            1,
            new KeyValuePair<string, object?>(
                SemanticConventions.MessagingSystem,
                SemanticConventions.MessagingSystemValue),
            new KeyValuePair<string, object?>(
                SemanticConventions.MessagingOperationName,
                operationName),
            new KeyValuePair<string, object?>(
                SemanticConventions.ErrorType,
                errorType));
    }

    internal static void RecordRetryExhausted(string operationName, string errorType)
    {
        RetryExhausted.Add(
            1,
            new KeyValuePair<string, object?>(
                SemanticConventions.MessagingSystem,
                SemanticConventions.MessagingSystemValue),
            new KeyValuePair<string, object?>(
                SemanticConventions.MessagingOperationName,
                operationName),
            new KeyValuePair<string, object?>(
                SemanticConventions.ErrorType,
                errorType));
    }

    internal static string MapErrorType(KubeMQErrorCategory category) =>
        category switch
        {
            KubeMQErrorCategory.Transient => "transient",
            KubeMQErrorCategory.Timeout => "timeout",
            KubeMQErrorCategory.Throttling => "throttling",
            KubeMQErrorCategory.Authentication => "authentication",
            KubeMQErrorCategory.Authorization => "authorization",
            KubeMQErrorCategory.Validation => "validation",
            KubeMQErrorCategory.NotFound => "not_found",
            KubeMQErrorCategory.Fatal => "fatal",
            KubeMQErrorCategory.Cancellation => "cancellation",
            KubeMQErrorCategory.Backpressure => "backpressure",
            _ => "unknown",
        };

    private static TagList CreateTags(
        string operationName,
        string channelName,
        string? errorType = null)
    {
        var tags = new TagList
        {
            { SemanticConventions.MessagingSystem, SemanticConventions.MessagingSystemValue },
            { SemanticConventions.MessagingOperationName, operationName },
        };

        if (ShouldIncludeChannel(channelName))
        {
            tags.Add(SemanticConventions.MessagingDestinationName, channelName);
        }

        if (errorType is not null)
        {
            tags.Add(SemanticConventions.ErrorType, errorType);
        }

        return tags;
    }
}
