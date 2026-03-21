using KubeMQ.Sdk.Common;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Logging;

/// <summary>
/// High-performance structured log messages using LoggerMessage source generators.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 119,
        Level = LogLevel.Warning,
        Message = "Server version {ServerVersion} is outside the tested compatibility range " +
                  "(tested: {MinVersion}\u2013{MaxVersion}). The SDK will continue to operate, but " +
                  "some features may not work as expected. See {MatrixUrl}")]
    internal static partial void ServerVersionOutsideTestedRange(
        ILogger logger, string serverVersion, string minVersion, string maxVersion, string matrixUrl);

    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        Message = "Connected to {Address}")]
    internal static partial void Connected(ILogger logger, string address);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Information,
        Message = "Connection state changed: {PreviousState} -> {CurrentState}")]
    internal static partial void StateChanged(
        ILogger logger,
        ConnectionState previousState,
        ConnectionState currentState);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Warning,
        Message = "State change handler threw an exception")]
    internal static partial void StateChangedHandlerError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 220,
        Level = LogLevel.Information,
        Message = "Reconnect attempt {Attempt} to {Address}, next retry in {Delay}")]
    internal static partial void ReconnectAttempt(
        ILogger logger,
        string address,
        int attempt,
        TimeSpan delay);

    [LoggerMessage(
        EventId = 221,
        Level = LogLevel.Information,
        Message = "Reconnected to {Address} after {Attempt} attempt(s)")]
    internal static partial void Reconnected(ILogger logger, string address, int attempt);

    [LoggerMessage(
        EventId = 222,
        Level = LogLevel.Error,
        Message = "Reconnection to {Address} exhausted after {Attempt} attempt(s)")]
    internal static partial void ReconnectExhausted(ILogger logger, string address, int attempt);

    [LoggerMessage(
        EventId = 223,
        Level = LogLevel.Warning,
        Message = "Discarded {Count} buffered message(s) on close")]
    internal static partial void BufferDiscardedOnClose(ILogger logger, int count);

    [LoggerMessage(
        EventId = 240,
        Level = LogLevel.Warning,
        Message = "Drain timed out after {Timeout}")]
    internal static partial void DrainTimeout(ILogger logger, TimeSpan timeout);

    [LoggerMessage(
        EventId = 241,
        Level = LogLevel.Information,
        Message = "Waiting for {Count} in-flight callbacks to complete (timeout: {Timeout})")]
    internal static partial void WaitingForCallbacks(
        ILogger logger, int count, TimeSpan timeout);

    [LoggerMessage(
        EventId = 242,
        Level = LogLevel.Information,
        Message = "All callbacks completed. {Remaining} remaining.")]
    internal static partial void CallbacksDrained(ILogger logger, int remaining);

    [LoggerMessage(
        EventId = 243,
        Level = LogLevel.Warning,
        Message = "Callback drain timed out after {Timeout}. {Remaining} callbacks still active.")]
    internal static partial void CallbackDrainTimeout(
        ILogger logger, int remaining, TimeSpan timeout);

    [LoggerMessage(
        EventId = 244,
        Level = LogLevel.Error,
        Message = "Callback processing error")]
    internal static partial void CallbackError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 260,
        Level = LogLevel.Information,
        Message = "Subscription restored for channel {Channel} (pattern: {Pattern})")]
    internal static partial void SubscriptionRestored(
        ILogger logger,
        string channel,
        string pattern);

    [LoggerMessage(
        EventId = 261,
        Level = LogLevel.Error,
        Message = "Failed to restore subscription for channel {Channel}")]
    internal static partial void SubscriptionRestoreFailed(
        ILogger logger,
        string channel,
        Exception exception);

    [LoggerMessage(
        EventId = 262,
        Level = LogLevel.Debug,
        Message = "Invalid state transition ignored: {PreviousState} -> {TargetState}")]
    internal static partial void InvalidTransitionIgnored(
        ILogger logger,
        ConnectionState previousState,
        ConnectionState targetState);

    [LoggerMessage(
        EventId = 270,
        Level = LogLevel.Warning,
        Message = "Connection lost to {Address}")]
    internal static partial void ConnectionLost(ILogger logger, string address);

    [LoggerMessage(
        EventId = 271,
        Level = LogLevel.Debug,
        Message = "Keepalive ping sent to {Address}")]
    internal static partial void KeepalivePing(ILogger logger, string address);

    [LoggerMessage(
        EventId = 272,
        Level = LogLevel.Information,
        Message = "Graceful shutdown initiated for {Address}")]
    internal static partial void ShutdownInitiated(ILogger logger, string address);

    [LoggerMessage(
        EventId = 273,
        Level = LogLevel.Information,
        Message = "Graceful shutdown completed for {Address}")]
    internal static partial void ShutdownCompleted(ILogger logger, string address);

    [LoggerMessage(
        EventId = 300,
        Level = LogLevel.Debug,
        Message = "Auth token obtained from credential provider, token_present={TokenPresent}")]
    internal static partial void AuthTokenObtained(ILogger logger, bool tokenPresent);

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Debug,
        Message = "Auth token cache invalidated due to UNAUTHENTICATED response")]
    internal static partial void AuthTokenInvalidated(ILogger logger);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Error,
        Message = "Credential provider failed")]
    internal static partial void CredentialProviderFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 310,
        Level = LogLevel.Information,
        Message = "TLS enabled for {Address}, min version={MinVersion}")]
    internal static partial void TlsEnabled(ILogger logger, string address, string minVersion);

    [LoggerMessage(
        EventId = 311,
        Level = LogLevel.Warning,
        Message = "TLS certificate verification disabled for {Address}")]
    internal static partial void InsecureConnection(ILogger logger, string address);

    [LoggerMessage(
        EventId = 320,
        Level = LogLevel.Debug,
        Message = "Reloading TLS credentials from configured source for {Address}")]
    internal static partial void CertificateReloadAttempted(ILogger logger, string address);

    [LoggerMessage(
        EventId = 321,
        Level = LogLevel.Debug,
        Message = "TLS credentials reloaded successfully for {Address}")]
    internal static partial void CertificateReloadSucceeded(ILogger logger, string address);

    [LoggerMessage(
        EventId = 322,
        Level = LogLevel.Error,
        Message = "Failed to reload TLS credentials for {Address}")]
    internal static partial void CertificateReloadFailed(
        ILogger logger,
        string address,
        Exception exception);

    [LoggerMessage(
        EventId = 330,
        Level = LogLevel.Error,
        Message = "Authentication failed for {Address}")]
    internal static partial void AuthenticationFailed(ILogger logger, string address, Exception exception);

    [LoggerMessage(
        EventId = 331,
        Level = LogLevel.Debug,
        Message = "Token refreshed for {Address}")]
    internal static partial void TokenRefreshed(ILogger logger, string address);

    [LoggerMessage(
        EventId = 332,
        Level = LogLevel.Error,
        Message = "Token refresh failed for {Address}")]
    internal static partial void TokenRefreshFailed(ILogger logger, string address, Exception exception);

    [LoggerMessage(
        EventId = 340,
        Level = LogLevel.Warning,
        Message = "TLS certificate validation skipped (InsecureSkipVerify) for {Address}")]
    internal static partial void InsecureSkipVerify(ILogger logger, string address);

    [LoggerMessage(
        EventId = 341,
        Level = LogLevel.Warning,
        Message = "Reconnect buffer at {Percent}% capacity ({UsedBytes}/{TotalBytes} bytes)")]
    internal static partial void BufferNearCapacity(ILogger logger, int percent, long usedBytes, long totalBytes);

    [LoggerMessage(
        EventId = 342,
        Level = LogLevel.Warning,
        Message = "[DEPRECATED] {ApiName} is deprecated and will be removed in a future version. Use {Replacement} instead.")]
    internal static partial void DeprecatedApiUsage(ILogger logger, string apiName, string replacement);

    [LoggerMessage(
        EventId = 350,
        Level = LogLevel.Warning,
        Message = "Metric cardinality threshold ({Threshold}) exceeded for messaging.destination.name. New channel names will be omitted from metrics.")]
    internal static partial void CardinalityThresholdExceeded(ILogger logger, int threshold);

    [LoggerMessage(
        EventId = 400,
        Level = LogLevel.Debug,
        Message = "Message sent to {Channel} in {DurationMs:F2}ms")]
    internal static partial void MessageSent(ILogger logger, string channel, double durationMs);

    [LoggerMessage(
        EventId = 401,
        Level = LogLevel.Debug,
        Message = "Message received from {Channel}")]
    internal static partial void MessageReceived(ILogger logger, string channel);

    [LoggerMessage(
        EventId = 402,
        Level = LogLevel.Information,
        Message = "Subscription created for {Channel} (group: {Group})")]
    internal static partial void SubscriptionCreated(ILogger logger, string channel, string? group);

    [LoggerMessage(
        EventId = 403,
        Level = LogLevel.Information,
        Message = "Subscription closed for {Channel}")]
    internal static partial void SubscriptionClosed(ILogger logger, string channel);

    [LoggerMessage(
        EventId = 404,
        Level = LogLevel.Error,
        Message = "Send to {Channel} failed")]
    internal static partial void SendFailed(ILogger logger, string channel, Exception exception);

    [LoggerMessage(
        EventId = 405,
        Level = LogLevel.Error,
        Message = "Receive from {Channel} failed")]
    internal static partial void ReceiveFailed(ILogger logger, string channel, Exception exception);

    [LoggerMessage(
        EventId = 406,
        Level = LogLevel.Debug,
        Message = "Batch of {Count} messages sent to {Channel}")]
    internal static partial void BatchSent(ILogger logger, int count, string channel);

    [LoggerMessage(
        EventId = 407,
        Level = LogLevel.Debug,
        Message = "Batch of {Count} messages received from {Channel}")]
    internal static partial void BatchReceived(ILogger logger, int count, string channel);

    [LoggerMessage(
        EventId = 408,
        Level = LogLevel.Debug,
        Message = "Message settled ({Action}) on {Channel}")]
    internal static partial void MessageSettled(ILogger logger, string action, string channel);

    [LoggerMessage(
        EventId = 410,
        Level = LogLevel.Debug,
        Message = "gRPC {MethodName} completed in {DurationMs:F2}ms with error {ErrorType}")]
    internal static partial void GrpcCallFailed(ILogger logger, string methodName, double durationMs, string errorType);

    [LoggerMessage(
        EventId = 500,
        Level = LogLevel.Debug,
        Message = "Retry attempt {Attempt}/{MaxAttempts} for {Operation} on {Channel}, delay {DelayMs:F0}ms")]
    internal static partial void RetryAttempt(ILogger logger, int attempt, int maxAttempts,
        string operation, string channel, double delayMs);

    [LoggerMessage(
        EventId = 501,
        Level = LogLevel.Error,
        Message = "Retries exhausted for {Operation} on {Channel} after {Attempts} attempt(s) in {DurationMs:F0}ms")]
    internal static partial void RetryExhausted(ILogger logger, string operation, string channel,
        int attempts, double durationMs);

    [LoggerMessage(
        EventId = 600,
        Level = LogLevel.Error,
        Message = "Stream broken for {Channel} with {UnackedCount} unacknowledged message(s)")]
    internal static partial void StreamBroken(ILogger logger, string channel, int unackedCount, Exception exception);

    [LoggerMessage(
        EventId = 601,
        Level = LogLevel.Debug,
        Message = "Stream reconnecting for {Channel}")]
    internal static partial void StreamReconnecting(ILogger logger, string channel);

    [LoggerMessage(
        EventId = 700,
        Level = LogLevel.Warning,
        Message = "Downstream settlement error for transaction {TransactionId}: {ErrorMessage}")]
    internal static partial void DownstreamSettlementError(ILogger logger, string transactionId, string errorMessage);

    [LoggerMessage(
        EventId = 701,
        Level = LogLevel.Warning,
        Message = "Downstream stream closed by server (ref: {RefRequestId})")]
    internal static partial void DownstreamCloseByServer(ILogger logger, string refRequestId);

    [LoggerMessage(
        EventId = 702,
        Level = LogLevel.Error,
        Message = "Downstream reader task failed")]
    internal static partial void DownstreamReaderFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 703,
        Level = LogLevel.Information,
        Message = "Downstream stream terminated")]
    internal static partial void DownstreamStreamTerminated(ILogger logger);
}
