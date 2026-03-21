using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// A queue message received from a poll operation. Supports ack, nack, and requeue.
/// This is a class (not record) because it holds a reference to the transport
/// for settlement operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> Properties are read-only and safe to access from any thread.
/// Settle methods (<see cref="AckAsync"/>, <see cref="NackAsync"/>,
/// <see cref="ReQueueAsync"/>) are thread-safe but enforce exactly-once semantics —
/// only the first settle call takes effect; subsequent calls throw
/// <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="true"/>
public class QueueMessageReceived
{
    private readonly Func<long, CancellationToken, Task>? _ackFunc;
    private readonly Func<long, CancellationToken, Task>? _nackFunc;
    private readonly Func<long, string?, CancellationToken, Task>? _requeueFunc;
    private int _settled;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueMessageReceived"/> class.
    /// Internal — only the SDK transport layer constructs these when polling queue messages.
    /// </summary>
    internal QueueMessageReceived(
        string channel,
        string messageId,
        ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string>? tags,
        string? clientId,
        string? metadata,
        int receiveCount,
        DateTimeOffset timestamp,
        Func<long, CancellationToken, Task>? ackFunc,
        Func<long, CancellationToken, Task>? nackFunc,
        Func<long, string?, CancellationToken, Task>? requeueFunc)
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
        _nackFunc = nackFunc;
        _requeueFunc = requeueFunc;
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
        await _ackFunc!(Sequence, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects (NACKs) the message, signaling processing failure.
    /// The server may redeliver based on queue configuration.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous nack operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message has already been settled or was received with AutoAck.</exception>
    public async Task NackAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoSettlementDelegate(_nackFunc, "Nack");
        await _nackFunc!(Sequence, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Requeues the message to the same or a different channel.
    /// </summary>
    /// <param name="channel">Optional target channel. Null means requeue to the original channel.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous requeue operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message has already been settled or was received with AutoAck.</exception>
    public async Task ReQueueAsync(string? channel = null, CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoSettlementDelegate(_requeueFunc, "ReQueue");
        await _requeueFunc!(Sequence, channel, cancellationToken).ConfigureAwait(false);
    }

    private static void ThrowIfNoSettlementDelegate(object? del, string operation)
    {
        if (del is null)
        {
            throw new InvalidOperationException(
                $"{operation} is not available. Use QueueDownstreamReceiver.PollAsync "
                + "with AutoAck=false for manual settlement.");
        }
    }

    private void ThrowIfSettled()
    {
        if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                $"Message '{MessageId}' has already been settled (acked, nacked, or requeued).");
        }
    }
}
