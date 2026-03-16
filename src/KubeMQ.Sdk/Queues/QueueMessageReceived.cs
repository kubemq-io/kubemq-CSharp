using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// A queue message received from a poll operation. Supports ack, reject, and requeue.
/// This is a class (not record) because it holds a reference to the transport
/// for settlement operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> Properties are read-only and safe to access from any thread.
/// Settle methods (<see cref="AckAsync"/>, <see cref="RejectAsync"/>,
/// <see cref="RequeueAsync"/>) are thread-safe but enforce exactly-once semantics —
/// only the first settle call takes effect; subsequent calls throw
/// <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public class QueueMessageReceived
{
    private readonly Func<string, CancellationToken, Task>? _ackFunc;
    private readonly Func<string, CancellationToken, Task>? _rejectFunc;
    private readonly Func<string, string?, CancellationToken, Task>? _requeueFunc;
    private readonly Func<string, int, CancellationToken, Task>? _extendFunc;
    private int _settled;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueMessageReceived"/> class.
    /// Internal — only the SDK transport layer constructs these when polling queue messages.
    /// </summary>
    /// <param name="channel">Channel the message was received from.</param>
    /// <param name="messageId">Server-assigned message ID.</param>
    /// <param name="body">Message payload.</param>
    /// <param name="tags">Key-value metadata (may be null).</param>
    /// <param name="clientId">Sender's client ID (may be null).</param>
    /// <param name="metadata">Optional metadata string (may be null).</param>
    /// <param name="receiveCount">How many times this message has been delivered.</param>
    /// <param name="timestamp">Server enqueue timestamp.</param>
    /// <param name="ackFunc">Delegate to acknowledge the message via the transport.</param>
    /// <param name="rejectFunc">Delegate to reject (NAK) the message via the transport.</param>
    /// <param name="requeueFunc">Delegate to requeue the message to a different channel.</param>
    /// <param name="extendFunc">Delegate to extend the visibility timeout.</param>
    internal QueueMessageReceived(
        string channel,
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string>? tags,
        string? clientId,
        string? metadata,
        int receiveCount,
        DateTimeOffset timestamp,
        Func<string, CancellationToken, Task>? ackFunc,
        Func<string, CancellationToken, Task>? rejectFunc,
        Func<string, string?, CancellationToken, Task>? requeueFunc,
        Func<string, int, CancellationToken, Task>? extendFunc)
    {
        Channel = channel;
        MessageId = messageId;
        Body = body;
        Tags = tags;
        ClientId = clientId;
        Metadata = metadata;
        ReceiveCount = receiveCount;
        Timestamp = timestamp;
        _ackFunc = ackFunc;
        _rejectFunc = rejectFunc;
        _requeueFunc = requeueFunc;
        _extendFunc = extendFunc;
    }

    /// <summary>Gets the channel the message was received from.</summary>
    public string Channel { get; }

    /// <summary>Gets the server-assigned unique message identifier.</summary>
    public string MessageId { get; }

    /// <summary>Gets the message payload.</summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>Gets the optional key-value metadata.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; }

    /// <summary>Gets the client ID of the sender.</summary>
    public string? ClientId { get; }

    /// <summary>Gets the optional metadata string.</summary>
    public string? Metadata { get; }

    /// <summary>Gets the number of times this message has been received (for DLQ tracking).</summary>
    public int ReceiveCount { get; }

    /// <summary>Gets the server timestamp when the message was enqueued.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the server-assigned sequence number for this message.</summary>
    public long Sequence { get; init; }

    /// <summary>Gets the MD5 hash of the message body, if provided by the server.</summary>
    public string? MD5OfBody { get; init; }

    /// <summary>Gets a value indicating whether this message was re-routed from another queue (e.g., DLQ).</summary>
    public bool ReRouted { get; init; }

    /// <summary>Gets the original queue name this message was re-routed from, if applicable.</summary>
    public string? ReRoutedFromQueue { get; init; }

    /// <summary>Gets the expiration timestamp, or null if the message does not expire.</summary>
    public DateTimeOffset? ExpirationAt { get; init; }

    /// <summary>Gets the timestamp when a delayed message becomes visible, or null if not delayed.</summary>
    public DateTimeOffset? DelayedTo { get; init; }

    /// <summary>
    /// Acknowledges the message, removing it from the queue.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous acknowledge operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message has already been settled.</exception>
    public async Task AckAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoSettlementDelegate(_ackFunc, "Ack");
        await _ackFunc!(MessageId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects the message, signaling processing failure.
    /// The server may redeliver based on queue configuration.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous reject operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message has already been settled or was received via PollQueueAsync.</exception>
    public async Task RejectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoSettlementDelegate(_rejectFunc, "Reject");
        await _rejectFunc!(MessageId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Requeues the message to the same or a different channel.
    /// </summary>
    /// <param name="channel">Optional target channel. Null means requeue to the original channel.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous requeue operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message has already been settled or was received via PollQueueAsync.</exception>
    public async Task RequeueAsync(string? channel = null, CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoSettlementDelegate(_requeueFunc, "Requeue");
        await _requeueFunc!(MessageId, channel, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Extends the visibility timeout for this message.
    /// This is NOT a terminal settlement — you can still call
    /// <see cref="AckAsync"/> or <see cref="RejectAsync"/> after extending.
    /// </summary>
    /// <param name="additionalSeconds">Additional seconds to extend the timeout.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous extend operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message has already been settled or was received via PollQueueAsync.</exception>
    public async Task ExtendVisibilityAsync(int additionalSeconds, CancellationToken cancellationToken = default)
    {
        ThrowIfAlreadySettled();
        ThrowIfNoSettlementDelegate(_extendFunc, "ExtendVisibility");
        await _extendFunc!(MessageId, additionalSeconds, cancellationToken).ConfigureAwait(false);
    }

    private static void ThrowIfNoSettlementDelegate(object? del, string operation)
    {
        if (del is null)
        {
            throw new InvalidOperationException(
                $"{operation} is not available. Use ReceiveQueueDownstreamAsync for manual settlement, or set AutoAck=true when using PollQueueAsync.");
        }
    }

    private void ThrowIfSettled()
    {
        if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                $"Message '{MessageId}' has already been settled (acked, rejected, or requeued).");
        }
    }

    private void ThrowIfAlreadySettled()
    {
        if (Volatile.Read(ref _settled) != 0)
        {
            throw new InvalidOperationException(
                $"Message '{MessageId}' has already been settled (acked, rejected, or requeued).");
        }
    }
}
