namespace KubeMQ.Sdk.Internal.Telemetry;

/// <summary>
/// OTel messaging semantic convention attribute name constants (v1.27.0).
/// </summary>
internal static class SemanticConventions
{
    internal const string MessagingSystem = "messaging.system";
    internal const string MessagingSystemValue = "kubemq";

    internal const string MessagingOperationName = "messaging.operation.name";
    internal const string MessagingOperationType = "messaging.operation.type";
    internal const string MessagingDestinationName = "messaging.destination.name";
    internal const string MessagingMessageId = "messaging.message.id";
    internal const string MessagingClientId = "messaging.client.id";
    internal const string MessagingConsumerGroupName = "messaging.consumer.group.name";
    internal const string MessagingMessageBodySize = "messaging.message.body.size";
    internal const string MessagingBatchMessageCount = "messaging.batch.message_count";

    internal const string ServerAddress = "server.address";
    internal const string ServerPort = "server.port";
    internal const string ErrorType = "error.type";

    internal const string OperationPublish = "publish";
    internal const string OperationProcess = "process";
    internal const string OperationReceive = "receive";
    internal const string OperationSettle = "settle";
    internal const string OperationSend = "send";

    internal const string RetryEventName = "retry";
    internal const string RetryAttemptAttribute = "retry.attempt";
    internal const string RetryDelaySecondsAttribute = "retry.delay_seconds";

    internal const string DeadLetteredEventName = "message.dead_lettered";

    internal const string MetricOperationDuration = "messaging.client.operation.duration";
    internal const string MetricSentMessages = "messaging.client.sent.messages";
    internal const string MetricConsumedMessages = "messaging.client.consumed.messages";
    internal const string MetricConnectionCount = "messaging.client.connection.count";
    internal const string MetricReconnections = "messaging.client.reconnections";
    internal const string MetricRetryAttempts = "kubemq.client.retry.attempts";
    internal const string MetricRetryExhausted = "kubemq.client.retry.exhausted";
}
