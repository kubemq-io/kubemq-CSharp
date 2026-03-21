using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.EventsStore;

/// <summary>
/// Wraps a bidirectional gRPC stream for high-throughput event store publishing.
/// For store events, the server sends a Result for every event confirming persistence.
/// Automatically reconnects if the stream breaks.
/// </summary>
/// <remarks>
/// Uses a bounded <see cref="Channel{T}"/> for write pipelining:
/// multiple concurrent <see cref="SendAsync"/> calls enqueue without lock contention,
/// and a single background writer drains them onto the gRPC stream.
/// Response matching uses <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by a monotonic long sequence.
/// </remarks>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "EventStoreStream is an intentional domain name.")]
public sealed class EventStoreStream : IAsyncDisposable
{
    private const int MaxPendingMessages = 8192;
    private readonly SemaphoreSlim _pendingGate = new(MaxPendingMessages, MaxPendingMessages);
    private readonly Func<CancellationToken, Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>>>? _reconnectFactory;
    private readonly Func<CancellationToken, Task>? _waitForReady;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<EventStoreResult>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<KubeMQ.Grpc.Event> _writeChannel;
    private readonly Task _writerTask;
    private long _nextSeq;
    private AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> _call;
    private Task _receiveTask;
    private bool _disposed;
    private volatile bool _streamBroken;

    internal EventStoreStream(
        AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> call,
        Func<CancellationToken, Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>>>? reconnectFactory = null,
        Func<CancellationToken, Task>? waitForReady = null)
    {
        _call = call;
        _reconnectFactory = reconnectFactory;
        _waitForReady = waitForReady;
        _writeChannel = Channel.CreateBounded<KubeMQ.Grpc.Event>(
            new BoundedChannelOptions(4096)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        _writerTask = Task.Run(WriterLoopAsync);
        _receiveTask = Task.Run(ReceiveLoopAsync);
    }

    /// <summary>Gets the number of messages awaiting server acknowledgment.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Sends an event to store on the stream and awaits confirmation.</summary>
    /// <param name="message">The event store message to send.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The send result confirming persistence.</returns>
    public async Task<EventStoreResult> SendAsync(EventStoreMessage message, string clientId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        if (_streamBroken)
        {
            throw new KubeMQOperationException("Event store stream is broken. Reconnecting or disposed.");
        }

        long seqId = Interlocked.Increment(ref _nextSeq);
        string eventId = seqId.ToString(CultureInfo.InvariantCulture);

        var grpcEvent = new KubeMQ.Grpc.Event
        {
            EventID = eventId,
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? clientId,
            Store = true,
        };

        if (message.Tags != null)
        {
            foreach (var kvp in message.Tags)
            {
                grpcEvent.Tags.Add(kvp.Key, kvp.Value);
            }
        }

        await _pendingGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var tcs = new TaskCompletionSource<EventStoreResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[seqId] = tcs;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Enqueue to the write channel — the writer loop will drain it
            await _writeChannel.Writer.WriteAsync(grpcEvent, cancellationToken).ConfigureAwait(false);

            return await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            if (_pending.TryRemove(seqId, out _))
            {
                _pendingGate.Release();
            }

            throw;
        }
    }

    /// <summary>Closes the stream gracefully.</summary>
    /// <returns>A task representing the asynchronous close operation.</returns>
    public async Task CloseAsync()
    {
        if (_disposed)
        {
            return;
        }

        _writeChannel.Writer.TryComplete();

        try
        {
            await _writerTask.ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }

        try
        {
            await _call.RequestStream.CompleteAsync().ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _receiveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected
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
        _writeChannel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _writerTask.ConfigureAwait(false);
        }
        catch
        {
            // expected
        }

        try
        {
            await _receiveTask.ConfigureAwait(false);
        }
        catch
        {
            // expected
        }

        int disposeCount = _pending.Count;
        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetCanceled();
        }

        _pending.Clear();
        try
        {
            if (disposeCount > 0)
            {
                _pendingGate.Release(disposeCount);
            }
        }
        catch (SemaphoreFullException)
        {
        }

        _call.Dispose();
        _cts.Dispose();
        _pendingGate.Dispose();
    }

    private async Task WriterLoopAsync()
    {
        var reader = _writeChannel.Reader;
        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var grpcEvent))
                {
                    await _call.RequestStream.WriteAsync(grpcEvent, _cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on dispose
        }
        catch (Exception ex)
        {
            _streamBroken = true;

            // Drain remaining channel items and fail their pending TCS entries.
            // TrySetException is idempotent (first-wins) — safe to race with ReceiveLoopAsync.
            while (_writeChannel.Reader.TryRead(out var orphanedEvent))
            {
                if (long.TryParse(orphanedEvent.EventID, out long orphanKey)
                    && _pending.TryRemove(orphanKey, out var orphanedTcs))
                {
                    orphanedTcs.TrySetException(
                        new KubeMQStreamBrokenException("Event store stream writer failed.", ex));
                    _pendingGate.Release();
                }
            }

            int remainingCount = _pending.Count;
            foreach (var kvp in _pending)
            {
                kvp.Value.TrySetException(
                    new KubeMQStreamBrokenException("Event store stream writer failed.", ex));
            }

            _pending.Clear();
            try
            {
                if (remainingCount > 0)
                {
                    _pendingGate.Release(remainingCount);
                }
            }
            catch (SemaphoreFullException)
            {
            }
        }
    }

    private async Task ReceiveLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested &&
                       await _call.ResponseStream.MoveNext(_cts.Token).ConfigureAwait(false))
                {
                    var result = _call.ResponseStream.Current;
                    if (long.TryParse(result.EventID, out long key)
                        && _pending.TryRemove(key, out var tcs))
                    {
                        _pendingGate.Release();
                        tcs.TrySetResult(new EventStoreResult
                        {
                            Id = result.EventID,
                            Sent = result.Sent,
                            Error = result.Error,
                        });
                    }
                }

                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _streamBroken = true;

                int disconnectCount = _pending.Count;
                foreach (var kvp in _pending)
                {
                    kvp.Value.TrySetException(
                        new KubeMQStreamBrokenException("Event store stream disconnected. Resend after reconnect.", ex));
                }

                _pending.Clear();
                try
                {
                    if (disconnectCount > 0)
                    {
                        _pendingGate.Release(disconnectCount);
                    }
                }
                catch (SemaphoreFullException)
                {
                }

                if (_reconnectFactory == null || _waitForReady == null)
                {
                    return;
                }

                try
                {
                    await _waitForReady(_cts.Token).ConfigureAwait(false);
                    var newCall = await _reconnectFactory(_cts.Token).ConfigureAwait(false);

                    var oldCall = _call;
                    _call = newCall;
                    _streamBroken = false;
                    oldCall.Dispose();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception reconnectEx)
                {
                    int reconnectFailCount = _pending.Count;
                    foreach (var kvp in _pending)
                    {
                        kvp.Value.TrySetException(reconnectEx);
                    }

                    _pending.Clear();
                    try
                    {
                        if (reconnectFailCount > 0)
                        {
                            _pendingGate.Release(reconnectFailCount);
                        }
                    }
                    catch (SemaphoreFullException)
                    {
                    }

                    return;
                }
            }
        }
    }
}
