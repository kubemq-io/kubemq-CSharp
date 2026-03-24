using System.Net.Http;
using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using KubeMQ.Sdk.Auth;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Protocol;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Manages the gRPC channel lifecycle and delegates every RPC to the
/// generated <c>kubemq.kubemqClient</c>. Implements <see cref="ITransport"/>.
/// </summary>
internal sealed class GrpcTransport : ITransport, IDisposable
{
    private readonly KubeMQClientOptions _options;
    private readonly ILogger _logger;
    private readonly AuthInterceptor? _authInterceptor;
    private volatile GrpcChannel[]? _grpcChannels;
    private volatile KubeMQ.Grpc.kubemq.kubemqClient[]? _grpcClients;
    private long _roundRobinCounter = -1;
    private volatile ConnectionState _transportState = ConnectionState.Idle;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcTransport"/> class.
    /// </summary>
    /// <param name="options">Client configuration.</param>
    /// <param name="logger">Logger instance.</param>
    internal GrpcTransport(KubeMQClientOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;

        ICredentialProvider? resolvedProvider = options.CredentialProvider;

        if (resolvedProvider is null && options.AuthToken is not null)
        {
            resolvedProvider = new StaticTokenProvider(options.AuthToken);
        }

        if (resolvedProvider is not null)
        {
            _authInterceptor = new AuthInterceptor(resolvedProvider, null, logger);
        }
    }

    public ConnectionState State => _transportState;

    internal KubeMQ.Grpc.kubemq.kubemqClient? Client =>
        _grpcClients is { Length: > 0 } clients ? clients[0] : null;

    public void Dispose()
    {
        DisposeChannel();
        _authInterceptor?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync(default).ConfigureAwait(false);
        _authInterceptor?.Dispose();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        DisposeChannel();
        _transportState = ConnectionState.Connecting;

        int count = Math.Max(1, _options.GrpcChannelCount);
        var channels = new GrpcChannel[count];
        var clients = new KubeMQ.Grpc.kubemq.kubemqClient[count];
        var handlers = new SocketsHttpHandler?[count];

        try
        {
            var tls = _options.Tls ?? new TlsOptions();
            string uri = BuildUri(_options.Address, tls.Enabled);
            (string host, int port) = ParseAddress(_options.Address);

            for (int i = 0; i < count; i++)
            {
                handlers[i] = CreateHandler(_options);
                TlsConfigurator.ConfigureTls(handlers[i]!, tls, _logger, _options.Address);

                channels[i] = GrpcChannel.ForAddress(uri, new GrpcChannelOptions
                {
                    HttpHandler = handlers[i],
                    MaxSendMessageSize = _options.MaxSendSize,
                    MaxReceiveMessageSize = _options.MaxReceiveSize,
                });

                handlers[i] = null; // ownership transferred to channels[i]

                CallInvoker invoker = channels[i].CreateCallInvoker();
                invoker = invoker.Intercept(
                    new TelemetryInterceptor(_options.ClientId, host, port, _logger));

                if (_authInterceptor is not null)
                {
                    invoker = invoker.Intercept(_authInterceptor);
                }

                clients[i] = new KubeMQ.Grpc.kubemq.kubemqClient(invoker);
            }

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_options.ConnectionTimeout);

            if (_authInterceptor is not null)
            {
                await _authInterceptor.GetTokenAsync(connectCts.Token).ConfigureAwait(false);
            }

            // CS-6: Ping ALL channels to verify connectivity, not just clients[0].
            // With GrpcChannelCount > 1, channels 1..N-1 could fail silently.
            var pingTasks = new Task[count];
            for (int i = 0; i < count; i++)
            {
                pingTasks[i] = clients[i].PingAsync(
                    new KubeMQ.Grpc.Empty(),
                    cancellationToken: connectCts.Token).ResponseAsync;
            }

            await Task.WhenAll(pingTasks).ConfigureAwait(false);

            // Atomic swap
            _grpcChannels = channels;
            _grpcClients = clients;

            _transportState = ConnectionState.Ready;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            DisposeHandlersAndChannels(handlers, channels);
            DisposeChannel();
            throw new KubeMQTimeoutException(
                $"Connection to {_options.Address} timed out after {_options.ConnectionTimeout}");
        }
        catch (OperationCanceledException)
        {
            DisposeHandlersAndChannels(handlers, channels);
            DisposeChannel();
            throw;
        }
        catch (KubeMQException)
        {
            DisposeHandlersAndChannels(handlers, channels);
            DisposeChannel();
            throw;
        }
        catch (Exception ex)
        {
            DisposeHandlersAndChannels(handlers, channels);
            DisposeChannel();
            throw new KubeMQConnectionException(
                $"Failed to connect to {_options.Address}: {ex.Message}", ex);
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        DisposeChannel();
        _transportState = ConnectionState.Idle;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<ServerInfo> PingAsync(CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            KubeMQ.Grpc.PingResult response = await client.PingAsync(
                new KubeMQ.Grpc.Empty(),
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new ServerInfo
            {
                Host = response.Host,
                Version = response.Version,
                ServerStartTime = response.ServerStartTime,
                ServerUpTimeSeconds = response.ServerUpTimeSeconds,
            };
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "Ping", null, cancellationToken, _options.Address);
        }
    }

    public async Task<KubeMQ.Grpc.Result> SendEventAsync(
        KubeMQ.Grpc.Event grpcEvent,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.SendEventAsync(
                grpcEvent, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "PublishEvent", grpcEvent.Channel, cancellationToken, _options.Address);
        }
    }

    public Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>> CreateEventStreamAsync(
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        return Task.FromResult(client.SendEventsStream(cancellationToken: cancellationToken));
    }

    public async IAsyncEnumerable<KubeMQ.Grpc.EventReceive> SubscribeToEventsAsync(
        KubeMQ.Grpc.Subscribe subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        using var call = client.SubscribeToEvents(
            subscription, cancellationToken: cancellationToken);

        while (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            yield return call.ResponseStream.Current;
        }
    }

    public async Task<KubeMQ.Grpc.SendQueueMessageResult> SendQueueMessageAsync(
        KubeMQ.Grpc.QueueMessage message,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.SendQueueMessageAsync(
                message, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "SendQueueMessage", message.Channel, cancellationToken, _options.Address);
        }
    }

    public async Task<KubeMQ.Grpc.QueueMessagesBatchResponse> SendQueueMessagesBatchAsync(
        KubeMQ.Grpc.QueueMessagesBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.SendQueueMessagesBatchAsync(
                request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "SendQueueMessagesBatch", null, cancellationToken, _options.Address);
        }
    }

    public async Task<KubeMQ.Grpc.ReceiveQueueMessagesResponse> ReceiveQueueMessagesAsync(
        KubeMQ.Grpc.ReceiveQueueMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.ReceiveQueueMessagesAsync(
                request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "ReceiveQueueMessages", request.Channel, cancellationToken, _options.Address);
        }
    }

    public Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse>> CreateUpstreamAsync(
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        return Task.FromResult(client.QueuesUpstream(cancellationToken: cancellationToken));
    }

    public Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse>> CreateDownstreamAsync(
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        return Task.FromResult(client.QueuesDownstream(cancellationToken: cancellationToken));
    }

    public async Task<KubeMQ.Grpc.QueuesDownstreamResponse> ReceiveQueueMessagesAsync(
        KubeMQ.Grpc.QueuesDownstreamRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            using var call = client.QueuesDownstream(cancellationToken: cancellationToken);
            await call.RequestStream.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            // Read the response BEFORE completing the request stream.
            // The server needs the bidirectional stream open while processing.
            if (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
            {
                var response = call.ResponseStream.Current;
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
                return response;
            }

            await call.RequestStream.CompleteAsync().ConfigureAwait(false);
            throw new KubeMQOperationException("No response from QueuesDownstream");
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "PollQueue", request.Channel, cancellationToken, _options.Address);
        }
    }

    public async Task<KubeMQ.Grpc.AckAllQueueMessagesResponse> AckAllQueueMessagesAsync(
        KubeMQ.Grpc.AckAllQueueMessagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.AckAllQueueMessagesAsync(
                request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "AckAllQueueMessages", request.Channel, cancellationToken, _options.Address);
        }
    }

    public async Task<KubeMQ.Grpc.Response> SendCommandAsync(
        KubeMQ.Grpc.Request request,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.SendRequestAsync(
                request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "SendCommand", request.Channel, cancellationToken, _options.Address);
        }
    }

    public async IAsyncEnumerable<KubeMQ.Grpc.Request> SubscribeToCommandsAsync(
        KubeMQ.Grpc.Subscribe subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        using var call = client.SubscribeToRequests(
            subscription, cancellationToken: cancellationToken);

        while (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            yield return call.ResponseStream.Current;
        }
    }

    public async Task SendCommandResponseAsync(
        KubeMQ.Grpc.Response response,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            await client.SendResponseAsync(
                response, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "SendCommandResponse", null, cancellationToken, _options.Address);
        }
    }

    public async Task<KubeMQ.Grpc.Response> SendQueryAsync(
        KubeMQ.Grpc.Request request,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.SendRequestAsync(
                request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "SendQuery", request.Channel, cancellationToken, _options.Address);
        }
    }

    public async IAsyncEnumerable<KubeMQ.Grpc.Request> SubscribeToQueriesAsync(
        KubeMQ.Grpc.Subscribe subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        using var call = client.SubscribeToRequests(
            subscription, cancellationToken: cancellationToken);

        while (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            yield return call.ResponseStream.Current;
        }
    }

    public async Task SendQueryResponseAsync(
        KubeMQ.Grpc.Response response,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            await client.SendResponseAsync(
                response, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "SendQueryResponse", null, cancellationToken, _options.Address);
        }
    }

    public async Task<KubeMQ.Grpc.Response> SendChannelManagementRequestAsync(
        KubeMQ.Grpc.Request request,
        CancellationToken cancellationToken = default)
    {
        var client = GetClientOrThrow();
        try
        {
            return await client.SendRequestAsync(
                request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RpcException rpcEx)
        {
            throw GrpcErrorMapper.MapException(
                rpcEx, "ChannelManagement", request.Channel, cancellationToken, _options.Address);
        }
    }

    public async Task SendBufferedAsync(BufferedMessage msg, CancellationToken ct)
    {
        switch (msg.OperationType)
        {
            case "PublishEvent":
                KubeMQ.Grpc.Event evt = KubeMQ.Grpc.Event.Parser.ParseFrom(msg.Payload);
                await SendEventAsync(evt, ct).ConfigureAwait(false);
                break;
            case "SendQueueMessage":
                KubeMQ.Grpc.QueueMessage qMsg = KubeMQ.Grpc.QueueMessage.Parser.ParseFrom(msg.Payload);
                await SendQueueMessageAsync(qMsg, ct).ConfigureAwait(false);
                break;
            default:
                throw new KubeMQOperationException(
                    $"Unknown buffered operation type: {msg.OperationType}");
        }
    }

    private static (string Host, int Port) ParseAddress(string address)
    {
        string addr = address;
        if (addr.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            addr = addr["http://".Length..];
        }
        else if (addr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            addr = addr["https://".Length..];
        }

        int colonIdx = addr.LastIndexOf(':');
        if (colonIdx > 0 && int.TryParse(addr[(colonIdx + 1)..], out int port))
        {
            return (addr[..colonIdx], port);
        }

        return (addr, 50000);
    }

    private static void DisposeHandlersAndChannels(SocketsHttpHandler?[] handlers, GrpcChannel[] channels)
    {
        foreach (var h in handlers)
        {
            h?.Dispose();
        }

        foreach (var ch in channels)
        {
            ch?.Dispose();
        }
    }

    private static SocketsHttpHandler CreateHandler(KubeMQClientOptions opts)
    {
        return new SocketsHttpHandler
        {
            KeepAlivePingDelay = opts.Keepalive.PingInterval,
            KeepAlivePingTimeout = opts.Keepalive.PingTimeout,
            KeepAlivePingPolicy = opts.Keepalive.PermitWithoutStream
                ? HttpKeepAlivePingPolicy.Always
                : HttpKeepAlivePingPolicy.WithActiveRequests,
            EnableMultipleHttp2Connections = true,
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,

            // Performance: larger HTTP/2 flow-control window reduces round-trips for high-throughput streams.
            InitialHttp2StreamWindowSize = 2 * 1024 * 1024,
        };
    }

    private static string BuildUri(string address, bool tlsEnabled)
    {
        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return address;
        }

        string scheme = tlsEnabled ? "https" : "http";
        return $"{scheme}://{address}";
    }

    private void DisposeChannel()
    {
        var channels = _grpcChannels;
        _grpcChannels = null;
        _grpcClients = null;

        if (channels != null)
        {
            foreach (var ch in channels)
            {
                ch.Dispose();
            }
        }
    }

    private KubeMQ.Grpc.kubemq.kubemqClient GetClientOrThrow()
    {
        var clients = _grpcClients;
        if (clients is null || clients.Length == 0)
        {
            throw new KubeMQConnectionException(
                "Not connected. Call ConnectAsync() first.");
        }

        if (clients.Length == 1)
        {
            return clients[0];
        }

        long idx = Interlocked.Increment(ref _roundRobinCounter);
        return clients[(idx & long.MaxValue) % clients.Length];
    }
}
