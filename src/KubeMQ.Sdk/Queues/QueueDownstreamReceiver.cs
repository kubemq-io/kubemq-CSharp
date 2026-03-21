using System.Diagnostics;
using KubeMQ.Sdk.Internal.Queues;
using KubeMQ.Sdk.Internal.Telemetry;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Queues;

/// <summary>
/// A persistent downstream receiver that owns a single gRPC stream. Create via
/// <see cref="Client.KubeMQClient.CreateQueueDownstreamReceiverAsync"/>.
/// </summary>
/// <remarks>
/// <para>Each receiver owns its own gRPC stream. Multiple receivers can be created
/// for different channels or processing patterns. Receivers are fully isolated —
/// a failure in one does not affect others.</para>
/// <para>Receivers do not auto-reconnect. When the stream breaks, create a new receiver.</para>
/// </remarks>
public sealed class QueueDownstreamReceiver : IAsyncDisposable
{
    private readonly DownstreamStreamHandle _handle;
    private readonly string _clientId;
    private readonly string _serverAddress;
    private readonly int _serverPort;
    private readonly ILogger _logger;
    private string? _lastTransactionId;
    private volatile bool _disposed;

    internal QueueDownstreamReceiver(
        DownstreamStreamHandle handle,
        string clientId,
        string serverAddress,
        int serverPort,
        ILogger logger)
    {
        _handle = handle;
        _clientId = clientId;
        _serverAddress = serverAddress;
        _serverPort = serverPort;
        _logger = logger;
    }

    /// <summary>
    /// Raised when the server reports a settlement error (e.g., transaction not found,
    /// requeue authorization failure).
    /// </summary>
    public event EventHandler<QueueDownstreamErrorEventArgs>? OnError;

    /// <summary>
    /// Polls the queue for messages. Only one poll can be active at a time.
    /// </summary>
    /// <param name="request">Poll configuration.</param>
    /// <param name="cancellationToken">Token to cancel the poll.</param>
    /// <returns>A <see cref="QueueBatch"/> containing the received messages.</returns>
    /// <exception cref="ObjectDisposedException">The receiver has been disposed.</exception>
    /// <exception cref="InvalidOperationException">A poll is already in progress.</exception>
    public async Task<QueueBatch> PollAsync(
        QueuePollRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var sw = Stopwatch.StartNew();
        using var activity = KubeMQActivitySource.StartClientActivity(
            request.Channel,
            _clientId,
            _serverAddress,
            _serverPort);
        QueueBatch? result = null;

        try
        {
            var getRequest = new KubeMQ.Grpc.QueuesDownstreamRequest
            {
                RequestID = Guid.NewGuid().ToString("N"),
                ClientID = _clientId,
                RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.Get,
                Channel = request.Channel,
                MaxItems = request.MaxMessages,
                WaitTimeout = request.WaitTimeoutSeconds * 1000,
                AutoAck = request.AutoAck,
            };

            var response = await _handle.PollAsync(getRequest, cancellationToken)
                .ConfigureAwait(false);

            var transactionId = response.TransactionId;
            _lastTransactionId = transactionId;

            var isManualAck = !request.AutoAck && !string.IsNullOrEmpty(transactionId);

            var messages = MapMessages(response, transactionId, isManualAck);

            result = new QueueBatch(
                transactionId,
                messages,
                response.IsError,
                string.IsNullOrEmpty(response.Error) ? null : response.Error,
                isManualAck ? _handle : null,
                _clientId);
            return result;
        }
        finally
        {
            sw.Stop();
            KubeMQMetrics.RecordOperationDuration(
                sw.Elapsed.TotalSeconds, "queue_downstream_poll", request.Channel);
            activity?.SetTag("queue.messages_count", result?.Messages.Count ?? 0);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _handle.SendCloseAsync(_lastTransactionId).ConfigureAwait(false);
        await _handle.DisposeAsync().ConfigureAwait(false);
    }

    internal void RaiseOnError(string transactionId, string error)
    {
        OnError?.Invoke(this, new QueueDownstreamErrorEventArgs
        {
            TransactionId = transactionId,
            Error = error,
        });
    }

    private static DateTimeOffset SafeFromUnixTimeSeconds(long ts)
    {
        // 253402300800 = seconds for year 9999-12-31T23:59:59Z
        if (ts > 0 && ts < 253402300800)
        {
            return DateTimeOffset.FromUnixTimeSeconds(ts);
        }

        return DateTimeOffset.UtcNow;
    }

    private List<QueueMessageReceived> MapMessages(
        KubeMQ.Grpc.QueuesDownstreamResponse response,
        string transactionId,
        bool isManualAck)
    {
        var messages = new List<QueueMessageReceived>(response.Messages.Count);
        foreach (var msg in response.Messages)
        {
            var sequence = msg.Attributes?.Sequence ?? 0;
            Dictionary<string, string>? tags = null;
            if (msg.Tags?.Count > 0)
            {
                tags = new Dictionary<string, string>(msg.Tags);
            }

            var received = new QueueMessageReceived(
                channel: msg.Channel,
                messageId: msg.MessageID,
                body: msg.Body.Memory,
                tags: tags,
                clientId: string.IsNullOrEmpty(msg.ClientID) ? null : msg.ClientID,
                metadata: string.IsNullOrEmpty(msg.Metadata) ? null : msg.Metadata,
                receiveCount: msg.Attributes?.ReceiveCount ?? 0,
                timestamp: SafeFromUnixTimeSeconds(msg.Attributes?.Timestamp ?? 0),
                ackFunc: isManualAck ? CreateAckDelegate(transactionId) : null,
                nackFunc: isManualAck ? CreateNackDelegate(transactionId) : null,
                requeueFunc: isManualAck ? CreateReQueueDelegate(transactionId) : null)
            {
                Sequence = (long)sequence,
                MD5OfBody = msg.Attributes?.MD5OfBody,
            };
            messages.Add(received);
        }

        return messages;
    }

    private Func<long, CancellationToken, Task> CreateAckDelegate(string transactionId)
    {
        return async (sequence, ct) =>
        {
            var request = new KubeMQ.Grpc.QueuesDownstreamRequest
            {
                RequestID = Guid.NewGuid().ToString("N"),
                ClientID = _clientId,
                RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.AckRange,
                RefTransactionId = transactionId,
            };
            request.SequenceRange.Add(sequence);
            await _handle.WriteAsync(request, ct).ConfigureAwait(false);
        };
    }

    private Func<long, CancellationToken, Task> CreateNackDelegate(string transactionId)
    {
        return async (sequence, ct) =>
        {
            var request = new KubeMQ.Grpc.QueuesDownstreamRequest
            {
                RequestID = Guid.NewGuid().ToString("N"),
                ClientID = _clientId,
                RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.NackRange,
                RefTransactionId = transactionId,
            };
            request.SequenceRange.Add(sequence);
            await _handle.WriteAsync(request, ct).ConfigureAwait(false);
        };
    }

    private Func<long, string?, CancellationToken, Task> CreateReQueueDelegate(
        string transactionId)
    {
        return async (sequence, channel, ct) =>
        {
            var request = new KubeMQ.Grpc.QueuesDownstreamRequest
            {
                RequestID = Guid.NewGuid().ToString("N"),
                ClientID = _clientId,
                RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.ReQueueRange,
                RefTransactionId = transactionId,
            };
            request.SequenceRange.Add(sequence);
            if (!string.IsNullOrEmpty(channel))
            {
                request.ReQueueChannel = channel;
            }

            await _handle.WriteAsync(request, ct).ConfigureAwait(false);
        };
    }
}
