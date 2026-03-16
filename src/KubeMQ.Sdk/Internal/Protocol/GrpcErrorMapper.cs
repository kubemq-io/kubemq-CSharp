using System.Security.Authentication;
using Grpc.Core;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// Maps gRPC <see cref="RpcException"/> to typed <see cref="KubeMQException"/> subtypes.
/// Raw gRPC errors MUST NOT leak to SDK callers.
/// </summary>
internal static class GrpcErrorMapper
{
    /// <summary>
    /// Maps a gRPC <see cref="RpcException"/> to the appropriate KubeMQ exception type.
    /// Always preserves the original RpcException as InnerException.
    /// </summary>
    /// <param name="rpcEx">The gRPC exception.</param>
    /// <param name="operation">The SDK operation name (e.g., "PublishEvent").</param>
    /// <param name="channel">The target channel/queue, if applicable.</param>
    /// <param name="callerToken">The caller's CancellationToken, used to distinguish
    /// client-initiated vs server-initiated CANCELLED.</param>
    /// <param name="serverAddress">The server address for diagnostics.</param>
    /// <returns>A typed <see cref="KubeMQException"/> wrapping the gRPC error.</returns>
    internal static KubeMQException MapException(
        RpcException rpcEx,
        string operation,
        string? channel,
        CancellationToken callerToken,
        string? serverAddress = null)
    {
        var (errorCode, category, isRetryable, suggestion) = ClassifyStatus(rpcEx.StatusCode, callerToken);

        string message = FormatMessage(operation, channel, rpcEx.Status.Detail, suggestion, serverAddress);

        if (category == KubeMQErrorCategory.Cancellation)
        {
            throw new OperationCanceledException(message, rpcEx, callerToken);
        }

        KubeMQException mapped = category switch
        {
            KubeMQErrorCategory.Transient or KubeMQErrorCategory.Throttling =>
                rpcEx.StatusCode == StatusCode.Unavailable
                    ? ClassifyTlsErrorOrConnection(rpcEx, message, errorCode, isRetryable)
                    : new KubeMQOperationException(message, errorCode, category, isRetryable, rpcEx),

            KubeMQErrorCategory.Timeout =>
                new KubeMQTimeoutException(message, rpcEx),

            KubeMQErrorCategory.Authentication =>
                new KubeMQAuthenticationException(message, rpcEx),

            KubeMQErrorCategory.Authorization or KubeMQErrorCategory.Validation
                or KubeMQErrorCategory.NotFound or KubeMQErrorCategory.Fatal =>
                new KubeMQOperationException(message, errorCode, category, isRetryable, rpcEx),

            _ => new KubeMQOperationException(message, errorCode, category, isRetryable, rpcEx),
        };

        mapped.Operation = operation;
        mapped.Channel = channel;
        mapped.ServerAddress = serverAddress;
        mapped.GrpcStatusCode = (int)rpcEx.StatusCode;

        return mapped;
    }

    /// <remarks>
    /// The SDK does NOT use CancellationTokenSource.CancelAfter() on the linked CTS
    /// for deadline enforcement. Deadlines are enforced exclusively via CallOptions.Deadline
    /// on the gRPC call. This means StatusCode.DeadlineExceeded is the only timeout signal,
    /// and StatusCode.Cancelled always indicates either client-initiated cancellation
    /// (callerToken.IsCancellationRequested) or server-initiated cancellation.
    /// </remarks>
    private static (KubeMQErrorCode Code, KubeMQErrorCategory Category, bool Retryable, string Suggestion)
        ClassifyStatus(StatusCode status, CancellationToken callerToken)
    {
        return status switch
        {
            StatusCode.OK =>
                throw new InvalidOperationException("OK status should not produce an exception"),

            StatusCode.Cancelled when callerToken.IsCancellationRequested =>
                (KubeMQErrorCode.Cancelled, KubeMQErrorCategory.Cancellation, false,
                 "Operation was cancelled by the caller."),

            StatusCode.Cancelled =>
                (KubeMQErrorCode.Cancelled, KubeMQErrorCategory.Transient, true,
                 "Server cancelled the operation. The SDK will retry automatically."),

            StatusCode.Unknown =>
                (KubeMQErrorCode.Unknown, KubeMQErrorCategory.Transient, true,
                 "Unknown error \u2014 may be transient. Will retry once."),

            StatusCode.InvalidArgument =>
                (KubeMQErrorCode.InvalidArgument, KubeMQErrorCategory.Validation, false,
                 "Check message format and field values."),

            StatusCode.DeadlineExceeded =>
                (KubeMQErrorCode.DeadlineExceeded, KubeMQErrorCategory.Timeout, true,
                 "Increase timeout or check server load."),

            StatusCode.NotFound =>
                (KubeMQErrorCode.NotFound, KubeMQErrorCategory.NotFound, false,
                 "Verify the channel/queue exists. Create it first if needed."),

            StatusCode.AlreadyExists =>
                (KubeMQErrorCode.AlreadyExists, KubeMQErrorCategory.Validation, false,
                 "Resource already exists."),

            StatusCode.PermissionDenied =>
                (KubeMQErrorCode.PermissionDenied, KubeMQErrorCategory.Authorization, false,
                 "Check ACL permissions for this channel."),

            StatusCode.ResourceExhausted =>
                (KubeMQErrorCode.ResourceExhausted, KubeMQErrorCategory.Throttling, true,
                 "Server is rate-limiting. The SDK will retry with extended backoff."),

            StatusCode.FailedPrecondition =>
                (KubeMQErrorCode.FailedPrecondition, KubeMQErrorCategory.Validation, false,
                 "Operation precondition not met. Check server state."),

            StatusCode.Aborted =>
                (KubeMQErrorCode.Aborted, KubeMQErrorCategory.Transient, true,
                 "Transient conflict. The SDK will retry automatically."),

            StatusCode.OutOfRange =>
                (KubeMQErrorCode.OutOfRange, KubeMQErrorCategory.Validation, false,
                 "Value out of acceptable range."),

            StatusCode.Unimplemented =>
                (KubeMQErrorCode.Unimplemented, KubeMQErrorCategory.Fatal, false,
                 "This operation is not supported by the server. Upgrade the server or SDK."),

            StatusCode.Internal =>
                (KubeMQErrorCode.Internal, KubeMQErrorCategory.Fatal, false,
                 "Internal server error. If persistent, contact KubeMQ support."),

            StatusCode.Unavailable =>
                (KubeMQErrorCode.Unavailable, KubeMQErrorCategory.Transient, true,
                 "Server is temporarily unavailable. Check connectivity and firewall rules."),

            StatusCode.DataLoss =>
                (KubeMQErrorCode.DataLoss, KubeMQErrorCategory.Fatal, false,
                 "Unrecoverable data loss. Contact KubeMQ support."),

            StatusCode.Unauthenticated =>
                (KubeMQErrorCode.AuthenticationFailed, KubeMQErrorCategory.Authentication, false,
                 "Verify auth token or TLS certificates."),

            _ =>
                (KubeMQErrorCode.Unknown, KubeMQErrorCategory.Fatal, false,
                 "Unexpected gRPC status code."),
        };
    }

    private static string FormatMessage(
        string operation, string? channel, string? detail, string suggestion, string? serverAddress)
    {
        var channelPart = channel is not null ? $" on channel \"{channel}\"" : string.Empty;
        var detailPart = !string.IsNullOrEmpty(detail) ? $": {detail}" : string.Empty;
        var serverPart = serverAddress is not null ? $" (server: {serverAddress})" : string.Empty;

        return $"{operation} failed{channelPart}{detailPart}{serverPart}. Suggestion: {suggestion}";
    }

    /// <summary>
    /// Refines Unavailable errors: checks for TLS-specific failures before
    /// falling back to generic <see cref="KubeMQConnectionException"/>.
    /// </summary>
    private static KubeMQException ClassifyTlsErrorOrConnection(
        RpcException rpcEx,
        string fallbackMessage,
        KubeMQErrorCode errorCode,
        bool isRetryable)
    {
        if (HasInnerException<AuthenticationException>(rpcEx))
        {
            var authEx = GetInnerException<AuthenticationException>(rpcEx)!;
            var msg = authEx.Message;

            if (msg.Contains("protocol", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("cipher", StringComparison.OrdinalIgnoreCase))
            {
                return new KubeMQConfigurationException(
                    $"TLS version/cipher negotiation failed: {msg}", rpcEx);
            }

            return new KubeMQAuthenticationException(
                $"TLS certificate validation failed: {msg}", rpcEx);
        }

        var detail = rpcEx.Status.Detail ?? string.Empty;
        if (detail.Contains("SSL", StringComparison.Ordinal) ||
            detail.Contains("TLS", StringComparison.Ordinal) ||
            detail.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("validation procedure", StringComparison.OrdinalIgnoreCase))
        {
            if (detail.Contains("expired", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("untrusted", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("hostname", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return new KubeMQAuthenticationException(
                    $"TLS certificate validation failed: {detail}", rpcEx);
            }

            if (detail.Contains("protocol", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("cipher", StringComparison.OrdinalIgnoreCase) ||
                detail.Contains("version", StringComparison.OrdinalIgnoreCase))
            {
                return new KubeMQConfigurationException(
                    $"TLS version/cipher negotiation failed: {detail}", rpcEx);
            }

            return new KubeMQConnectionException(
                $"TLS handshake failed (transient): {detail}", rpcEx);
        }

        return new KubeMQConnectionException(fallbackMessage, errorCode, isRetryable, rpcEx);
    }

    private static bool HasInnerException<T>(Exception ex)
        where T : Exception
    {
        var current = ex.InnerException;
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    private static T? GetInnerException<T>(Exception ex)
        where T : Exception
    {
        var current = ex.InnerException;
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = current.InnerException;
        }

        return null;
    }
}
