using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Client;

/// <summary>
/// Public interface for the KubeMQ client. Enables mocking and DI registration.
/// All methods mirror <see cref="KubeMQClient"/> public virtual methods.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> Implementations of this interface are thread-safe. A single
/// instance should be shared across the application. All publish, subscribe, and
/// management methods may be called concurrently from multiple threads.
/// </para>
/// <para><b>Dual-Dispose contract:</b></para>
/// <para>
/// <see cref="IAsyncDisposable.DisposeAsync"/> is the preferred disposal path. It
/// gracefully shuts down the gRPC channel and cancels active subscriptions.
/// </para>
/// <para>
/// <see cref="IDisposable.Dispose"/> is provided for <c>using</c>-block compatibility
/// and synchronous DI containers. Prefer <c>await using</c> over <c>using</c>.
/// </para>
/// <para>
/// Both methods are idempotent; calling them multiple times is safe.
/// After disposal, all methods throw <see cref="ObjectDisposedException"/>.
/// </para>
/// <para>
/// <b>Exception hierarchy:</b> All SDK methods throw exceptions derived from
/// <see cref="KubeMQException"/>. Raw gRPC errors are never exposed. Common subtypes:
/// <see cref="KubeMQConnectionException"/>, <see cref="KubeMQConfigurationException"/>,
/// <see cref="KubeMQOperationException"/>, <see cref="KubeMQTimeoutException"/>,
/// <see cref="KubeMQAuthenticationException"/>, and <see cref="KubeMQRetryExhaustedException"/>.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public interface IKubeMQClient : IDisposable, IAsyncDisposable
{
    /// <summary>Raised when the connection state changes.</summary>
    event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    /// <summary>Gets the current connection state.</summary>
    ConnectionState State { get; }

    /// <summary>Establishes the gRPC connection to the KubeMQ server.</summary>
    /// <param name="cancellationToken">Optional token to cancel the connection attempt. When triggered,
    /// the method throws <see cref="OperationCanceledException"/> and the client remains in
    /// <see cref="ConnectionState.Disconnected"/> state.</param>
    /// <returns>A task that completes when the connection is established and the client transitions to
    /// <see cref="ConnectionState.Connected"/>.</returns>
    /// <remarks>
    /// <para>Must be called exactly once before any messaging operation. Calling
    /// <see cref="PingAsync"/>, <see cref="PublishEventAsync(EventMessage, CancellationToken)"/>,
    /// or any other operation before connecting will block until the connection is ready
    /// (or fail if not connected).</para>
    /// </remarks>
    /// <exception cref="KubeMQConnectionException">The server is unreachable or the connection was refused.</exception>
    /// <exception cref="KubeMQTimeoutException">The connection attempt exceeded
    /// <see cref="KubeMQClientOptions.ConnectionTimeout"/>.</exception>
    /// <exception cref="KubeMQAuthenticationException">TLS certificate validation failed or the auth token
    /// was rejected.</exception>
    /// <exception cref="InvalidOperationException">The client is already connected or currently connecting.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <example>
    /// <code>
    /// await using var client = new KubeMQClient(new KubeMQClientOptions
    /// {
    ///     Address = "localhost:50000",
    ///     ClientId = "my-client"
    /// });
    /// await client.ConnectAsync(cancellationToken);
    /// </code>
    /// </example>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Pings the KubeMQ server and returns server information.</summary>
    /// <param name="cancellationToken">Optional token to cancel the ping request.</param>
    /// <returns>A <see cref="ServerInfo"/> containing the server's host, version, and uptime.</returns>
    /// <remarks>
    /// <para>Useful for health checks and verifying connectivity. The client must be connected
    /// via <see cref="ConnectAsync"/> before calling this method.</para>
    /// </remarks>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQTimeoutException">The ping exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted (when retry policy
    /// is configured).</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<ServerInfo> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>Publishes an event message to the specified channel.</summary>
    /// <param name="message">The <see cref="EventMessage"/> to publish. Must have a non-empty
    /// <see cref="EventMessage.Channel"/>. The <see cref="EventMessage.Body"/> may be empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the publish operation.</param>
    /// <returns>An <see cref="EventSendResult"/> containing the server-assigned event ID and delivery status.</returns>
    /// <remarks>
    /// <para>Events are fire-and-forget: they are delivered to all active subscribers but are
    /// not persisted. Use <see cref="PublishEventStoreAsync"/> for persistent events.</para>
    /// <para><b>Related types:</b> Build messages with <see cref="EventMessage"/>, receive events
    /// with <see cref="SubscribeToEventsAsync"/>, or use <see cref="CreateEventStreamAsync"/>
    /// for high-throughput streaming.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="message"/> has an invalid channel name
    /// (empty, contains whitespace or wildcards, or ends with a dot).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server rejected the event (e.g., <c>Sent=false</c>).</exception>
    /// <exception cref="KubeMQTimeoutException">The publish exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <example>
    /// <code>
    /// var result = await client.PublishEventAsync(new EventMessage
    /// {
    ///     Channel = "events.notifications",
    ///     Body = System.Text.Encoding.UTF8.GetBytes("Hello, KubeMQ!"),
    ///     Tags = new Dictionary&lt;string, string&gt; { ["source"] = "demo" }
    /// }, cancellationToken);
    /// Console.WriteLine($"Event sent: {result.EventId}");
    /// </code>
    /// </example>
    Task<EventSendResult> PublishEventAsync(EventMessage message, CancellationToken cancellationToken = default);

    /// <summary>Publishes an event message with a byte payload (convenience overload).</summary>
    /// <param name="channel">Target channel name. Must be non-empty, without whitespace or wildcard characters.</param>
    /// <param name="body">Message payload as a byte buffer. May be empty for signal-only events.</param>
    /// <param name="tags">Optional key-value metadata attached to the event. Pass <see langword="null"/> to omit.</param>
    /// <param name="cancellationToken">Optional token to cancel the publish operation.</param>
    /// <returns>An <see cref="EventSendResult"/> containing the server-assigned event ID and delivery status.</returns>
    /// <remarks>
    /// <para>Convenience overload that constructs an <see cref="EventMessage"/> internally.
    /// For full control over event ID and client ID, use
    /// <see cref="PublishEventAsync(EventMessage, CancellationToken)"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="channel"/> is invalid
    /// (empty, contains whitespace or wildcards, or ends with a dot).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server rejected the event.</exception>
    /// <exception cref="KubeMQTimeoutException">The publish exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<EventSendResult> PublishEventAsync(
        string channel,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>Subscribes to events on a channel.</summary>
    /// <param name="subscription">Subscription configuration specifying the channel and optional consumer group.
    /// See <see cref="EventsSubscription"/> for available options.</param>
    /// <param name="cancellationToken">Token to cancel the subscription. Cancelling ends the async enumeration
    /// gracefully.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="EventReceived"/> that yields events as
    /// they arrive. The stream automatically reconnects on transient failures.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Publish events with
    /// <see cref="PublishEventAsync(EventMessage, CancellationToken)"/> or
    /// <see cref="CreateEventStreamAsync"/>. For persistent events, use
    /// <see cref="SubscribeToEventStoreAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="subscription"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="subscription"/> has invalid configuration
    /// (empty channel, whitespace, or trailing dot).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <example>
    /// <code>
    /// var subscription = new EventsSubscription { Channel = "events.notifications" };
    /// await foreach (var ev in client.SubscribeToEventsAsync(subscription, cancellationToken))
    /// {
    ///     Console.WriteLine($"Received on {ev.Channel}: "
    ///         + System.Text.Encoding.UTF8.GetString(ev.Body.Span));
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<EventReceived> SubscribeToEventsAsync(
        EventsSubscription subscription,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a bidirectional event stream for high-throughput publishing.</summary>
    /// <param name="onError">Optional callback invoked when the server reports a stream-level error.
    /// Pass <see langword="null"/> to ignore errors. The callback must not block or throw.</param>
    /// <param name="cancellationToken">Optional token to cancel the stream creation.</param>
    /// <returns>An <see cref="EventStream"/> that can be used to send events with minimal latency.
    /// Dispose the stream when publishing is complete.</returns>
    /// <remarks>
    /// <para>Use this for high-throughput scenarios where the overhead of individual
    /// <see cref="PublishEventAsync(EventMessage, CancellationToken)"/> calls is too high.
    /// The stream multiplexes writes over a single gRPC duplex call.</para>
    /// </remarks>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<EventStream> CreateEventStreamAsync(
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a bidirectional event store stream for high-throughput publishing with confirmation.</summary>
    /// <param name="cancellationToken">Optional token to cancel the stream creation.</param>
    /// <returns>An <see cref="EventStoreStream"/> where each send awaits server-side persistence confirmation.
    /// Dispose the stream when publishing is complete.</returns>
    /// <remarks>
    /// <para>Use this for high-throughput persistent event publishing where each event must be
    /// confirmed as stored before proceeding. For single-event publishing, use
    /// <see cref="PublishEventStoreAsync"/>.</para>
    /// </remarks>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<EventStoreStream> CreateEventStoreStreamAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Publishes an event store message to the specified channel.</summary>
    /// <param name="message">The <see cref="EventStoreMessage"/> to publish. Must have a non-empty
    /// <see cref="EventStoreMessage.Channel"/>. Events are persisted and assigned a sequence number.</param>
    /// <param name="cancellationToken">Optional token to cancel the publish operation.</param>
    /// <returns>An <see cref="EventSendResult"/> containing the event ID and delivery status.</returns>
    /// <remarks>
    /// <para>Unlike <see cref="PublishEventAsync(EventMessage, CancellationToken)"/>, event store messages
    /// are persisted on the server and can be replayed by subscribers using
    /// <see cref="SubscribeToEventStoreAsync"/>.</para>
    /// <para><b>Related types:</b> Build messages with <see cref="EventStoreMessage"/>,
    /// subscribe with <see cref="SubscribeToEventStoreAsync"/>, or use
    /// <see cref="CreateEventStoreStreamAsync"/> for high-throughput streaming.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="message"/> has an invalid channel name
    /// (empty, contains whitespace or wildcards, or ends with a dot).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server rejected the event store message.</exception>
    /// <exception cref="KubeMQTimeoutException">The publish exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<EventSendResult> PublishEventStoreAsync(EventStoreMessage message, CancellationToken cancellationToken = default);

    /// <summary>Subscribes to event store messages on a channel.</summary>
    /// <param name="subscription">Subscription configuration specifying the channel, optional consumer group,
    /// and start position (e.g., from beginning, from sequence, or from timestamp).
    /// See <see cref="EventStoreSubscription"/> for available options.</param>
    /// <param name="cancellationToken">Token to cancel the subscription. Cancelling ends the async enumeration
    /// gracefully.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="EventStoreReceived"/> that yields persisted
    /// events. The stream automatically reconnects on transient failures and resumes from the last received
    /// sequence.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Publish persistent events with <see cref="PublishEventStoreAsync"/>
    /// or <see cref="CreateEventStoreStreamAsync"/>. For non-persistent events, use
    /// <see cref="SubscribeToEventsAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="subscription"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="subscription"/> has invalid configuration
    /// (empty channel, missing required start position fields, wildcards).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    IAsyncEnumerable<EventStoreReceived> SubscribeToEventStoreAsync(
        EventStoreSubscription subscription,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a message to a queue channel.</summary>
    /// <param name="message">The <see cref="QueueMessage"/> to send. Must have a non-empty
    /// <see cref="QueueMessage.Channel"/>. Supports optional <see cref="QueueMessage.DelaySeconds"/>,
    /// <see cref="QueueMessage.ExpirationSeconds"/>, and <see cref="QueueMessage.MaxReceiveCount"/>
    /// for dead-letter queue routing.</param>
    /// <param name="cancellationToken">Optional token to cancel the send operation.</param>
    /// <returns>A <see cref="QueueSendResult"/> containing the message ID, send timestamp, and error status.</returns>
    /// <remarks>
    /// <para>Queue messages are point-to-point: each message is delivered to exactly one consumer.
    /// Use <see cref="PollQueueAsync"/> or <see cref="ReceiveQueueMessagesAsync"/> to consume messages.</para>
    /// <para><b>Related types:</b> Build messages with <see cref="QueueMessage"/>,
    /// poll with <see cref="PollQueueAsync"/>, or send batches with
    /// <see cref="SendQueueMessagesAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="message"/> has an invalid channel name
    /// or negative <see cref="QueueMessage.DelaySeconds"/>, <see cref="QueueMessage.ExpirationSeconds"/>,
    /// or <see cref="QueueMessage.MaxReceiveCount"/> values.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server rejected the message.</exception>
    /// <exception cref="KubeMQTimeoutException">The send exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <example>
    /// <code>
    /// var result = await client.SendQueueMessageAsync(new QueueMessage
    /// {
    ///     Channel = "queues.orders",
    ///     Body = System.Text.Encoding.UTF8.GetBytes("{\"orderId\": 123}"),
    ///     ExpirationSeconds = 3600
    /// }, cancellationToken);
    /// Console.WriteLine($"Sent: {result.MessageId}, Error: {result.IsError}");
    /// </code>
    /// </example>
    Task<QueueSendResult> SendQueueMessageAsync(QueueMessage message, CancellationToken cancellationToken = default);

    /// <summary>Sends a message to a queue channel with a byte payload (convenience overload).</summary>
    /// <param name="channel">Target queue channel name. Must be non-empty, without whitespace or
    /// wildcard characters.</param>
    /// <param name="body">Message payload as a byte buffer. May be empty for signal-only messages.</param>
    /// <param name="tags">Optional key-value metadata attached to the message. Pass <see langword="null"/>
    /// to omit.</param>
    /// <param name="cancellationToken">Optional token to cancel the send operation.</param>
    /// <returns>A <see cref="QueueSendResult"/> containing the message ID, send timestamp, and error status.</returns>
    /// <remarks>
    /// <para>Convenience overload that constructs a <see cref="QueueMessage"/> internally with default
    /// policy (no delay, no expiration, no DLQ). For full control, use
    /// <see cref="SendQueueMessageAsync(QueueMessage, CancellationToken)"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="channel"/> is invalid
    /// (empty, contains whitespace or wildcards, or ends with a dot).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server rejected the message.</exception>
    /// <exception cref="KubeMQTimeoutException">The send exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<QueueSendResult> SendQueueMessageAsync(
        string channel,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends multiple messages to queue channels in a batch.</summary>
    /// <param name="messages">The queue messages to send. Each <see cref="QueueMessage"/> in the collection
    /// is validated independently; messages may target different channels. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Optional token to cancel the batch operation.</param>
    /// <returns>A <see cref="QueueSendResult"/> with aggregate status. When <see cref="QueueSendResult.IsError"/>
    /// is <see langword="true"/>, inspect <see cref="QueueSendResult.BatchResults"/> for per-message
    /// details.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> For single-message sends, use
    /// <see cref="SendQueueMessageAsync(QueueMessage, CancellationToken)"/>. For stream-based batching,
    /// use <see cref="SendQueueMessagesUpstreamAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="messages"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException">One or more messages have invalid configuration
    /// (invalid channel, negative policy values).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The batch request failed at the server level.</exception>
    /// <exception cref="KubeMQTimeoutException">The batch exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<QueueSendResult> SendQueueMessagesAsync(IEnumerable<QueueMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>Polls a queue for messages.</summary>
    /// <param name="request">Poll configuration specifying the channel, max messages, wait timeout, and
    /// auto-ack behavior. See <see cref="QueuePollRequest"/> for available options.
    /// <see cref="QueuePollRequest.MaxMessages"/> must be between 1 and 1024, and
    /// <see cref="QueuePollRequest.WaitTimeoutSeconds"/> between 1 and 3600.</param>
    /// <param name="cancellationToken">Optional token to cancel the poll operation.</param>
    /// <returns>A <see cref="QueuePollResponse"/> containing zero or more <see cref="QueueMessageReceived"/>
    /// messages. When <see cref="QueuePollRequest.AutoAck"/> is <see langword="false"/>, each message
    /// must be individually acknowledged, rejected, or requeued.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Send messages with
    /// <see cref="SendQueueMessageAsync(QueueMessage, CancellationToken)"/>, peek without consuming
    /// with <see cref="PeekQueueAsync"/>, or use the legacy pull API via
    /// <see cref="ReceiveQueueMessagesAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="request"/> has invalid values
    /// (empty channel, non-positive MaxMessages or WaitTimeoutSeconds, MaxMessages &gt; 1024,
    /// WaitTimeoutSeconds &gt; 3600).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The downstream stream returned no response or the server
    /// closed the stream.</exception>
    /// <exception cref="KubeMQTimeoutException">The poll exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <example>
    /// <code>
    /// var response = await client.PollQueueAsync(new QueuePollRequest
    /// {
    ///     Channel = "queues.orders",
    ///     MaxMessages = 10,
    ///     WaitTimeoutSeconds = 5,
    ///     AutoAck = true
    /// }, cancellationToken);
    /// foreach (var msg in response.Messages)
    /// {
    ///     Console.WriteLine($"Message {msg.MessageId}: "
    ///         + System.Text.Encoding.UTF8.GetString(msg.Body.Span));
    /// }
    /// </code>
    /// </example>
    Task<QueuePollResponse> PollQueueAsync(QueuePollRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks at queue messages without consuming them.
    /// </summary>
    /// <remarks>
    /// <para>Uses the <c>ReceiveQueueMessages</c> gRPC call with <c>IsPeak=true</c>.
    /// Messages remain in the queue and are not acknowledged.</para>
    /// <para><b>Related types:</b> To consume messages, use <see cref="PollQueueAsync"/> or
    /// <see cref="ReceiveQueueMessagesAsync"/>. To send messages, use
    /// <see cref="SendQueueMessageAsync(QueueMessage, CancellationToken)"/>.</para>
    /// </remarks>
    /// <param name="request">Poll configuration specifying the channel and message count.
    /// See <see cref="QueuePollRequest"/> for available options.</param>
    /// <param name="cancellationToken">Optional token to cancel the peek operation.</param>
    /// <returns>A <see cref="QueuePollResponse"/> containing zero or more messages. The messages
    /// remain in the queue and can be consumed by a subsequent <see cref="PollQueueAsync"/> call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="request"/> has invalid values
    /// (empty channel, non-positive MaxMessages or WaitTimeoutSeconds).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The peek exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<QueuePollResponse> PeekQueueAsync(QueuePollRequest request, CancellationToken cancellationToken = default);

    /// <summary>Receives messages from a queue via the simple pull API (consumes messages).</summary>
    /// <param name="channel">Queue channel name to receive from. Must be non-empty.</param>
    /// <param name="maxMessages">Maximum number of messages to receive in one call. Defaults to 1.</param>
    /// <param name="waitTimeSeconds">Long-poll wait time in seconds. The server holds the request open for
    /// this duration if no messages are immediately available. Defaults to 1.</param>
    /// <param name="cancellationToken">Optional token to cancel the receive operation.</param>
    /// <returns>A <see cref="QueueReceiveResult"/> containing received messages, counts, and error status.</returns>
    /// <remarks>
    /// <para>This is the legacy pull API. Prefer <see cref="PollQueueAsync"/> for new code, which
    /// supports auto-ack, visibility timeout, and per-message settlement.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="channel"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The receive exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<QueueReceiveResult> ReceiveQueueMessagesAsync(
        string channel,
        int maxMessages = 1,
        int waitTimeSeconds = 1,
        CancellationToken cancellationToken = default);

    /// <summary>Sends messages to a queue via the upstream stream API.</summary>
    /// <param name="messages">Messages to send. Each <see cref="QueueMessage"/> may target a different
    /// channel. Must not be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A <see cref="QueueUpstreamResult"/> containing per-message send results and aggregate
    /// error status.</returns>
    /// <remarks>
    /// <para>Uses a gRPC duplex stream for sending, which may be more efficient than individual
    /// <see cref="SendQueueMessageAsync(QueueMessage, CancellationToken)"/> calls for large batches.
    /// Automatically retries transient gRPC failures up to 3 times.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="messages"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The upstream stream returned no response or failed after
    /// retries.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<QueueUpstreamResult> SendQueueMessagesUpstreamAsync(
        IEnumerable<QueueMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>Receives messages from a queue using the downstream stream API with transactional semantics.</summary>
    /// <param name="channel">Queue channel name to receive from. Must be non-empty.</param>
    /// <param name="maxItems">Maximum number of messages to receive. Defaults to 1.</param>
    /// <param name="waitTimeoutMs">Wait timeout in milliseconds for messages to become available.
    /// Defaults to 10000 (10 seconds).</param>
    /// <param name="autoAck">When <see langword="true"/>, messages are automatically acknowledged upon receipt.
    /// When <see langword="false"/>, you must call settlement methods (e.g.,
    /// <see cref="AckAllDownstreamAsync"/>, <see cref="NAckAllDownstreamAsync"/>) using the returned
    /// <see cref="QueueDownstreamResult.TransactionId"/>. Defaults to <see langword="false"/>.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A <see cref="QueueDownstreamResult"/> containing messages, transaction ID for settlement,
    /// active offsets, and error status.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Settle transactions with <see cref="AckAllDownstreamAsync"/>,
    /// <see cref="AckRangeDownstreamAsync"/>, <see cref="NAckAllDownstreamAsync"/>,
    /// <see cref="NAckRangeDownstreamAsync"/>, <see cref="ReQueueAllDownstreamAsync"/>,
    /// or <see cref="ReQueueRangeDownstreamAsync"/>. Query transaction state with
    /// <see cref="GetActiveOffsetsAsync"/> and <see cref="IsTransactionActiveAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="channel"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server closed the stream, returned no response, or failed
    /// after retries.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<QueueDownstreamResult> ReceiveQueueDownstreamAsync(
        string channel,
        int maxItems = 1,
        int waitTimeoutMs = 10000,
        bool autoAck = false,
        CancellationToken cancellationToken = default);

    /// <summary>Acknowledges all messages in a downstream transaction.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous acknowledge operation.</returns>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task AckAllDownstreamAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>Acknowledges specific messages by sequence range in a downstream transaction.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/> or empty.</param>
    /// <param name="sequenceRange">Sequence numbers of the messages to acknowledge. Obtain from
    /// <see cref="QueueMessageReceived.Sequence"/> or <see cref="GetActiveOffsetsAsync"/>.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous acknowledge operation.</returns>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task AckRangeDownstreamAsync(string transactionId, IEnumerable<long> sequenceRange, CancellationToken cancellationToken = default);

    /// <summary>Rejects all messages in a downstream transaction.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous reject operation.</returns>
    /// <remarks>
    /// <para>Rejected messages are returned to the queue and can be redelivered to another consumer.
    /// Use <see cref="ReQueueAllDownstreamAsync"/> to move messages to a different queue instead.</para>
    /// </remarks>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task NAckAllDownstreamAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>Rejects specific messages by sequence range in a downstream transaction.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/> or empty.</param>
    /// <param name="sequenceRange">Sequence numbers of the messages to reject. Obtain from
    /// <see cref="QueueMessageReceived.Sequence"/> or <see cref="GetActiveOffsetsAsync"/>.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous reject operation.</returns>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task NAckRangeDownstreamAsync(string transactionId, IEnumerable<long> sequenceRange, CancellationToken cancellationToken = default);

    /// <summary>Requeues all messages in a downstream transaction to another queue.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/> or empty.</param>
    /// <param name="reQueueChannel">Target queue channel for the requeued messages. Must be a valid,
    /// non-empty channel name.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous requeue operation.</returns>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task ReQueueAllDownstreamAsync(string transactionId, string reQueueChannel, CancellationToken cancellationToken = default);

    /// <summary>Requeues specific messages by sequence range to another queue.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/> or empty.</param>
    /// <param name="reQueueChannel">Target queue channel for the requeued messages. Must be a valid,
    /// non-empty channel name.</param>
    /// <param name="sequenceRange">Sequence numbers of the messages to requeue. Obtain from
    /// <see cref="QueueMessageReceived.Sequence"/> or <see cref="GetActiveOffsetsAsync"/>.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous requeue operation.</returns>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task ReQueueRangeDownstreamAsync(string transactionId, string reQueueChannel, IEnumerable<long> sequenceRange, CancellationToken cancellationToken = default);

    /// <summary>Gets the list of active (unacknowledged) sequence numbers for a downstream transaction.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/>, empty,
    /// or whitespace.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A read-only list of sequence numbers for messages that have not yet been acknowledged
    /// or rejected.</returns>
    /// <exception cref="ArgumentException"><paramref name="transactionId"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>, or the server returned an error.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<IReadOnlyList<long>> GetActiveOffsetsAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a downstream transaction is still active.</summary>
    /// <param name="transactionId">The <see cref="QueueDownstreamResult.TransactionId"/> from
    /// <see cref="ReceiveQueueDownstreamAsync"/>. Must not be <see langword="null"/>, empty,
    /// or whitespace.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the transaction is still active and messages can be settled;
    /// <see langword="false"/> if the transaction has completed or expired.</returns>
    /// <exception cref="ArgumentException"><paramref name="transactionId"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQOperationException">No active downstream stream exists for the specified
    /// <paramref name="transactionId"/>, or the server returned an error.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<bool> IsTransactionActiveAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>Sends a command to a channel and waits for acknowledgment.</summary>
    /// <param name="message">The <see cref="CommandMessage"/> to send. Must have a non-empty
    /// <see cref="CommandMessage.Channel"/>. The <see cref="CommandMessage.TimeoutInSeconds"/>
    /// controls the server-side deadline; if <see langword="null"/>, the client's
    /// <see cref="KubeMQClientOptions.DefaultTimeout"/> is used.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A <see cref="CommandResponse"/> indicating whether the command was executed and any
    /// error details.</returns>
    /// <remarks>
    /// <para>Commands are fire-and-await-ack RPC: the caller blocks until a subscriber processes
    /// the command and sends a response, or the timeout expires.</para>
    /// <para><b>Related types:</b> Build messages with <see cref="CommandMessage"/>,
    /// handle commands with <see cref="SubscribeToCommandsAsync"/>, respond with
    /// <see cref="SendCommandResponseAsync"/>. For request-reply with data, use
    /// <see cref="SendQueryAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="message"/> has an invalid channel name
    /// or a non-positive <see cref="CommandMessage.TimeoutInSeconds"/>.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error or no subscriber was
    /// available.</exception>
    /// <exception cref="KubeMQTimeoutException">No response was received within the timeout period.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <example>
    /// <code>
    /// var response = await client.SendCommandAsync(new CommandMessage
    /// {
    ///     Channel = "commands.user-service",
    ///     Body = System.Text.Encoding.UTF8.GetBytes("{\"action\": \"deactivate\"}"),
    ///     TimeoutInSeconds = 10
    /// }, cancellationToken);
    /// Console.WriteLine($"Executed: {response.Executed}, Error: {response.Error}");
    /// </code>
    /// </example>
    Task<CommandResponse> SendCommandAsync(CommandMessage message, CancellationToken cancellationToken = default);

    /// <summary>Subscribes to incoming commands on a channel.</summary>
    /// <param name="subscription">Subscription configuration specifying the channel and optional consumer group.
    /// See <see cref="CommandsSubscription"/> for available options.</param>
    /// <param name="cancellationToken">Token to cancel the subscription. Cancelling ends the async enumeration
    /// gracefully.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="CommandReceived"/> that yields incoming
    /// commands. The stream automatically reconnects on transient failures.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Send commands with <see cref="SendCommandAsync"/>,
    /// respond to commands with <see cref="SendCommandResponseAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="subscription"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="subscription"/> has invalid configuration
    /// (empty channel, whitespace, wildcards, or trailing dot).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    IAsyncEnumerable<CommandReceived> SubscribeToCommandsAsync(
        CommandsSubscription subscription,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a query to a channel and waits for a response with data.</summary>
    /// <param name="message">The <see cref="QueryMessage"/> to send. Must have a non-empty
    /// <see cref="QueryMessage.Channel"/>. The <see cref="QueryMessage.TimeoutInSeconds"/>
    /// controls the server-side deadline. Optionally set <see cref="QueryMessage.CacheKey"/> and
    /// <see cref="QueryMessage.CacheTtlSeconds"/> for server-side response caching.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A <see cref="QueryResponse"/> containing the response body, execution status, and
    /// cache-hit indicator.</returns>
    /// <remarks>
    /// <para>Queries are request-reply RPC with optional server-side caching. Unlike
    /// <see cref="SendCommandAsync"/>, queries return data in the response body.</para>
    /// <para><b>Related types:</b> Build messages with <see cref="QueryMessage"/>,
    /// handle queries with <see cref="SubscribeToQueriesAsync"/>, respond with
    /// <see cref="SendQueryResponseAsync"/>. For fire-and-ack without response data, use
    /// <see cref="SendCommandAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="message"/> has an invalid channel name,
    /// a non-positive <see cref="QueryMessage.TimeoutInSeconds"/>, a non-positive
    /// <see cref="QueryMessage.CacheTtlSeconds"/>, or a <see cref="QueryMessage.CacheKey"/>
    /// without a valid TTL.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error or no subscriber was
    /// available.</exception>
    /// <exception cref="KubeMQTimeoutException">No response was received within the timeout period.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    /// <example>
    /// <code>
    /// var response = await client.SendQueryAsync(new QueryMessage
    /// {
    ///     Channel = "queries.inventory",
    ///     Body = System.Text.Encoding.UTF8.GetBytes("{\"sku\": \"ABC-123\"}"),
    ///     TimeoutInSeconds = 15,
    ///     CacheKey = "inventory:ABC-123",
    ///     CacheTtlSeconds = 60
    /// }, cancellationToken);
    /// if (response.Executed)
    /// {
    ///     var data = System.Text.Encoding.UTF8.GetString(response.Body.Span);
    ///     Console.WriteLine($"Result: {data}, CacheHit: {response.CacheHit}");
    /// }
    /// </code>
    /// </example>
    Task<QueryResponse> SendQueryAsync(QueryMessage message, CancellationToken cancellationToken = default);

    /// <summary>Subscribes to incoming queries on a channel.</summary>
    /// <param name="subscription">Subscription configuration specifying the channel and optional consumer group.
    /// See <see cref="QueriesSubscription"/> for available options.</param>
    /// <param name="cancellationToken">Token to cancel the subscription. Cancelling ends the async enumeration
    /// gracefully.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> of <see cref="QueryReceived"/> that yields incoming
    /// queries. The stream automatically reconnects on transient failures.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Send queries with <see cref="SendQueryAsync"/>,
    /// respond to queries with <see cref="SendQueryResponseAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="subscription"/> is <see langword="null"/>.</exception>
    /// <exception cref="KubeMQConfigurationException"><paramref name="subscription"/> has invalid configuration
    /// (empty channel, whitespace, wildcards, or trailing dot).</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    IAsyncEnumerable<QueryReceived> SubscribeToQueriesAsync(
        QueriesSubscription subscription,
        CancellationToken cancellationToken = default);

    /// <summary>Lists channels of the specified type, optionally filtered by a search pattern.</summary>
    /// <param name="channelType">Type of channel to list. Valid values: <c>"events"</c>,
    /// <c>"events_store"</c>, <c>"queues"</c>, <c>"commands"</c>, <c>"queries"</c>.
    /// Must not be <see langword="null"/> or empty.</param>
    /// <param name="searchPattern">Optional regex pattern for filtering channel names. Pass
    /// <see langword="null"/> to return all channels of the specified type.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ChannelInfo"/> describing matching channels, including
    /// activity status, message statistics, and last activity timestamp.</returns>
    /// <remarks>
    /// <para>For type-safe convenience, prefer the typed variants:
    /// <see cref="ListEventsChannelsAsync"/>, <see cref="ListEventsStoreChannelsAsync"/>,
    /// <see cref="ListCommandsChannelsAsync"/>, <see cref="ListQueriesChannelsAsync"/>,
    /// <see cref="ListQueuesChannelsAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="channelType"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<IReadOnlyList<ChannelInfo>> ListChannelsAsync(
        string channelType,
        string? searchPattern = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a channel of the specified type.</summary>
    /// <param name="channelName">Name of the channel to create. Must be non-empty, without whitespace.</param>
    /// <param name="channelType">Type of channel to create. Valid values: <c>"events"</c>,
    /// <c>"events_store"</c>, <c>"queues"</c>, <c>"commands"</c>, <c>"queries"</c>.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is created on the server.</returns>
    /// <remarks>
    /// <para>For type-safe convenience, prefer the typed variants:
    /// <see cref="CreateEventsChannelAsync"/>, <see cref="CreateEventsStoreChannelAsync"/>,
    /// <see cref="CreateCommandsChannelAsync"/>, <see cref="CreateQueriesChannelAsync"/>,
    /// <see cref="CreateQueuesChannelAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> or <paramref name="channelType"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error (e.g., channel already
    /// exists).</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task CreateChannelAsync(
        string channelName,
        string channelType,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a channel of the specified type.</summary>
    /// <param name="channelName">Name of the channel to delete. Must be non-empty.</param>
    /// <param name="channelType">Type of channel to delete. Valid values: <c>"events"</c>,
    /// <c>"events_store"</c>, <c>"queues"</c>, <c>"commands"</c>, <c>"queries"</c>.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is deleted on the server.</returns>
    /// <remarks>
    /// <para>For type-safe convenience, prefer the typed variants:
    /// <see cref="DeleteEventsChannelAsync"/>, <see cref="DeleteEventsStoreChannelAsync"/>,
    /// <see cref="DeleteCommandsChannelAsync"/>, <see cref="DeleteQueriesChannelAsync"/>,
    /// <see cref="DeleteQueuesChannelAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> or <paramref name="channelType"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error (e.g., channel not
    /// found).</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task DeleteChannelAsync(
        string channelName,
        string channelType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a response to a received command.
    /// </summary>
    /// <param name="requestId">The <see cref="CommandReceived.RequestId"/> of the command being responded to.</param>
    /// <param name="replyChannel">The <see cref="CommandReceived.ReplyChannel"/> from the received command.</param>
    /// <param name="executed">Whether the command was executed successfully.</param>
    /// <param name="errorMessage">Optional error description when <paramref name="executed"/> is
    /// <see langword="false"/>. Pass <see langword="null"/> on success.</param>
    /// <param name="body">Optional response payload. Defaults to empty.</param>
    /// <param name="metadata">Optional response metadata string. Defaults to <see langword="null"/>.</param>
    /// <param name="tags">Optional response key-value tags. Defaults to <see langword="null"/>.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the response is sent to the command sender.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Subscribe to commands with <see cref="SubscribeToCommandsAsync"/>,
    /// send commands with <see cref="SendCommandAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="requestId"/> or <paramref name="replyChannel"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The response send exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task SendCommandResponseAsync(
        string requestId,
        string replyChannel,
        bool executed,
        string? errorMessage = null,
        ReadOnlyMemory<byte> body = default,
        string? metadata = null,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a response to a received query, including optional response data.
    /// </summary>
    /// <param name="requestId">The <see cref="QueryReceived.RequestId"/> of the query being responded to.</param>
    /// <param name="replyChannel">The <see cref="QueryReceived.ReplyChannel"/> from the received query.</param>
    /// <param name="body">Response payload. Defaults to empty. Typically contains the query result data.</param>
    /// <param name="executed">Whether the query was executed successfully. Defaults to <see langword="true"/>.</param>
    /// <param name="tags">Optional response key-value metadata. Defaults to <see langword="null"/>.</param>
    /// <param name="errorMessage">Optional error description when <paramref name="executed"/> is
    /// <see langword="false"/>. Pass <see langword="null"/> on success.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the response is sent to the query sender.</returns>
    /// <remarks>
    /// <para><b>Related types:</b> Subscribe to queries with <see cref="SubscribeToQueriesAsync"/>,
    /// send queries with <see cref="SendQueryAsync"/>.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="requestId"/> or <paramref name="replyChannel"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The response send exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task SendQueryResponseAsync(
        string requestId,
        string replyChannel,
        ReadOnlyMemory<byte> body = default,
        bool executed = true,
        IReadOnlyDictionary<string, string>? tags = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an events channel.</summary>
    /// <param name="channelName">Name of the channel to create. Must be non-empty, without whitespace.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is created.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task CreateEventsChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Creates an events store channel.</summary>
    /// <param name="channelName">Name of the channel to create. Must be non-empty, without whitespace.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is created.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task CreateEventsStoreChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Creates a commands channel.</summary>
    /// <param name="channelName">Name of the channel to create. Must be non-empty, without whitespace.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is created.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task CreateCommandsChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Creates a queries channel.</summary>
    /// <param name="channelName">Name of the channel to create. Must be non-empty, without whitespace.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is created.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task CreateQueriesChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Creates a queues channel.</summary>
    /// <param name="channelName">Name of the channel to create. Must be non-empty, without whitespace.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is created.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task CreateQueuesChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Deletes an events channel.</summary>
    /// <param name="channelName">Name of the channel to delete. Must be non-empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is deleted.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task DeleteEventsChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Deletes an events store channel.</summary>
    /// <param name="channelName">Name of the channel to delete. Must be non-empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is deleted.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task DeleteEventsStoreChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a commands channel.</summary>
    /// <param name="channelName">Name of the channel to delete. Must be non-empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is deleted.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task DeleteCommandsChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a queries channel.</summary>
    /// <param name="channelName">Name of the channel to delete. Must be non-empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is deleted.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task DeleteQueriesChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Deletes a queues channel.</summary>
    /// <param name="channelName">Name of the channel to delete. Must be non-empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A task that completes when the channel is deleted.</returns>
    /// <exception cref="ArgumentException"><paramref name="channelName"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task DeleteQueuesChannelAsync(string channelName, CancellationToken cancellationToken = default);

    /// <summary>Lists events channels, optionally filtered by search pattern.</summary>
    /// <param name="searchPattern">Optional regex pattern for filtering channel names. Pass
    /// <see langword="null"/> to return all events channels.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ChannelInfo"/> for matching events channels.</returns>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<IReadOnlyList<ChannelInfo>> ListEventsChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default);

    /// <summary>Lists events store channels, optionally filtered by search pattern.</summary>
    /// <param name="searchPattern">Optional regex pattern for filtering channel names. Pass
    /// <see langword="null"/> to return all events store channels.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ChannelInfo"/> for matching events store channels.</returns>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<IReadOnlyList<ChannelInfo>> ListEventsStoreChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default);

    /// <summary>Lists commands channels, optionally filtered by search pattern.</summary>
    /// <param name="searchPattern">Optional regex pattern for filtering channel names. Pass
    /// <see langword="null"/> to return all commands channels.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ChannelInfo"/> for matching commands channels.</returns>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<IReadOnlyList<ChannelInfo>> ListCommandsChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default);

    /// <summary>Lists queries channels, optionally filtered by search pattern.</summary>
    /// <param name="searchPattern">Optional regex pattern for filtering channel names. Pass
    /// <see langword="null"/> to return all queries channels.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ChannelInfo"/> for matching queries channels.</returns>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<IReadOnlyList<ChannelInfo>> ListQueriesChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default);

    /// <summary>Lists queues channels, optionally filtered by search pattern.</summary>
    /// <param name="searchPattern">Optional regex pattern for filtering channel names. Pass
    /// <see langword="null"/> to return all queues channels.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="ChannelInfo"/> for matching queues channels.</returns>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<IReadOnlyList<ChannelInfo>> ListQueuesChannelsAsync(string? searchPattern = null, CancellationToken cancellationToken = default);

    /// <summary>Acknowledges all pending messages in a queue.</summary>
    /// <param name="channel">Queue channel name whose pending messages should be acknowledged. Must be
    /// non-empty.</param>
    /// <param name="waitTimeSeconds">How long to wait for the server to process the acknowledgment, in
    /// seconds. Defaults to 1. Must be positive.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>An <see cref="AckAllResult"/> with the number of affected messages and error status.</returns>
    /// <exception cref="ArgumentException"><paramref name="channel"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<AckAllResult> AckAllQueueMessagesAsync(
        string channel,
        int waitTimeSeconds = 1,
        CancellationToken cancellationToken = default);

    /// <summary>Purges all messages from a queue without deleting the queue.</summary>
    /// <param name="channel">Queue channel name to purge. Must be non-empty.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>An <see cref="AckAllResult"/> with the number of purged messages and error status.</returns>
    /// <remarks>
    /// <para>Internally delegates to <see cref="AckAllQueueMessagesAsync"/> with a 1-second wait.
    /// The queue itself remains; only its messages are removed.</para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="channel"/> is <see langword="null"/>, empty,
    /// or whitespace.</exception>
    /// <exception cref="KubeMQConnectionException">The client is not connected or the server is unreachable.</exception>
    /// <exception cref="KubeMQOperationException">The server returned an error.</exception>
    /// <exception cref="KubeMQTimeoutException">The operation exceeded the server deadline.</exception>
    /// <exception cref="KubeMQAuthenticationException">Authentication credentials are invalid or expired.</exception>
    /// <exception cref="KubeMQRetryExhaustedException">All retry attempts were exhausted.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    Task<AckAllResult> PurgeQueueAsync(
        string channel,
        CancellationToken cancellationToken = default);
}
