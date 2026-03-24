using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Logging;
using KubeMQ.Sdk.Internal.Protocol;
using KubeMQ.Sdk.Internal.Telemetry;
using KubeMQ.Sdk.Internal.Transport;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Client;

/// <summary>
/// Main entry point for KubeMQ operations. A single client instance uses one
/// gRPC channel and multiplexes all operations over it. Create one client
/// and share it across all threads/tasks. Do NOT create a new client per operation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is thread-safe. A single instance should be shared
/// across the application. All publish, subscribe, and management methods may be called
/// concurrently from multiple threads.
/// </para>
/// <para>
/// Implements <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>.
/// Use <c>await using</c> for deterministic cleanup.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public class KubeMQClient : IKubeMQClient
{
    private readonly KubeMQClientOptions? _options;
    private readonly ILogger _logger;
    private readonly StateMachine? _stateMachine;
    private readonly GrpcTransport? _grpcTransport;
    private readonly ITransport? _transport;
    private readonly ConnectionManager? _connectionManager;
    private readonly StreamManager? _streamManager;
    private readonly RetryHandler? _retryHandler;
    private readonly InFlightCallbackTracker _callbackTracker = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly TimeSpan _drainTimeout = TimeSpan.FromSeconds(5);
    private readonly string _serverAddress;
    private readonly int _serverPort;
    private readonly string _requestIdPrefix = Guid.NewGuid().ToString("N")[..8];
    private long _requestIdSeq;
    private int _disposeStarted;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KubeMQClient"/> class.
    /// </summary>
    /// <param name="options">Client configuration options.</param>
    /// <exception cref="KubeMQConfigurationException">
    /// Thrown when <paramref name="options"/> contains invalid values.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is null.
    /// </exception>
    public KubeMQClient(KubeMQClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _options = options;
        _logger = (options.LoggerFactory ?? NullLoggerFactory.Instance)
            .CreateLogger<KubeMQClient>();

        if (string.IsNullOrEmpty(_options.ClientId))
        {
            _options.ClientId = GenerateClientId();
        }

        (_serverAddress, _serverPort) = ParseAddress(_options.Address);

        if (_options.Retry is { Enabled: true, MaxRetries: > 0 })
        {
            _retryHandler = new RetryHandler(_options.Retry, _logger);
        }

        _stateMachine = new StateMachine(_logger);
        _grpcTransport = new GrpcTransport(_options, _logger);
        _transport = _grpcTransport;
        _streamManager = new StreamManager(_logger);
        _connectionManager = new ConnectionManager(
            _options,
            _grpcTransport,
            _stateMachine,
            _streamManager,
            _logger);
        _connectionManager.StateTransitionCallback = RaiseStateChanged;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KubeMQClient"/> class.
    /// Internal constructor for unit testing with a mock <see cref="ITransport"/>.
    /// Accessible from the test project via <c>[InternalsVisibleTo]</c>.
    /// </summary>
    /// <param name="options">Client configuration options.</param>
    /// <param name="testTransport">Mock transport for testing.</param>
    /// <param name="testLogger">Logger instance.</param>
    /// <param name="testRetryHandler">Optional retry handler for testing.</param>
    /// <param name="testConnectionManager">Optional connection manager for testing.</param>
    /// <param name="testStreamManager">Optional stream manager for testing.</param>
    internal KubeMQClient(
        KubeMQClientOptions options,
        ITransport testTransport,
        ILogger testLogger,
        RetryHandler? testRetryHandler = null,
        ConnectionManager? testConnectionManager = null,
        StreamManager? testStreamManager = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(testTransport);
        options.Validate();

        _options = options;
        _logger = testLogger;

        if (string.IsNullOrEmpty(_options.ClientId))
        {
            _options.ClientId = GenerateClientId();
        }

        (_serverAddress, _serverPort) = ParseAddress(_options.Address);
        _retryHandler = testRetryHandler;
        _transport = testTransport;
        _stateMachine = new StateMachine(testLogger);
        _connectionManager = testConnectionManager;
        _streamManager = testStreamManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KubeMQClient"/> class.
    /// Protected constructor for testing/mocking (per CS-28).
    /// </summary>
    protected KubeMQClient()
    {
        _logger = NullLogger<KubeMQClient>.Instance;
        _serverAddress = "localhost";
        _serverPort = 50000;
    }

    /// <summary>
    /// Raised when the connection state changes.
    /// Handlers are invoked asynchronously and MUST NOT block.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ConnectionState State => _stateMachine?.Current ?? ConnectionState.Idle;

    /// <summary>
    /// Disposes resources synchronously. Prefer <see cref="DisposeAsync"/> for async cleanup.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources asynchronously with graceful shutdown.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the async dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Establishes the gRPC connection to the KubeMQ server.
    /// Must be called explicitly before any messaging operations.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the connection attempt.</param>
    /// <returns>A task representing the asynchronous connect operation.</returns>
    /// <exception cref="KubeMQConnectionException">
    /// Thrown when the connection cannot be established.
    /// </exception>
    /// <exception cref="KubeMQTimeoutException">
    /// Thrown when the connection exceeds <see cref="KubeMQClientOptions.ConnectionTimeout"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the client is already connected or connecting.
    /// </exception>
    public virtual async Task ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_stateMachine == null || _transport == null)
        {
            throw new InvalidOperationException("Client is not properly initialized.");
        }

        if (!_stateMachine.TryTransition(
            ConnectionState.Idle, ConnectionState.Connecting))
        {
            throw new InvalidOperationException(
                $"Cannot connect: current state is {_stateMachine.Current}");
        }

        _connectionManager?.ResetReady();
        RaiseStateChanged(ConnectionState.Idle, ConnectionState.Connecting);

        try
        {
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);

            _stateMachine.TryTransition(
                ConnectionState.Connecting, ConnectionState.Ready);
            RaiseStateChanged(ConnectionState.Connecting, ConnectionState.Ready);
            _connectionManager?.NotifyReady();

            // CS-5: Start background health-check after successful connection
            _connectionManager?.StartHealthCheck();

            Log.Connected(_logger, _options!.Address);

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        var serverInfo = await _transport.PingAsync(CancellationToken.None)
                            .ConfigureAwait(false);
                        CheckServerCompatibility(serverInfo);
                    }
                    catch
                    {
                        // Best-effort: compatibility check must never fail the connection.
                    }
                },
                CancellationToken.None);
        }
        catch
        {
            _stateMachine.TryTransition(
                ConnectionState.Connecting, ConnectionState.Idle);
            RaiseStateChanged(ConnectionState.Connecting, ConnectionState.Idle);
            throw;
        }
    }

    /// <summary>
    /// Pings the KubeMQ server and returns server information.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the ping.</param>
    /// <returns>Server information including host, version, and uptime.</returns>
    /// <exception cref="KubeMQConnectionException">
    /// Thrown when the client is not connected.
    /// </exception>
    public virtual async Task<ServerInfo> PingAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_connectionManager != null)
        {
            await _connectionManager.WaitForReadyAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (_transport == null)
        {
            throw new InvalidOperationException("Client is not properly initialized.");
        }

        return await ExecuteWithRetryAsync(
            ct => _transport.PingAsync(ct),
            "Ping",
            null,
            true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task SendEventAsync(
        EventMessage message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        MessageValidator.ValidateEventMessage(message, _options?.MaxMessageBodySize ?? 0);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcEvent = new KubeMQ.Grpc.Event
        {
            EventID = message.Id ?? Guid.NewGuid().ToString("N"),
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? _options?.ClientId ?? string.Empty,
            Metadata = message.Metadata ?? string.Empty,
            Store = false,
        };
        CopyTags(message.Tags, grpcEvent.Tags);

        var sw = Stopwatch.StartNew();
        using var activity = KubeMQActivitySource.StartProducerActivity(
            SemanticConventions.OperationPublish,
            message.Channel,
            _options?.ClientId,
            _serverAddress,
            _serverPort);
        try
        {
            var grpcResult = await ExecuteWithRetryAsync(
                ct => _transport!.SendEventAsync(grpcEvent, ct),
                "SendEvent",
                message.Channel,
                true,
                cancellationToken).ConfigureAwait(false);

            KubeMQMetrics.RecordMessageSent(SemanticConventions.OperationPublish, message.Channel);

            KubeMQMetrics.RecordOperationDuration(sw.Elapsed.TotalSeconds, SemanticConventions.OperationPublish, message.Channel);

            if (!grpcResult.Sent)
            {
                throw new KubeMQOperationException(
                    $"Event send failed: {(string.IsNullOrEmpty(grpcResult.Error) ? "server returned Sent=false" : grpcResult.Error)}");
            }
        }
        catch (Exception ex)
        {
            KubeMQActivitySource.SetError(activity, ex);
            RecordDurationWithError(sw, SemanticConventions.OperationPublish, message.Channel, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task SendEventAsync(
        string channel,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        await SendEventAsync(
            new EventMessage
            {
                Channel = channel,
                Body = body,
                Tags = tags,
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<EventReceived> SubscribeToEventsAsync(
        EventsSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.Validate();

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcSub = new KubeMQ.Grpc.Subscribe
        {
            Channel = subscription.Channel,
            Group = subscription.Group ?? string.Empty,
            ClientID = _options?.ClientId ?? string.Empty,
            SubscribeTypeData = KubeMQ.Grpc.Subscribe.Types.SubscribeType.Events,
        };

        var subscriptionId = Guid.NewGuid().ToString("N");

        await foreach (var item in WithReconnect(
            subscriptionId,
            ct => _transport!.SubscribeToEventsAsync(grpcSub, ct),
            SubscriptionPattern.Events,
            grpcSub,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return MapToEventReceived(item);
        }
    }

    /// <inheritdoc />
    public virtual async Task<EventStream> CreateEventStreamAsync(
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var call = await _transport!.CreateEventStreamAsync(cancellationToken).ConfigureAwait(false);
        return new EventStream(
            call,
            onError,
            reconnectFactory: ct => _transport!.CreateEventStreamAsync(ct),
            waitForReady: ct => WaitForReadyIfNeededAsync(ct));
    }

    /// <inheritdoc />
    public virtual async Task<EventStoreStream> CreateEventStoreStreamAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var call = await _transport!.CreateEventStreamAsync(cancellationToken).ConfigureAwait(false);
        return new EventStoreStream(
            call,
            reconnectFactory: ct => _transport!.CreateEventStreamAsync(ct),
            waitForReady: ct => WaitForReadyIfNeededAsync(ct));
    }

    /// <inheritdoc />
    public virtual async Task<EventStoreResult> SendEventStoreAsync(
        EventStoreMessage message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        MessageValidator.ValidateEventStoreMessage(message, _options?.MaxMessageBodySize ?? 0);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcEvent = new KubeMQ.Grpc.Event
        {
            EventID = message.Id ?? Guid.NewGuid().ToString("N"),
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? _options?.ClientId ?? string.Empty,
            Metadata = message.Metadata ?? string.Empty,
            Store = true,
        };
        CopyTags(message.Tags, grpcEvent.Tags);

        var sw = Stopwatch.StartNew();
        using var activity = KubeMQActivitySource.StartProducerActivity(
            SemanticConventions.OperationPublish,
            message.Channel,
            _options?.ClientId,
            _serverAddress,
            _serverPort);
        try
        {
            var grpcResult = await ExecuteWithRetryAsync(
                ct => _transport!.SendEventAsync(grpcEvent, ct),
                "PublishEventStore",
                message.Channel,
                true,
                cancellationToken).ConfigureAwait(false);

            KubeMQMetrics.RecordMessageSent(SemanticConventions.OperationPublish, message.Channel);

            KubeMQMetrics.RecordOperationDuration(sw.Elapsed.TotalSeconds, SemanticConventions.OperationPublish, message.Channel);

            return new EventStoreResult
            {
                Id = grpcResult.EventID,
                Sent = grpcResult.Sent,
                Error = grpcResult.Error,
            };
        }
        catch (Exception ex)
        {
            KubeMQActivitySource.SetError(activity, ex);
            RecordDurationWithError(sw, SemanticConventions.OperationPublish, message.Channel, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<EventStoreReceived> SubscribeToEventsStoreAsync(
        EventStoreSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.Validate();

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcSub = EncodeEventStoreSubscription(subscription, _options?.ClientId ?? string.Empty);
        var subscriptionId = Guid.NewGuid().ToString("N");
        long lastSequence = 0;

        await foreach (var item in WithReconnect(
            subscriptionId,
            ct =>
            {
                if (lastSequence > 0)
                {
                    grpcSub.EventsStoreTypeData = KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartAtSequence;
                    grpcSub.EventsStoreTypeValue = lastSequence + 1;
                }

                return _transport!.SubscribeToEventsAsync(grpcSub, ct);
            },
            SubscriptionPattern.EventsStore,
            grpcSub,
            item => { lastSequence = (long)item.Sequence; },
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return MapToEventStoreReceived(item);
        }
    }

    /// <inheritdoc />
    public virtual async Task<QueueSendResult> SendQueueMessageAsync(
        QueueMessage message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        MessageValidator.ValidateQueueMessage(message, _options?.MaxMessageBodySize ?? 0);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcMsg = new KubeMQ.Grpc.QueueMessage
        {
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? _options?.ClientId ?? string.Empty,
            Metadata = message.Metadata ?? string.Empty,
            MessageID = Guid.NewGuid().ToString("N"),
        };
        CopyTags(message.Tags, grpcMsg.Tags);

        if (message.DelaySeconds.HasValue)
        {
            grpcMsg.Policy ??= new KubeMQ.Grpc.QueueMessagePolicy();
            grpcMsg.Policy.DelaySeconds = message.DelaySeconds.Value;
        }

        if (message.ExpirationSeconds.HasValue)
        {
            grpcMsg.Policy ??= new KubeMQ.Grpc.QueueMessagePolicy();
            grpcMsg.Policy.ExpirationSeconds = message.ExpirationSeconds.Value;
        }

        if (message.MaxReceiveCount.HasValue)
        {
            grpcMsg.Policy ??= new KubeMQ.Grpc.QueueMessagePolicy();
            grpcMsg.Policy.MaxReceiveCount = message.MaxReceiveCount.Value;
            if (message.MaxReceiveQueue is not null)
            {
                grpcMsg.Policy.MaxReceiveQueue = message.MaxReceiveQueue;
            }
        }

        var sw = Stopwatch.StartNew();
        using var activity = KubeMQActivitySource.StartClientActivity(
            message.Channel,
            _options?.ClientId,
            _serverAddress,
            _serverPort);
        KubeMQ.Grpc.SendQueueMessageResult result;
        try
        {
            result = await ExecuteWithRetryAsync(
                ct => _transport!.SendQueueMessageAsync(grpcMsg, ct),
                "SendQueueMessage",
                message.Channel,
                false,
                cancellationToken).ConfigureAwait(false);

            KubeMQMetrics.RecordMessageSent(SemanticConventions.OperationSend, message.Channel);
        }
        catch (Exception ex)
        {
            KubeMQActivitySource.SetError(activity, ex);
            RecordDurationWithError(sw, SemanticConventions.OperationSend, message.Channel, ex);
            throw;
        }

        KubeMQMetrics.RecordOperationDuration(sw.Elapsed.TotalSeconds, SemanticConventions.OperationSend, message.Channel);

        return new QueueSendResult
        {
            MessageId = result.MessageID,
            SentAt = NanosToDateTimeOffset(result.SentAt),
            IsError = result.IsError,
            Error = string.IsNullOrEmpty(result.Error) ? null : result.Error,
            DelayedTo = result.DelayedTo > 0 ? (int)result.DelayedTo : null,
            ExpiresAt = result.ExpirationAt > 0 ? (int)result.ExpirationAt : null,
        };
    }

    /// <inheritdoc />
    public virtual async Task<QueueSendResult> SendQueueMessageAsync(
        string channel,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return await SendQueueMessageAsync(
            new QueueMessage
            {
                Channel = channel,
                Body = body,
                Tags = tags,
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<QueueSendResult> SendQueueMessagesAsync(
        IEnumerable<QueueMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages.ToList();
        if (messageList.Count == 0)
        {
            return new QueueSendResult { MessageId = string.Empty, IsError = false };
        }

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var batchRequest = new KubeMQ.Grpc.QueueMessagesBatchRequest
        {
            BatchID = Guid.NewGuid().ToString("N"),
        };

        foreach (var msg in messageList)
        {
            MessageValidator.ValidateQueueMessage(msg, _options?.MaxMessageBodySize ?? 0);
            var grpcMsg = new KubeMQ.Grpc.QueueMessage
            {
                MessageID = Guid.NewGuid().ToString("N"),
                ClientID = msg.ClientId ?? _options?.ClientId ?? string.Empty,
                Channel = msg.Channel,
                Metadata = msg.Metadata ?? string.Empty,
                Body = ByteString.CopyFrom(msg.Body.Span),
            };
            CopyTags(msg.Tags, grpcMsg.Tags);

            if (msg.DelaySeconds is > 0 || msg.ExpirationSeconds is > 0 || msg.MaxReceiveCount is > 0)
            {
                grpcMsg.Policy = new KubeMQ.Grpc.QueueMessagePolicy
                {
                    DelaySeconds = msg.DelaySeconds ?? 0,
                    ExpirationSeconds = msg.ExpirationSeconds ?? 0,
                    MaxReceiveCount = msg.MaxReceiveCount ?? 0,
                    MaxReceiveQueue = msg.MaxReceiveQueue ?? string.Empty,
                };
            }

            batchRequest.Messages.Add(grpcMsg);
        }

        var batchResponse = await ExecuteWithRetryAsync(
            ct => _transport!.SendQueueMessagesBatchAsync(batchRequest, ct),
            "SendQueueMessagesBatch",
            null,
            false,
            cancellationToken).ConfigureAwait(false);

        List<QueueSendResult>? perMessageResults = null;
        if (batchResponse.Results is { Count: > 0 })
        {
            perMessageResults = new List<QueueSendResult>(batchResponse.Results.Count);
            foreach (var r in batchResponse.Results)
            {
                perMessageResults.Add(new QueueSendResult
                {
                    MessageId = r.MessageID,
                    SentAt = NanosToDateTimeOffset(r.SentAt),
                    IsError = r.IsError,
                    Error = string.IsNullOrEmpty(r.Error) ? null : r.Error,
                    DelayedTo = r.DelayedTo > 0 ? (int)r.DelayedTo : null,
                    ExpiresAt = r.ExpirationAt > 0 ? (int)r.ExpirationAt : null,
                });
            }
        }

        return new QueueSendResult
        {
            MessageId = batchResponse.BatchID,
            IsError = batchResponse.HaveErrors,
            Error = batchResponse.HaveErrors ? "One or more messages in the batch failed" : string.Empty,
            BatchResults = perMessageResults,
        };
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Handle ownership transfers to receiver")]
    public virtual async Task<QueueDownstreamReceiver> CreateQueueDownstreamReceiverAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);
        var call = await _transport!.CreateDownstreamAsync(CancellationToken.None)
            .ConfigureAwait(false);

        QueueDownstreamReceiver? receiver = null;

        // Handle ownership transfers to the receiver — CA2000 is a false positive.
        var handle = new KubeMQ.Sdk.Internal.Queues.DownstreamStreamHandle(
            call,
            _options?.ClientId ?? string.Empty,
            _logger,
            onError: (txnId, err) =>
            {
                Log.DownstreamSettlementError(_logger, txnId, err);
                receiver?.RaiseOnError(txnId, err);
            },
            onTerminated: () =>
            {
                Log.DownstreamStreamTerminated(_logger);
            });

        receiver = new QueueDownstreamReceiver(
            handle,
            _options?.ClientId ?? string.Empty,
            _serverAddress,
            _serverPort,
            _logger);

        return receiver;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>This is a convenience method that creates a one-shot receiver internally.
    /// Each call opens and closes a gRPC stream. For high-throughput scenarios, use
    /// <see cref="CreateQueueDownstreamReceiverAsync"/> to create a persistent receiver
    /// and call <see cref="QueueDownstreamReceiver.PollAsync"/> in a loop.</para>
    /// <para>This method always forces <see cref="QueuePollRequest.AutoAck"/> to
    /// <see langword="true"/>. Returned messages cannot be individually settled.</para>
    /// </remarks>
    public virtual async Task<QueuePollResponse> ReceiveQueueMessagesAsync(
        QueuePollRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        request.AutoAck = true;
        request.Validate();

        var receiver = await CreateQueueDownstreamReceiverAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (receiver.ConfigureAwait(false))
        {
            var batch = await receiver.PollAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return new QueuePollResponse
            {
                Messages = batch.Messages,
                Error = batch.IsError ? batch.Error : null,
            };
        }
    }

    /// <inheritdoc />
    public virtual async Task<QueuePollResponse> PeekQueueMessagesAsync(
        QueuePollRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new KubeMQ.Grpc.ReceiveQueueMessagesRequest
        {
            RequestID = Guid.NewGuid().ToString("N"),
            ClientID = _options?.ClientId ?? string.Empty,
            Channel = request.Channel,
            MaxNumberOfMessages = request.MaxMessages,
            WaitTimeSeconds = request.WaitTimeoutSeconds,
            IsPeak = true,
        };

        var grpcResponse = await ExecuteWithRetryAsync(
            ct => _transport!.ReceiveQueueMessagesAsync(grpcRequest, ct),
            "PeekQueue",
            request.Channel,
            true,
            cancellationToken).ConfigureAwait(false);

        var messages = new List<QueueMessageReceived>();
        if (grpcResponse.Messages != null)
        {
            foreach (var msg in grpcResponse.Messages)
            {
                messages.Add(MapToQueueMessageReceived(msg));
            }
        }

        if (grpcResponse.IsError && !string.IsNullOrEmpty(grpcResponse.Error))
        {
            return new QueuePollResponse
            {
                Messages = messages.AsReadOnly(),
                Error = grpcResponse.Error,
            };
        }

        return new QueuePollResponse
        {
            Messages = messages.AsReadOnly(),
            Error = null,
        };
    }

    /// <inheritdoc />
    public virtual async Task<QueueReceiveResult> ReceiveQueueMessagesAsync(
        string channel,
        int maxMessages = 1,
        int waitTimeSeconds = 1,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new KubeMQ.Grpc.ReceiveQueueMessagesRequest
        {
            RequestID = Guid.NewGuid().ToString("N"),
            ClientID = _options?.ClientId ?? string.Empty,
            Channel = channel,
            MaxNumberOfMessages = maxMessages,
            WaitTimeSeconds = waitTimeSeconds,
            IsPeak = false,
        };

        var grpcResponse = await ExecuteWithRetryAsync(
            ct => _transport!.ReceiveQueueMessagesAsync(grpcRequest, ct),
            "ReceiveQueueMessages",
            channel,
            false, // destructive dequeue — NOT safe to retry on timeout
            cancellationToken).ConfigureAwait(false);

        var msgs = new List<QueueMessageReceived>();
        if (grpcResponse.Messages != null)
        {
            foreach (var msg in grpcResponse.Messages)
            {
                msgs.Add(MapToQueueMessageReceived(msg));
            }
        }

        return new QueueReceiveResult
        {
            RequestId = grpcResponse.RequestID,
            Messages = msgs.AsReadOnly(),
            MessagesReceived = grpcResponse.MessagesReceived,
            MessagesExpired = grpcResponse.MessagesExpired,
            IsPeak = grpcResponse.IsPeak,
            IsError = grpcResponse.IsError,
            Error = grpcResponse.IsError ? grpcResponse.Error : string.Empty,
        };
    }

    /// <inheritdoc />
    public virtual async Task<QueueUpstreamResult> SendQueueMessagesUpstreamAsync(
        IEnumerable<QueueMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(messages);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new KubeMQ.Grpc.QueuesUpstreamRequest
        {
            RequestID = Guid.NewGuid().ToString("N"),
        };

        foreach (var msg in messages)
        {
            var grpcMsg = new KubeMQ.Grpc.QueueMessage
            {
                MessageID = Guid.NewGuid().ToString("N"),
                ClientID = msg.ClientId ?? _options?.ClientId ?? string.Empty,
                Channel = msg.Channel,
                Body = ByteString.CopyFrom(msg.Body.Span),
            };
            CopyTags(msg.Tags, grpcMsg.Tags);

            if (msg.DelaySeconds is > 0 || msg.ExpirationSeconds is > 0 || msg.MaxReceiveCount is > 0)
            {
                grpcMsg.Policy = new KubeMQ.Grpc.QueueMessagePolicy
                {
                    DelaySeconds = msg.DelaySeconds ?? 0,
                    ExpirationSeconds = msg.ExpirationSeconds ?? 0,
                    MaxReceiveCount = msg.MaxReceiveCount ?? 0,
                    MaxReceiveQueue = msg.MaxReceiveQueue ?? string.Empty,
                };
            }

            grpcRequest.Messages.Add(grpcMsg);
        }

        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            var call = await _transport!.CreateUpstreamAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await call.RequestStream.WriteAsync(grpcRequest, cancellationToken).ConfigureAwait(false);
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);

                if (await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
                {
                    var resp = call.ResponseStream.Current;
                    var results = new List<QueueSendResult>();
                    foreach (var r in resp.Results)
                    {
                        results.Add(new QueueSendResult
                        {
                            MessageId = r.MessageID,
                            SentAt = NanosToDateTimeOffset(r.SentAt),
                            IsError = r.IsError,
                            Error = r.Error,
                        });
                    }

                    return new QueueUpstreamResult
                    {
                        RefRequestId = resp.RefRequestID,
                        Results = results,
                        IsError = resp.IsError,
                        Error = resp.Error,
                    };
                }

                throw new KubeMQOperationException("No response from QueuesUpstream");
            }
            catch (RpcException) when (attempt < maxRetries)
            {
                call.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }
            finally
            {
                call.Dispose();
            }
        }

        throw new KubeMQOperationException("QueuesUpstream failed after retries");
    }

    /// <inheritdoc />
    public virtual async Task<CommandResponse> SendCommandAsync(
        CommandMessage message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        MessageValidator.ValidateCommandMessage(message, _options?.MaxMessageBodySize ?? 0);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        int timeoutMs = message.TimeoutInSeconds.HasValue
            ? message.TimeoutInSeconds.Value * 1000
            : (int)_options!.DefaultTimeout.TotalMilliseconds;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs + 5000);
        var effectiveCt = timeoutCts.Token;

        var grpcRequest = new KubeMQ.Grpc.Request
        {
            RequestID = NextRequestId(),
            RequestTypeData = KubeMQ.Grpc.Request.Types.RequestType.Command,
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? _options?.ClientId ?? string.Empty,
            Metadata = message.Metadata ?? string.Empty,
            Timeout = timeoutMs,
        };
        CopyTags(message.Tags, grpcRequest.Tags);

        long startTimestamp = Stopwatch.GetTimestamp();
        Activity? activity = KubeMQActivitySource.Source.HasListeners()
            ? KubeMQActivitySource.StartClientActivity(
                message.Channel,
                _options?.ClientId,
                _serverAddress,
                _serverPort)
            : null;
        using var activityScope = activity;
        if (activity is not null || System.Diagnostics.Activity.Current is not null)
        {
            grpcRequest.Span = SpanContextSerializer.Serialize(
                activity ?? System.Diagnostics.Activity.Current);
        }

        KubeMQ.Grpc.Response grpcResponse;
        try
        {
            grpcResponse = _retryHandler is not null
                ? await _retryHandler.ExecuteWithRetryAsync(
                    ct => _transport!.SendCommandAsync(grpcRequest, ct),
                    "SendCommand",
                    message.Channel,
                    false,
                    effectiveCt).ConfigureAwait(false)
                : await _transport!.SendCommandAsync(
                    grpcRequest, effectiveCt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            KubeMQActivitySource.SetError(activity, ex);
            RecordDurationWithError(
                startTimestamp,
                SemanticConventions.OperationSend,
                message.Channel,
                ex);
            throw;
        }

        Dictionary<string, string>? commandResponseTags = null;
        if (grpcResponse.Tags is { Count: > 0 })
        {
            commandResponseTags = new Dictionary<string, string>(grpcResponse.Tags);
        }

        return new CommandResponse
        {
            RequestId = grpcResponse.RequestID,
            Executed = grpcResponse.Executed,
            Timestamp = SafeFromUnixTimeSeconds(grpcResponse.Timestamp),
            Error = string.IsNullOrEmpty(grpcResponse.Error) ? null : grpcResponse.Error,
            Body = grpcResponse.Body.Memory,
            Metadata = string.IsNullOrEmpty(grpcResponse.Metadata) ? null : grpcResponse.Metadata,
            Tags = commandResponseTags,
        };
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<CommandReceived> SubscribeToCommandsAsync(
        CommandsSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.Validate();

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcSub = new KubeMQ.Grpc.Subscribe
        {
            Channel = subscription.Channel,
            Group = subscription.Group ?? string.Empty,
            ClientID = _options?.ClientId ?? string.Empty,
            SubscribeTypeData = KubeMQ.Grpc.Subscribe.Types.SubscribeType.Commands,
        };

        var subscriptionId = Guid.NewGuid().ToString("N");

        await foreach (var item in WithReconnect(
            subscriptionId,
            ct => _transport!.SubscribeToCommandsAsync(grpcSub, ct),
            SubscriptionPattern.Commands,
            grpcSub,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return MapToCommandReceived(item);
        }
    }

    /// <inheritdoc />
    public virtual async Task<QueryResponse> SendQueryAsync(
        QueryMessage message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        MessageValidator.ValidateQueryMessage(message, _options?.MaxMessageBodySize ?? 0);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        int timeoutMs = message.TimeoutInSeconds.HasValue
            ? message.TimeoutInSeconds.Value * 1000
            : (int)_options!.DefaultTimeout.TotalMilliseconds;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs + 5000);
        var effectiveCt = timeoutCts.Token;

        var grpcRequest = new KubeMQ.Grpc.Request
        {
            RequestID = NextRequestId(),
            RequestTypeData = KubeMQ.Grpc.Request.Types.RequestType.Query,
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? _options?.ClientId ?? string.Empty,
            Metadata = message.Metadata ?? string.Empty,
            Timeout = timeoutMs,
        };
        CopyTags(message.Tags, grpcRequest.Tags);

        if (message.CacheKey is not null)
        {
            grpcRequest.CacheKey = message.CacheKey;
        }

        if (message.CacheTtlSeconds.HasValue)
        {
            grpcRequest.CacheTTL = message.CacheTtlSeconds.Value;
        }

        long startTimestamp = Stopwatch.GetTimestamp();
        Activity? activity = KubeMQActivitySource.Source.HasListeners()
            ? KubeMQActivitySource.StartClientActivity(
                message.Channel,
                _options?.ClientId,
                _serverAddress,
                _serverPort)
            : null;
        using var activityScope = activity;
        if (activity is not null || System.Diagnostics.Activity.Current is not null)
        {
            grpcRequest.Span = SpanContextSerializer.Serialize(
                activity ?? System.Diagnostics.Activity.Current);
        }

        KubeMQ.Grpc.Response grpcResponse;
        try
        {
            grpcResponse = _retryHandler is not null
                ? await _retryHandler.ExecuteWithRetryAsync(
                    ct => _transport!.SendQueryAsync(grpcRequest, ct),
                    "SendQuery",
                    message.Channel,
                    false,
                    effectiveCt).ConfigureAwait(false)
                : await _transport!.SendQueryAsync(
                    grpcRequest, effectiveCt).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            KubeMQActivitySource.SetError(activity, ex);
            RecordDurationWithError(
                startTimestamp,
                SemanticConventions.OperationSend,
                message.Channel,
                ex);
            throw;
        }

        Dictionary<string, string>? responseTags = null;
        if (grpcResponse.Tags is { Count: > 0 })
        {
            responseTags = new Dictionary<string, string>(grpcResponse.Tags);
        }

        return new QueryResponse
        {
            RequestId = grpcResponse.RequestID,
            Executed = grpcResponse.Executed,
            Body = grpcResponse.Body.Memory,
            Tags = responseTags,
            Timestamp = SafeFromUnixTimeSeconds(grpcResponse.Timestamp),
            Error = string.IsNullOrEmpty(grpcResponse.Error) ? null : grpcResponse.Error,
            CacheHit = grpcResponse.CacheHit,
        };
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<QueryReceived> SubscribeToQueriesAsync(
        QueriesSubscription subscription,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(subscription);
        subscription.Validate();

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcSub = new KubeMQ.Grpc.Subscribe
        {
            Channel = subscription.Channel,
            Group = subscription.Group ?? string.Empty,
            ClientID = _options?.ClientId ?? string.Empty,
            SubscribeTypeData = KubeMQ.Grpc.Subscribe.Types.SubscribeType.Queries,
        };

        var subscriptionId = Guid.NewGuid().ToString("N");

        await foreach (var item in WithReconnect(
            subscriptionId,
            ct => _transport!.SubscribeToQueriesAsync(grpcSub, ct),
            SubscriptionPattern.Queries,
            grpcSub,
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return MapToQueryReceived(item);
        }
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<ChannelInfo>> ListChannelsAsync(
        string channelType,
        string? searchPattern = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(channelType);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new KubeMQ.Grpc.Request
        {
            RequestID = Guid.NewGuid().ToString("N"),
            RequestTypeData = KubeMQ.Grpc.Request.Types.RequestType.Query,
            Channel = "kubemq.cluster.internal.requests",
            Metadata = "list-channels",
            Timeout = (int)OperationDefaults.RpcTimeout.TotalMilliseconds,
            ClientID = _options?.ClientId ?? string.Empty,
        };
        grpcRequest.Tags.Add("channel_type", channelType);
        if (!string.IsNullOrEmpty(searchPattern))
        {
            grpcRequest.Tags.Add("channel_search", searchPattern);
        }

        const int maxSnapshotRetries = 3;
        for (int attempt = 0; attempt <= maxSnapshotRetries; attempt++)
        {
            KubeMQ.Grpc.Response grpcResponse;
            try
            {
                grpcResponse = await ExecuteWithRetryAsync(
                    ct => _transport!.SendChannelManagementRequestAsync(grpcRequest, ct),
                    "ListChannels",
                    channelType,
                    true,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded && attempt < maxSnapshotRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!string.IsNullOrEmpty(grpcResponse.Error)
                && grpcResponse.Error.Contains("cluster snapshot not ready yet", StringComparison.OrdinalIgnoreCase)
                && attempt < maxSnapshotRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                continue;
            }

            return ParseChannelInfoList(grpcResponse);
        }

        return Array.Empty<ChannelInfo>();
    }

    /// <inheritdoc />
    public virtual async Task CreateChannelAsync(
        string channelName,
        string channelType,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelType);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new KubeMQ.Grpc.Request
        {
            RequestID = Guid.NewGuid().ToString("N"),
            RequestTypeData = KubeMQ.Grpc.Request.Types.RequestType.Query,
            Channel = "kubemq.cluster.internal.requests",
            Metadata = "create-channel",
            Timeout = (int)OperationDefaults.RpcTimeout.TotalMilliseconds,
            ClientID = _options?.ClientId ?? string.Empty,
        };
        grpcRequest.Tags.Add("channel_type", channelType);
        grpcRequest.Tags.Add("channel", channelName);
        grpcRequest.Tags.Add("client_id", _options?.ClientId ?? string.Empty);

        await ExecuteWithRetryAsync(
            ct => _transport!.SendChannelManagementRequestAsync(grpcRequest, ct),
            "CreateChannel",
            channelName,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task DeleteChannelAsync(
        string channelName,
        string channelType,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(channelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelType);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new KubeMQ.Grpc.Request
        {
            RequestID = Guid.NewGuid().ToString("N"),
            RequestTypeData = KubeMQ.Grpc.Request.Types.RequestType.Query,
            Channel = "kubemq.cluster.internal.requests",
            Metadata = "delete-channel",
            Timeout = (int)OperationDefaults.RpcTimeout.TotalMilliseconds,
            ClientID = _options?.ClientId ?? string.Empty,
        };
        grpcRequest.Tags.Add("channel_type", channelType);
        grpcRequest.Tags.Add("channel", channelName);

        await ExecuteWithRetryAsync(
            ct => _transport!.SendChannelManagementRequestAsync(grpcRequest, ct),
            "DeleteChannel",
            channelName,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual Task CreateEventsChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => CreateChannelAsync(channelName, "events", cancellationToken);

    /// <inheritdoc />
    public virtual Task CreateEventsStoreChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => CreateChannelAsync(channelName, "events_store", cancellationToken);

    /// <inheritdoc />
    public virtual Task CreateCommandsChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => CreateChannelAsync(channelName, "commands", cancellationToken);

    /// <inheritdoc />
    public virtual Task CreateQueriesChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => CreateChannelAsync(channelName, "queries", cancellationToken);

    /// <inheritdoc />
    public virtual Task CreateQueuesChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => CreateChannelAsync(channelName, "queues", cancellationToken);

    /// <inheritdoc />
    public virtual Task DeleteEventsChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => DeleteChannelAsync(channelName, "events", cancellationToken);

    /// <inheritdoc />
    public virtual Task DeleteEventsStoreChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => DeleteChannelAsync(channelName, "events_store", cancellationToken);

    /// <inheritdoc />
    public virtual Task DeleteCommandsChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => DeleteChannelAsync(channelName, "commands", cancellationToken);

    /// <inheritdoc />
    public virtual Task DeleteQueriesChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => DeleteChannelAsync(channelName, "queries", cancellationToken);

    /// <inheritdoc />
    public virtual Task DeleteQueuesChannelAsync(string channelName, CancellationToken cancellationToken = default)
        => DeleteChannelAsync(channelName, "queues", cancellationToken);

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<ChannelInfo>> ListEventsChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default)
        => ListChannelsAsync("events", searchPattern, cancellationToken);

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<ChannelInfo>> ListEventsStoreChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default)
        => ListChannelsAsync("events_store", searchPattern, cancellationToken);

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<ChannelInfo>> ListCommandsChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default)
        => ListChannelsAsync("commands", searchPattern, cancellationToken);

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<ChannelInfo>> ListQueriesChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default)
        => ListChannelsAsync("queries", searchPattern, cancellationToken);

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<ChannelInfo>> ListQueuesChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default)
        => ListChannelsAsync("queues", searchPattern, cancellationToken);

    /// <inheritdoc />
    public virtual async Task<AckAllResult> AckAllQueueMessagesAsync(
        string channel,
        int waitTimeSeconds = 1,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcRequest = new KubeMQ.Grpc.AckAllQueueMessagesRequest
        {
            RequestID = Guid.NewGuid().ToString("N"),
            ClientID = _options?.ClientId ?? string.Empty,
            Channel = channel,
            WaitTimeSeconds = waitTimeSeconds,
        };

        var grpcResponse = await ExecuteWithRetryAsync(
            ct => _transport!.AckAllQueueMessagesAsync(grpcRequest, ct),
            "AckAllQueueMessages",
            channel,
            false,
            cancellationToken).ConfigureAwait(false);

        return new AckAllResult
        {
            AffectedMessages = (long)grpcResponse.AffectedMessages,
            IsError = grpcResponse.IsError,
            Error = grpcResponse.Error,
        };
    }

    /// <inheritdoc />
    public virtual Task<AckAllResult> PurgeQueueAsync(
        string channel,
        CancellationToken cancellationToken = default)
    {
        return AckAllQueueMessagesAsync(channel, 1, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task SendCommandResponseAsync(
        CommandResponse response,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(response.RequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(response.ReplyChannel);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcResponse = new KubeMQ.Grpc.Response
        {
            RequestID = response.RequestId,
            ReplyChannel = response.ReplyChannel!,
            Executed = response.Executed,
            Error = response.Error ?? string.Empty,
            ClientID = _options?.ClientId ?? string.Empty,
            Body = ByteString.CopyFrom(response.Body.Span),
            Metadata = response.Metadata ?? string.Empty,
            Span = SpanContextSerializer.Serialize(System.Diagnostics.Activity.Current),
        };
        CopyTags(response.Tags, grpcResponse.Tags);

        await ExecuteWithRetryAsync(
            ct => _transport!.SendCommandResponseAsync(grpcResponse, ct),
            "SendCommandResponse",
            response.ReplyChannel!,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task SendQueryResponseAsync(
        QueryResponse response,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(response.RequestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(response.ReplyChannel);

        await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);

        var grpcResponse = new KubeMQ.Grpc.Response
        {
            RequestID = response.RequestId,
            ReplyChannel = response.ReplyChannel!,
            Executed = response.Executed,
            Body = ByteString.CopyFrom(response.Body.Span),
            Error = response.Error ?? string.Empty,
            ClientID = _options?.ClientId ?? string.Empty,
            Span = SpanContextSerializer.Serialize(System.Diagnostics.Activity.Current),
        };
        CopyTags(response.Tags, grpcResponse.Tags);

        await ExecuteWithRetryAsync(
            ct => _transport!.SendQueryResponseAsync(grpcResponse, ct),
            "SendQueryResponse",
            response.ReplyChannel!,
            false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates an auto-generated client ID from hostname, PID, and random suffix.
    /// </summary>
    /// <returns>A unique client identifier.</returns>
    internal static string GenerateClientId()
    {
        string host = Environment.MachineName;
        int pid = Environment.ProcessId;
        int random = Random.Shared.Next(1000, 9999);
        return $"{host}-{pid}-{random}";
    }

    /// <summary>
    /// Synchronous dispose — best-effort cleanup.
    /// </summary>
    /// <param name="disposing">True if called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposeStarted, 1, 0) != 0)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _shutdownCts.Cancel();
            _shutdownCts.Dispose();
            _retryHandler?.Dispose();
            _grpcTransport?.Dispose();
            _connectionManager?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _stateMachine?.ForceDisposed();
            _stateMachine?.Dispose();
            _callbackTracker.Dispose();
        }
    }

    /// <summary>
    /// Async dispose — graceful shutdown with drain.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the async dispose operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.CompareExchange(ref _disposeStarted, 1, 0) != 0)
        {
            return;
        }

        _disposed = true;

        ConnectionState previousState = _stateMachine?.Current ?? ConnectionState.Idle;

        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        if (previousState == ConnectionState.Reconnecting && _connectionManager != null)
        {
            int discarded = _connectionManager.DiscardBuffer();
            if (discarded > 0)
            {
                Log.BufferDiscardedOnClose(_logger, discarded);
            }
        }
        else if (previousState == ConnectionState.Ready && _connectionManager != null)
        {
            await DrainAsync().ConfigureAwait(false);
        }

        await DrainCallbacksAsync().ConfigureAwait(false);

        if (_connectionManager != null)
        {
            await _connectionManager.DisposeAsync().ConfigureAwait(false);
        }

        if (_transport != null)
        {
            await _transport.CloseAsync(default).ConfigureAwait(false);
        }

        ConnectionState oldState = _stateMachine?.ForceDisposed() ?? ConnectionState.Idle;
        RaiseStateChanged(oldState, ConnectionState.Closed);

        _retryHandler?.Dispose();
        _shutdownCts.Dispose();
        _stateMachine?.Dispose();
        _callbackTracker.Dispose();
    }

    private static KubeMQ.Grpc.Subscribe EncodeEventStoreSubscription(
        EventStoreSubscription subscription,
        string clientId)
    {
        var request = new KubeMQ.Grpc.Subscribe
        {
            SubscribeTypeData = KubeMQ.Grpc.Subscribe.Types.SubscribeType.EventsStore,
            ClientID = clientId,
            Channel = subscription.Channel,
            Group = subscription.Group ?? string.Empty,
        };

        switch (subscription.StartPosition)
        {
            case EventStoreStartPosition.StartFromNew:
                request.EventsStoreTypeData = KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartNewOnly;
                request.EventsStoreTypeValue = 0;
                break;

            case EventStoreStartPosition.StartFromFirst:
                request.EventsStoreTypeData = KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartFromFirst;
                request.EventsStoreTypeValue = 0;
                break;

            case EventStoreStartPosition.StartFromLast:
                request.EventsStoreTypeData = KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartFromLast;
                request.EventsStoreTypeValue = 0;
                break;

            case EventStoreStartPosition.StartAtSequence:
                request.EventsStoreTypeData = KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartAtSequence;
                request.EventsStoreTypeValue = subscription.StartSequence!.Value;
                break;

            case EventStoreStartPosition.StartAtTime:
                request.EventsStoreTypeData = KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartAtTime;
                request.EventsStoreTypeValue = subscription.StartTime!.Value.ToUnixTimeSeconds();
                break;

            case EventStoreStartPosition.StartAtTimeDelta:
                request.EventsStoreTypeData = KubeMQ.Grpc.Subscribe.Types.EventsStoreType.StartAtTimeDelta;
                request.EventsStoreTypeValue = subscription.StartTimeDeltaSeconds!.Value;
                break;

            default:
                throw new KubeMQConfigurationException(
                    $"Unknown EventStoreStartPosition: {subscription.StartPosition}");
        }

        return request;
    }

    private static void CopyTags(
        IReadOnlyDictionary<string, string>? source,
        Google.Protobuf.Collections.MapField<string, string> target)
    {
        if (source is null)
        {
            return;
        }

        foreach (KeyValuePair<string, string> kvp in source)
        {
            target[kvp.Key] = kvp.Value;
        }
    }

    private static EventReceived MapToEventReceived(KubeMQ.Grpc.EventReceive item)
    {
        Dictionary<string, string>? tags = null;
        if (item.Tags is { Count: > 0 })
        {
            tags = new Dictionary<string, string>(item.Tags);
        }

        return new EventReceived
        {
            Id = string.IsNullOrEmpty(item.EventID) ? null : item.EventID,
            Channel = item.Channel,
            Body = item.Body.Memory,
            Tags = tags,
            Metadata = string.IsNullOrEmpty(item.Metadata) ? null : item.Metadata,
            Timestamp = NanosToDateTimeOffset(item.Timestamp),
        };
    }

    private static EventStoreReceived MapToEventStoreReceived(KubeMQ.Grpc.EventReceive item)
    {
        Dictionary<string, string>? tags = null;
        if (item.Tags is { Count: > 0 })
        {
            tags = new Dictionary<string, string>(item.Tags);
        }

        return new EventStoreReceived
        {
            Id = string.IsNullOrEmpty(item.EventID) ? null : item.EventID,
            Channel = item.Channel,
            Body = item.Body.Memory,
            Tags = tags,
            Metadata = string.IsNullOrEmpty(item.Metadata) ? null : item.Metadata,
            Sequence = (long)item.Sequence,
            Timestamp = NanosToDateTimeOffset(item.Timestamp),
        };
    }

    private static CommandReceived MapToCommandReceived(KubeMQ.Grpc.Request item)
    {
        Dictionary<string, string>? tags = null;
        if (item.Tags is { Count: > 0 })
        {
            tags = new Dictionary<string, string>(item.Tags);
        }

        return new CommandReceived
        {
            Channel = item.Channel,
            RequestId = item.RequestID,
            Body = item.Body.Memory,
            Tags = tags,
            Metadata = string.IsNullOrEmpty(item.Metadata) ? null : item.Metadata,
            ReplyChannel = string.IsNullOrEmpty(item.ReplyChannel) ? null : item.ReplyChannel,
        };
    }

    private static QueryReceived MapToQueryReceived(KubeMQ.Grpc.Request item)
    {
        Dictionary<string, string>? tags = null;
        if (item.Tags is { Count: > 0 })
        {
            tags = new Dictionary<string, string>(item.Tags);
        }

        return new QueryReceived
        {
            Channel = item.Channel,
            RequestId = item.RequestID,
            Body = item.Body.Memory,
            Tags = tags,
            Metadata = string.IsNullOrEmpty(item.Metadata) ? null : item.Metadata,
            ReplyChannel = string.IsNullOrEmpty(item.ReplyChannel) ? null : item.ReplyChannel,
            CacheKey = string.IsNullOrEmpty(item.CacheKey) ? null : item.CacheKey,
        };
    }

    private static QueueMessageReceived MapToQueueMessageReceived(KubeMQ.Grpc.QueueMessage msg)
    {
        Dictionary<string, string>? tags = null;
        if (msg.Tags is { Count: > 0 })
        {
            tags = new Dictionary<string, string>(msg.Tags);
        }

        return new QueueMessageReceived(
            channel: msg.Channel,
            messageId: msg.MessageID,
            body: msg.Body.Memory,
            tags: tags,
            clientId: string.IsNullOrEmpty(msg.ClientID) ? null : msg.ClientID,
            metadata: string.IsNullOrEmpty(msg.Metadata) ? null : msg.Metadata,
            receiveCount: msg.Attributes?.ReceiveCount ?? 0,
            timestamp: SafeFromUnixTimeSeconds(msg.Attributes?.Timestamp ?? 0),
            ackFunc: null,
            nackFunc: null,
            requeueFunc: null)
        {
            Sequence = (long)(msg.Attributes?.Sequence ?? 0),
            MD5OfBody = msg.Attributes?.MD5OfBody,
        };
    }

    [SuppressMessage("Globalization", "CA1307:Specify StringComparison", Justification = "IndexOf(char) overload is not ambiguous")]
    private static string NormalizeVersion(string version)
    {
        var v = version.TrimStart('v', 'V');

        // Strip SemVer pre-release label (e.g., "3.5.0-beta.1" -> "3.5.0")
        int dashIdx = v.IndexOf('-');
        if (dashIdx > 0)
        {
            v = v[..dashIdx];
        }

        string[] parts = v.Split('.');
        return parts.Length switch
        {
            1 => $"{parts[0]}.0.0",
            2 => $"{parts[0]}.{parts[1]}.0",
            _ => $"{parts[0]}.{parts[1]}.{parts[2]}",
        };
    }

    private static IReadOnlyList<ChannelInfo> ParseChannelInfoList(KubeMQ.Grpc.Response response)
    {
        if (response.Body == null || response.Body.IsEmpty)
        {
            return Array.Empty<ChannelInfo>();
        }

        try
        {
            using var doc = JsonDocument.Parse(response.Body.ToByteArray());
            var list = new List<ChannelInfo>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                list.Add(new ChannelInfo
                {
                    Name = element.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    Type = element.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                    LastActivity = element.TryGetProperty("lastActivity", out var la) ? la.GetInt64() : 0,
                    IsActive = element.TryGetProperty("isActive", out var ia) && ia.GetBoolean(),
                    Incoming = TryParseChannelStats(element, "incoming"),
                    Outgoing = TryParseChannelStats(element, "outgoing"),
                });
            }

            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<ChannelInfo>();
        }
    }

    private static ChannelStats? TryParseChannelStats(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var stats) || stats.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new ChannelStats
        {
            Messages = stats.TryGetProperty("messages", out var m) ? m.GetInt64() : 0,
            Volume = stats.TryGetProperty("volume", out var v) ? v.GetInt64() : 0,
            Waiting = stats.TryGetProperty("waiting", out var w) ? w.GetInt64() : 0,
            Expired = stats.TryGetProperty("expired", out var e) ? e.GetInt64() : 0,
            Delayed = stats.TryGetProperty("delayed", out var d) ? d.GetInt64() : 0,
        };
    }

    private static DateTimeOffset NanosToDateTimeOffset(long nanos)
    {
        if (nanos <= 0)
        {
            return DateTimeOffset.MinValue;
        }

        long ticks = nanos / (1_000_000_000 / TimeSpan.TicksPerSecond);
        if (ticks > DateTimeOffset.MaxValue.Ticks)
        {
            return DateTimeOffset.MaxValue;
        }

        return DateTimeOffset.UnixEpoch.AddTicks(ticks);
    }

    /// <summary>
    /// Safely converts a Unix timestamp to <see cref="DateTimeOffset"/>.
    /// Handles timestamps that may be in nanoseconds instead of seconds
    /// by checking whether the value is in a reasonable seconds range (before year 9999).
    /// </summary>
    private static DateTimeOffset SafeFromUnixTimeSeconds(long ts)
    {
        // 253402300800 = seconds for year 9999-12-31T23:59:59Z
        if (ts > 0 && ts < 253402300800)
        {
            return DateTimeOffset.FromUnixTimeSeconds(ts);
        }

        return DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Safely converts a nullable Unix timestamp to <see cref="DateTimeOffset"/>
    /// for optional timestamp fields (ExpirationAt, DelayedTo).
    /// Returns null if the value is zero or negative.
    /// </summary>
    private static DateTimeOffset? SafeFromUnixTimeSecondsNullable(long ts)
    {
        if (ts <= 0)
        {
            return null;
        }

        // 253402300800 = seconds for year 9999-12-31T23:59:59Z
        if (ts < 253402300800)
        {
            return DateTimeOffset.FromUnixTimeSeconds(ts);
        }

        return DateTimeOffset.UtcNow;
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

    private static void RecordDurationWithError(
        Stopwatch sw,
        string operationName,
        string channel,
        Exception ex)
    {
        string? errorType = ex is KubeMQException ke
            ? KubeMQMetrics.MapErrorType(ke.Category)
            : ex.GetType().Name;
        KubeMQMetrics.RecordOperationDuration(
            sw.Elapsed.TotalSeconds,
            operationName,
            channel,
            errorType);
    }

    private static void RecordDurationWithError(
        long startTimestamp,
        string operationName,
        string channel,
        Exception ex)
    {
        string? errorType = ex is KubeMQException ke
            ? KubeMQMetrics.MapErrorType(ke.Category)
            : ex.GetType().Name;
        KubeMQMetrics.RecordOperationDuration(
            Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
            operationName,
            channel,
            errorType);
    }

    private async IAsyncEnumerable<T> WithReconnect<T>(
        string subscriptionId,
        Func<CancellationToken, IAsyncEnumerable<T>> streamFactory,
        SubscriptionPattern pattern,
        KubeMQ.Grpc.Subscribe grpcSub,
        Action<T>? onItem = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // CS-3: Real resubscription delegate that restarts the stream via streamFactory.
        // The ResubscribeFunc is called by StreamManager.ResubscribeAllAsync after reconnection.
        // It doesn't need to do anything here because WithReconnect's outer while-loop
        // already re-calls streamFactory on each iteration. The reconnection infrastructure
        // (CS-1) triggers the state machine, and WaitForReadyIfNeededAsync unblocks when
        // the connection is re-established, allowing the loop to naturally restart the stream.
        var record = new SubscriptionRecord(
            grpcSub.Channel,
            pattern,
            grpcSub,
            async (parameters, ct) =>
            {
                // The actual resubscription happens in the outer while-loop below.
                // This delegate is a signal that the subscription should be restarted.
                // We just need to ensure the connection is ready.
                if (_connectionManager != null)
                {
                    await _connectionManager.WaitForReadyAsync(ct).ConfigureAwait(false);
                }
            });

        _streamManager?.TrackSubscription(subscriptionId, record);

        try
        {
            int attempt = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                IAsyncEnumerable<T>? stream = null;
                try
                {
                    await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);
                    stream = streamFactory(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (stream == null)
                {
                    yield break;
                }

                bool disconnected = false;
                Exception? lastException = null;
                var enumerator = stream.GetAsyncEnumerator(cancellationToken);
                await using var enumeratorDisposal = enumerator.ConfigureAwait(false);
                while (true)
                {
                    T current;
                    try
                    {
                        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        {
                            yield break;
                        }

                        current = enumerator.Current;
                        attempt = 0; // reset backoff on successful read
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        disconnected = true;
                        lastException = ex;
                        break;
                    }

                    onItem?.Invoke(current);
                    yield return current;
                }

                if (!disconnected)
                {
                    yield break;
                }

                // CS-1: Notify ConnectionManager that the connection was lost.
                // This triggers the reconnection state machine (Reconnecting state)
                // and starts ReconnectLoopAsync in the background.
                _connectionManager?.OnConnectionLost(lastException);

                // CS-2: Exponential backoff before retrying after disconnection.
                // Prevents CPU-burning spin loops when the broker is down.
                var delay = TimeSpan.FromMilliseconds(
                    Math.Min(1000 * (1 << Math.Min(attempt, 5)), 30000));
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                attempt++;

                try
                {
                    await WaitForReadyIfNeededAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
            }
        }
        finally
        {
            _streamManager?.UntrackSubscription(subscriptionId);
        }
    }

    private string NextRequestId()
    {
        return string.Create(
            null,
            stackalloc char[24],
            $"{_requestIdPrefix}{Interlocked.Increment(ref _requestIdSeq):x}");
    }

    private async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        string? channel,
        bool isSafeToRetryOnTimeout,
        CancellationToken cancellationToken)
    {
        if (_retryHandler is not null)
        {
            await _retryHandler.ExecuteWithRetryAsync(
                operation, operationName, channel, isSafeToRetryOnTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await operation(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        string? channel,
        bool isSafeToRetryOnTimeout,
        CancellationToken cancellationToken)
    {
        if (_retryHandler is not null)
        {
            return await _retryHandler.ExecuteWithRetryAsync(
                operation, operationName, channel, isSafeToRetryOnTimeout, cancellationToken)
                .ConfigureAwait(false);
        }

        return await operation(cancellationToken).ConfigureAwait(false);
    }

    [StackTraceHidden]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private async Task WaitForReadyIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_connectionManager != null)
        {
            await _connectionManager.WaitForReadyAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else if (_transport == null)
        {
            throw new InvalidOperationException("Client is not properly initialized.");
        }
    }

    private void RaiseStateChanged(
        ConnectionState oldState,
        ConnectionState newState,
        Exception? error = null)
    {
        var args = new ConnectionStateChangedEventArgs
        {
            PreviousState = oldState,
            CurrentState = newState,
            Timestamp = DateTimeOffset.UtcNow,
            Error = error,
        };

        Log.StateChanged(_logger, oldState, newState);

        EventHandler<ConnectionStateChangedEventArgs>? handler = StateChanged;
        if (handler != null)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    Log.StateChangedHandlerError(_logger, ex);
                }
            });
        }

        if (newState == ConnectionState.Ready)
        {
            _connectionManager?.NotifyReady();
        }
        else if (newState is ConnectionState.Reconnecting or ConnectionState.Connecting)
        {
            _connectionManager?.ResetReady();
        }
    }

    private async Task DrainAsync()
    {
        using var drainCts = new CancellationTokenSource(_drainTimeout);

        try
        {
            if (_connectionManager != null)
            {
                await _connectionManager.FlushBufferAsync(drainCts.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Log.DrainTimeout(_logger, _drainTimeout);
        }
    }

    private async Task DrainCallbacksAsync()
    {
        if (_callbackTracker.ActiveCount == 0)
        {
            return;
        }

        TimeSpan timeout = _options?.CallbackDrainTimeout ?? TimeSpan.FromSeconds(30);

        Log.WaitingForCallbacks(_logger, _callbackTracker.ActiveCount, timeout);

        using var drainCts = new CancellationTokenSource(timeout);

        try
        {
            await _callbackTracker.WaitForAllAsync(drainCts.Token)
                .ConfigureAwait(false);
            Log.CallbacksDrained(_logger, 0);
        }
        catch (OperationCanceledException)
        {
            int remaining = _callbackTracker.ActiveCount;
            Log.CallbackDrainTimeout(_logger, remaining, timeout);
        }
    }

    private void CheckServerCompatibility(ServerInfo serverInfo)
    {
        if (string.IsNullOrWhiteSpace(serverInfo.Version))
        {
            return;
        }

        if (!Version.TryParse(NormalizeVersion(serverInfo.Version), out Version? serverVer))
        {
            return;
        }

        bool belowMin = !string.IsNullOrEmpty(CompatibilityConstants.MinTestedServerVersion)
            && Version.TryParse(CompatibilityConstants.MinTestedServerVersion, out Version? minVer)
            && serverVer < minVer;

        bool aboveMax = !string.IsNullOrEmpty(CompatibilityConstants.MaxTestedServerVersion)
            && Version.TryParse(CompatibilityConstants.MaxTestedServerVersion, out Version? maxVer)
            && serverVer > maxVer;

        if (belowMin || aboveMax)
        {
            string maxDisplay = string.IsNullOrEmpty(CompatibilityConstants.MaxTestedServerVersion)
                ? "latest"
                : CompatibilityConstants.MaxTestedServerVersion;

            Log.ServerVersionOutsideTestedRange(
                _logger,
                serverInfo.Version,
                CompatibilityConstants.MinTestedServerVersion,
                maxDisplay,
                CompatibilityConstants.CompatibilityMatrixUrl);
        }
    }
}
