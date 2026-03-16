using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Grpc.Core;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Events;

/// <summary>
/// Wraps a bidirectional gRPC stream for high-throughput event publishing.
/// For non-store events, the server only sends Result on error.
/// Automatically reconnects if the stream breaks.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "EventStream is an intentional domain name.")]
public sealed class EventStream : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>>>? _reconnectFactory;
    private readonly Func<CancellationToken, Task>? _waitForReady;
    private readonly Action<Exception>? _onError;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> _call;
    private Task _receiveTask;
    private bool _disposed;

    internal EventStream(
        AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> call,
        Action<Exception>? onError = null,
        Func<CancellationToken, Task<AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>>>? reconnectFactory = null,
        Func<CancellationToken, Task>? waitForReady = null)
    {
        _call = call;
        _onError = onError;
        _reconnectFactory = reconnectFactory;
        _waitForReady = waitForReady;
        _receiveTask = Task.Run(ReceiveLoopAsync);
    }

    /// <summary>Sends an event on the stream (non-blocking for non-store events).</summary>
    /// <param name="message">The event message to send.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task SendAsync(EventMessage message, string clientId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        var grpcEvent = new KubeMQ.Grpc.Event
        {
            EventID = message.EventId ?? Guid.NewGuid().ToString("N"),
            Channel = message.Channel,
            Body = ByteString.CopyFrom(message.Body.Span),
            ClientID = message.ClientId ?? clientId,
            Store = false,
        };

        if (message.Tags != null)
        {
            foreach (var kvp in message.Tags)
            {
                grpcEvent.Tags.Add(kvp.Key, kvp.Value);
            }
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _call.RequestStream.WriteAsync(grpcEvent, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
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
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        _onError?.Invoke(new KubeMQOperationException(result.Error));
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
                _onError?.Invoke(ex);

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
                    _onError?.Invoke(reconnectEx);
                    return;
                }
            }
        }
    }
}
