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

        if (category == ErrorCategory.Cancellation)
        {
            throw new OperationCanceledException(message, rpcEx, callerToken);
        }

        KubeMQException mapped = category switch
        {
            ErrorCategory.Transient or ErrorCategory.Throttling =>
                rpcEx.StatusCode == StatusCode.Unavailable
                    ? ClassifyTlsErrorOrConnection(rpcEx, message, errorCode, isRetryable)
                    : new KubeMQOperationException(message, errorCode, category, isRetryable, rpcEx),

            ErrorCategory.Timeout =>
                new KubeMQTimeoutException(message, rpcEx),

            ErrorCategory.Authentication =>
                new KubeMQAuthenticationException(message, rpcEx),

            ErrorCategory.Authorization or ErrorCategory.Validation
                or ErrorCategory.NotFound or ErrorCategory.Fatal =>
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
    private static (ErrorCode Code, ErrorCategory Category, bool Retryable, string Suggestion)
        ClassifyStatus(StatusCode status, CancellationToken callerToken)
    {
        return status switch
        {
            StatusCode.OK =>
                throw new InvalidOperationException("OK status should not produce an exception"),

            StatusCode.Cancelled when callerToken.IsCancellationRequested =>
                (ErrorCode.Cancelled, ErrorCategory.Cancellation, false,
                 "Operation was cancelled by the caller."),

            StatusCode.Cancelled =>
                (ErrorCode.Cancelled, ErrorCategory.Transient, true,
                 "Server cancelled the operation. The SDK will retry automatically."),

            StatusCode.Unknown =>
                (ErrorCode.Unknown, ErrorCategory.Transient, true,
                 "Unknown error \u2014 may be transient. Will retry once."),

            StatusCode.InvalidArgument =>
                (ErrorCode.InvalidArgument, ErrorCategory.Validation, false,
                 "Check message format and field values."),

            StatusCode.DeadlineExceeded =>
                (ErrorCode.DeadlineExceeded, ErrorCategory.Timeout, true,
                 "Increase timeout or check server load."),

            StatusCode.NotFound =>
                (ErrorCode.NotFound, ErrorCategory.NotFound, false,
                 "Verify the channel/queue exists. Create it first if needed."),

            StatusCode.AlreadyExists =>
                (ErrorCode.AlreadyExists, ErrorCategory.Validation, false,
                 "Resource already exists."),

            StatusCode.PermissionDenied =>
                (ErrorCode.PermissionDenied, ErrorCategory.Authorization, false,
                 "Check ACL permissions for this channel."),

            StatusCode.ResourceExhausted =>
                (ErrorCode.ResourceExhausted, ErrorCategory.Throttling, true,
                 "Server is rate-limiting. The SDK will retry with extended backoff."),

            StatusCode.FailedPrecondition =>
                (ErrorCode.FailedPrecondition, ErrorCategory.Validation, false,
                 "Operation precondition not met. Check server state."),

            StatusCode.Aborted =>
                (ErrorCode.Aborted, ErrorCategory.Transient, true,
                 "Transient conflict. The SDK will retry automatically."),

            StatusCode.OutOfRange =>
                (ErrorCode.OutOfRange, ErrorCategory.Validation, false,
                 "Value out of acceptable range."),

            StatusCode.Unimplemented =>
                (ErrorCode.Unimplemented, ErrorCategory.Fatal, false,
                 "This operation is not supported by the server. Upgrade the server or SDK."),

            StatusCode.Internal =>
                (ErrorCode.Internal, ErrorCategory.Fatal, false,
                 "Internal server error. If persistent, contact KubeMQ support."),

            StatusCode.Unavailable =>
                (ErrorCode.Unavailable, ErrorCategory.Transient, true,
                 "Server is temporarily unavailable. Check connectivity and firewall rules."),

            StatusCode.DataLoss =>
                (ErrorCode.DataLoss, ErrorCategory.Fatal, false,
                 "Unrecoverable data loss. Contact KubeMQ support."),

            StatusCode.Unauthenticated =>
                (ErrorCode.AuthenticationFailed, ErrorCategory.Authentication, false,
                 "Verify auth token or TLS certificates."),

            _ =>
                (ErrorCode.Unknown, ErrorCategory.Fatal, false,
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
        ErrorCode errorCode,
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
