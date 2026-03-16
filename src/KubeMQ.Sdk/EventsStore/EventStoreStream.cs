using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "EventStoreStream is an intentional domain name.")]
public sealed class EventStoreStream : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>>>? _reconnectFactory;
    private readonly Func<CancellationToken, Task>? _waitForReady;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<EventSendResult>> _pending = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> _call;
    private Task _receiveTask;
    private bool _disposed;

    internal EventStoreStream(
        AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> call,
        Func<CancellationToken, Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>>>? reconnectFactory = null,
        Func<CancellationToken, Task>? waitForReady = null)
    {
        _call = call;
        _reconnectFactory = reconnectFactory;
        _waitForReady = waitForReady;
        _receiveTask = Task.Run(ReceiveLoopAsync);
    }

    /// <summary>Sends an event to store on the stream and awaits confirmation.</summary>
    /// <param name="message">The event store message to send.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The send result confirming persistence.</returns>
    public async Task<EventSendResult> SendAsync(EventStoreMessage message, string clientId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        var eventId = message.EventId ?? Guid.NewGuid().ToString("N");

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

        var tcs = new TaskCompletionSource<EventSendResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[eventId] = tcs;

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _call.RequestStream.WriteAsync(grpcEvent, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(eventId, out _);
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
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _receiveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        foreach (var kvp in _pending)
        {
            kvp.Value.TrySetCanceled();
        }

        _pending.Clear();

        _call.Dispose();
        _cts.Dispose();
        _writeLock.Dispose();
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
                    if (_pending.TryRemove(result.EventID, out var tcs))
                    {
                        tcs.TrySetResult(new EventSendResult
                        {
                            EventId = result.EventID,
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
                foreach (var kvp in _pending)
                {
                    kvp.Value.TrySetException(
                        new KubeMQStreamBrokenException("Event store stream disconnected. Resend after reconnect.", ex));
                }

                _pending.Clear();

                if (_reconnectFactory == null || _waitForReady == null)
                {
                    return;
                }

                try
                {
                    await _waitForReady(_cts.Token).ConfigureAwait(false);
                    var newCall = await _reconnectFactory(_cts.Token).ConfigureAwait(false);
                    await _writeLock.WaitAsync(_cts.Token).ConfigureAwait(false);
                    try
                    {
                        _call.Dispose();
                        _call = newCall;
                    }
                    finally
                    {
                        _writeLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception reconnectEx)
                {
                    foreach (var kvp in _pending)
                    {
                        kvp.Value.TrySetException(reconnectEx);
                    }

                    _pending.Clear();
                    return;
                }
            }
        }
    }
}
