using Grpc.Core;
using Grpc.Core.Interceptors;
using KubeMQ.Sdk.Internal.Logging;
using KubeMQ.Sdk.Internal.Telemetry;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Protocol;

/// <summary>
/// gRPC interceptor that records metrics for unary calls.
/// Streaming operations record metrics at the KubeMQClient layer instead.
/// </summary>
internal sealed class TelemetryInterceptor : Interceptor
{
    private readonly string _clientId;
    private readonly string _serverAddress;
    private readonly int _serverPort;
    private readonly ILogger _logger;

    internal TelemetryInterceptor(
        string? clientId,
        string serverAddress,
        int serverPort,
        ILogger logger)
    {
        _clientId = clientId ?? string.Empty;
        _serverAddress = serverAddress;
        _serverPort = serverPort;
        _logger = logger;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        if (!KubeMQMetrics.OperationDuration.Enabled)
        {
            return continuation(request, context);
        }

        string methodName = context.Method.Name;
        var sw = ValueStopwatch.StartNew();

        var call = continuation(request, context);

        Task<TResponse> responseAsync = WrapUnaryResponse(call.ResponseAsync, methodName, sw);

        return new AsyncUnaryCall<TResponse>(
            responseAsync,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(request, context);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(context);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        return continuation(context);
    }

    private async Task<TResponse> WrapUnaryResponse<TResponse>(
        Task<TResponse> responseTask,
        string methodName,
        ValueStopwatch sw)
    {
        try
        {
            TResponse response = await responseTask.ConfigureAwait(false);
            double elapsed = sw.GetElapsedTime().TotalSeconds;
            KubeMQMetrics.RecordOperationDuration(elapsed, methodName, string.Empty);
            return response;
        }
        catch (RpcException ex)
        {
            double elapsed = sw.GetElapsedTime().TotalSeconds;
            string errorType = ex.StatusCode.ToString();
            KubeMQMetrics.RecordOperationDuration(
                elapsed,
                methodName,
                string.Empty,
                errorType: errorType);
            Log.GrpcCallFailed(_logger, methodName, elapsed * 1000, errorType);
            throw;
        }
    }
}
