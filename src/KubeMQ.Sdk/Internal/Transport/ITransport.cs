using Grpc.Core;
using KubeMQ.Sdk.Common;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Abstraction over gRPC transport for testability (per CS-28).
/// Internal to the SDK — not part of public API surface.
/// </summary>
internal interface ITransport : IAsyncDisposable
{
    /// <summary>Gets the current connection state.</summary>
    ConnectionState State { get; }

    /// <summary>Establishes a connection to the KubeMQ server.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous connect operation.</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the connection gracefully.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous close operation.</returns>
    Task CloseAsync(CancellationToken cancellationToken = default);

    /// <summary>Pings the server and returns server information.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Server information from the ping response.</returns>
    Task<ServerInfo> PingAsync(CancellationToken cancellationToken = default);

    // Events

    /// <summary>Sends a single event to the server.</summary>
    /// <param name="grpcEvent">The gRPC event to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task<KubeMQ.Grpc.Result> SendEventAsync(
        KubeMQ.Grpc.Event grpcEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a bidirectional event stream for high-throughput publishing.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A duplex streaming call for events.</returns>
    Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>> CreateEventStreamAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Subscribes to an event stream.</summary>
    /// <param name="subscription">The subscription request.</param>
    /// <param name="cancellationToken">Token to cancel the subscription.</param>
    /// <returns>An async stream of received events.</returns>
    IAsyncEnumerable<KubeMQ.Grpc.EventReceive> SubscribeToEventsAsync(
        KubeMQ.Grpc.Subscribe subscription,
        CancellationToken cancellationToken = default);

    // Queues

    /// <summary>Sends a queue message.</summary>
    /// <param name="message">The queue message to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The send result from the server.</returns>
    Task<KubeMQ.Grpc.SendQueueMessageResult> SendQueueMessageAsync(
        KubeMQ.Grpc.QueueMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a batch of queue messages.</summary>
    /// <param name="request">The batch request containing messages.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The batch response from the server.</returns>
    Task<KubeMQ.Grpc.QueueMessagesBatchResponse> SendQueueMessagesBatchAsync(
        KubeMQ.Grpc.QueueMessagesBatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Polls for queue messages using the downstream API.</summary>
    /// <param name="request">The downstream poll request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The downstream response containing polled messages.</returns>
    Task<KubeMQ.Grpc.QueuesDownstreamResponse> PollQueueAsync(
        KubeMQ.Grpc.QueuesDownstreamRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Receives queue messages using the simple pull API.</summary>
    /// <param name="request">The receive request configuration.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The receive response containing messages.</returns>
    Task<KubeMQ.Grpc.ReceiveQueueMessagesResponse> ReceiveQueueMessagesAsync(
        KubeMQ.Grpc.ReceiveQueueMessagesRequest request,
        CancellationToken cancellationToken = default);

    // Queue Streams

    /// <summary>Opens an upstream queue stream for sending messages.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A duplex streaming call for upstream queue messages.</returns>
    Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse>> CreateUpstreamAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Opens a downstream queue stream for receiving and processing messages.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A duplex streaming call for downstream queue messages.</returns>
    Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse>> CreateDownstreamAsync(
        CancellationToken cancellationToken = default);

    // Commands

    /// <summary>Sends a command request.</summary>
    /// <param name="request">The gRPC request to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The command response.</returns>
    Task<KubeMQ.Grpc.Response> SendCommandAsync(
        KubeMQ.Grpc.Request request,
        CancellationToken cancellationToken = default);

    /// <summary>Subscribes to incoming command requests.</summary>
    /// <param name="subscription">The subscription request.</param>
    /// <param name="cancellationToken">Token to cancel the subscription.</param>
    /// <returns>An async stream of incoming command requests.</returns>
    IAsyncEnumerable<KubeMQ.Grpc.Request> SubscribeToCommandsAsync(
        KubeMQ.Grpc.Subscribe subscription,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a command response back to the requester.</summary>
    /// <param name="response">The response to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendCommandResponseAsync(
        KubeMQ.Grpc.Response response,
        CancellationToken cancellationToken = default);

    // Queries

    /// <summary>Sends a query request.</summary>
    /// <param name="request">The gRPC request to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The query response.</returns>
    Task<KubeMQ.Grpc.Response> SendQueryAsync(
        KubeMQ.Grpc.Request request,
        CancellationToken cancellationToken = default);

    /// <summary>Subscribes to incoming query requests.</summary>
    /// <param name="subscription">The subscription request.</param>
    /// <param name="cancellationToken">Token to cancel the subscription.</param>
    /// <returns>An async stream of incoming query requests.</returns>
    IAsyncEnumerable<KubeMQ.Grpc.Request> SubscribeToQueriesAsync(
        KubeMQ.Grpc.Subscribe subscription,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a query response back to the requester.</summary>
    /// <param name="response">The response to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendQueryResponseAsync(
        KubeMQ.Grpc.Response response,
        CancellationToken cancellationToken = default);

    // Channel management

    /// <summary>Sends a channel management request (create/delete/list).</summary>
    /// <param name="request">The management request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The management response.</returns>
    Task<KubeMQ.Grpc.Response> SendChannelManagementRequestAsync(
        KubeMQ.Grpc.Request request,
        CancellationToken cancellationToken = default);

    /// <summary>Acknowledges all pending messages in a queue.</summary>
    /// <param name="request">The ack-all request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The ack-all response from the server.</returns>
    Task<KubeMQ.Grpc.AckAllQueueMessagesResponse> AckAllQueueMessagesAsync(
        KubeMQ.Grpc.AckAllQueueMessagesRequest request,
        CancellationToken cancellationToken = default);

    // Buffered replay

    /// <summary>Re-sends a buffered message after reconnection.</summary>
    /// <param name="message">The buffered message to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendBufferedAsync(BufferedMessage message, CancellationToken cancellationToken = default);
}
