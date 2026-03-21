using KubeMQ.Sdk.Internal.Queues;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// Represents a batch of messages received from a queue downstream poll operation.
/// Provides batch-level settlement methods (AckAll, NackAll, ReQueueAll).
/// </summary>
/// <remarks>
/// <para>
/// Batch-level and per-message settlement should not be mixed on the same batch.
/// If individual messages have already been settled via
/// <see cref="QueueMessageReceived.AckAsync"/> or similar, calling batch-level
/// settlement may produce contradictory settlements. The server will drop or
/// error on conflicting operations.
/// </para>
/// </remarks>
public sealed class QueueBatch
{
    private readonly DownstreamStreamHandle? _handle;
    private readonly string _clientId;
    private int _settled;

    internal QueueBatch(
        string transactionId,
        IReadOnlyList<QueueMessageReceived> messages,
        bool isError,
        string? error,
        DownstreamStreamHandle? handle,
        string clientId)
    {
        TransactionId = transactionId;
        Messages = messages;
        IsError = isError;
        Error = error;
        _handle = handle;
        _clientId = clientId;
    }

    /// <summary>Gets the server-assigned transaction ID for this batch.</summary>
    public string TransactionId { get; }

    /// <summary>Gets the messages in this batch.</summary>
    public IReadOnlyList<QueueMessageReceived> Messages { get; }

    /// <summary>Gets a value indicating whether the server reported an error.</summary>
    public bool IsError { get; }

    /// <summary>Gets the error message from the server, if any.</summary>
    public string? Error { get; }

    /// <summary>Gets a value indicating whether this batch contains any messages.</summary>
    public bool HasMessages => Messages.Count > 0;

    /// <summary>
    /// Acknowledges all messages in this batch.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous acknowledge operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the batch was already settled or received with AutoAck=true.</exception>
    public async Task AckAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoHandle();
        var request = new KubeMQ.Grpc.QueuesDownstreamRequest
        {
            RequestID = Guid.NewGuid().ToString("N"),
            ClientID = _clientId,
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.AckAll,
            RefTransactionId = TransactionId,
        };
        await _handle!.WriteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects (NACKs) all messages in this batch, causing redelivery.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous nack operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the batch was already settled or received with AutoAck=true.</exception>
    public async Task NackAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoHandle();
        var request = new KubeMQ.Grpc.QueuesDownstreamRequest
        {
            RequestID = Guid.NewGuid().ToString("N"),
            ClientID = _clientId,
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.NackAll,
            RefTransactionId = TransactionId,
        };
        await _handle!.WriteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Requeues all messages in this batch to a different channel.
    /// </summary>
    /// <param name="channel">Target channel for the requeued messages.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous requeue operation.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="channel"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the batch was already settled or received with AutoAck=true.</exception>
    public async Task ReQueueAllAsync(
        string channel,
        CancellationToken cancellationToken = default)
    {
        ThrowIfSettled();
        ThrowIfNoHandle();
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        var request = new KubeMQ.Grpc.QueuesDownstreamRequest
        {
            RequestID = Guid.NewGuid().ToString("N"),
            ClientID = _clientId,
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.ReQueueAll,
            RefTransactionId = TransactionId,
            ReQueueChannel = channel,
        };
        await _handle!.WriteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void ThrowIfSettled()
    {
        if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                $"Batch '{TransactionId}' has already been settled.");
        }
    }

    private void ThrowIfNoHandle()
    {
        if (_handle is null)
        {
            throw new InvalidOperationException(
                "Settlement is not available. This batch was received with AutoAck=true.");
        }
    }
}
