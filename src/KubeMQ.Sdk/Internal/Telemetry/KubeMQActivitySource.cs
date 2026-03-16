using System.Diagnostics;

namespace KubeMQ.Sdk.Internal.Telemetry;

/// <summary>
/// Centralized ActivitySource for all KubeMQ SDK trace instrumentation.
/// Uses System.Diagnostics.ActivitySource — no OTel NuGet dependency.
/// Returns null when no listener is attached (near-zero overhead).
/// </summary>
internal static class KubeMQActivitySource
{
    internal static readonly ActivitySource Source = new(
        _instrumentationScopeName,
        KubeMQSdkInfo.Version);

    private const string _instrumentationScopeName = "KubeMQ.Sdk";

    internal static Activity? StartProducerActivity(
        string operationName,
        string channel,
        string? clientId,
        string serverAddress,
        int serverPort)
    {
        var activity = Source.StartActivity(
            $"{operationName} {channel}",
            ActivityKind.Producer);

        if (activity is null)
        {
            return null;
        }

        SetCommonAttributes(
            activity,
            operationName,
            operationName,
            channel,
            clientId,
            serverAddress,
            serverPort);
        return activity;
    }

    internal static Activity? StartConsumerActivity(
        string operationName,
        string channel,
        string? clientId,
        string serverAddress,
        int serverPort,
        ActivityContext? linkedContext = null)
    {
        var links = linkedContext.HasValue
            ? new[] { new ActivityLink(linkedContext.Value) }
            : Array.Empty<ActivityLink>();

        var activity = Source.StartActivity(
            $"{operationName} {channel}",
            ActivityKind.Consumer,
            parentContext: default,
            links: links);

        if (activity is null)
        {
            return null;
        }

        SetCommonAttributes(
            activity,
            operationName,
            MapOperationType(operationName),
            channel,
            clientId,
            serverAddress,
            serverPort);
        return activity;
    }

    internal static Activity? StartClientActivity(
        string channel,
        string? clientId,
        string serverAddress,
        int serverPort)
    {
        var activity = Source.StartActivity(
            $"{SemanticConventions.OperationSend} {channel}",
            ActivityKind.Client);

        if (activity is null)
        {
            return null;
        }

        SetCommonAttributes(
            activity,
            SemanticConventions.OperationSend,
            SemanticConventions.OperationSend,
            channel,
            clientId,
            serverAddress,
            serverPort);
        return activity;
    }

    internal static Activity? StartServerActivity(
        string channel,
        string? clientId,
        string serverAddress,
        int serverPort,
        ActivityContext? linkedContext = null)
    {
        var links = linkedContext.HasValue
            ? new[] { new ActivityLink(linkedContext.Value) }
            : Array.Empty<ActivityLink>();

        var activity = Source.StartActivity(
            $"{SemanticConventions.OperationProcess} {channel}",
            ActivityKind.Server,
            parentContext: default,
            links: links);

        if (activity is null)
        {
            return null;
        }

        SetCommonAttributes(
            activity,
            SemanticConventions.OperationProcess,
            SemanticConventions.OperationProcess,
            channel,
            clientId,
            serverAddress,
            serverPort);
        return activity;
    }

    internal static void RecordRetryEvent(
        Activity? activity,
        int attempt,
        double delaySeconds,
        string errorType)
    {
        activity?.AddEvent(new ActivityEvent(
            SemanticConventions.RetryEventName,
            tags: new ActivityTagsCollection
            {
                { SemanticConventions.RetryAttemptAttribute, attempt },
                { SemanticConventions.RetryDelaySecondsAttribute, delaySeconds },
                { SemanticConventions.ErrorType, errorType },
            }));
    }

    internal static void SetError(Activity? activity, Exception ex, string? errorType = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag(
            SemanticConventions.ErrorType,
            errorType ?? ex.GetType().Name);
    }

    private static void SetCommonAttributes(
        Activity activity,
        string operationName,
        string operationType,
        string channel,
        string? clientId,
        string serverAddress,
        int serverPort)
    {
        activity.SetTag(SemanticConventions.MessagingSystem, SemanticConventions.MessagingSystemValue);
        activity.SetTag(SemanticConventions.MessagingOperationName, operationName);
        activity.SetTag(SemanticConventions.MessagingOperationType, operationType);
        activity.SetTag(SemanticConventions.MessagingDestinationName, channel);
        activity.SetTag(SemanticConventions.ServerAddress, serverAddress);
        activity.SetTag(SemanticConventions.ServerPort, serverPort);

        if (clientId is not null)
        {
            activity.SetTag(SemanticConventions.MessagingClientId, clientId);
        }
    }

    private static string MapOperationType(string operationName) =>
        operationName switch
        {
            SemanticConventions.OperationPublish => SemanticConventions.OperationPublish,
            SemanticConventions.OperationProcess => SemanticConventions.OperationProcess,
            SemanticConventions.OperationReceive => SemanticConventions.OperationReceive,
            SemanticConventions.OperationSettle => SemanticConventions.OperationSettle,
            SemanticConventions.OperationSend => SemanticConventions.OperationSend,
            _ => operationName,
        };
}
