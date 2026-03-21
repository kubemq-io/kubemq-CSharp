using System.Threading.Channels;
using Grpc.Core;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Logging;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Queues;

internal sealed class DownstreamStreamHandle : IAsyncDisposable
{
    private readonly AsyncDuplexStreamingCall<
        KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse> _call;

    private readonly string _clientId;

    private readonly Channel<KubeMQ.Grpc.QueuesDownstreamRequest> _writeChannel =
        Channel.CreateBounded<KubeMQ.Grpc.QueuesDownstreamRequest>(
            new BoundedChannelOptions(512)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
            });

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readerTask;
    private readonly Task _writerTask;
    private readonly Action<string, string>? _onError;
    private readonly Action? _onTerminated;
    private readonly ILogger _logger;
    private readonly object _pollLock = new();

    private TaskCompletionSource<KubeMQ.Grpc.QueuesDownstreamResponse>? _pendingPoll;
    private string? _expectedPollRequestId;
    private volatile bool _disposed;
    private volatile bool _streamBroken;

    internal DownstreamStreamHandle(
        AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse> call,
        string clientId,
        ILogger logger,
        Action<string, string>? onError = null,
        Action? onTerminated = null)
    {
        _call = call;
        _clientId = clientId;
        _logger = logger;
        _onError = onError;
        _onTerminated = onTerminated;
        _readerTask = Task.Run(ReaderLoopAsync);
        _writerTask = Task.Run(WriterLoopAsync);
    }

    internal bool IsStreamBroken => _streamBroken;

    internal bool IsDisposed => _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _cts.CancelAsync().ConfigureAwait(false);
        _writeChannel.Writer.TryComplete();

        try
        {
            await _writerTask.ConfigureAwait(false);
        }
        catch
        {
            // Expected — writer may throw on cancellation.
        }

        try
        {
            await _readerTask.ConfigureAwait(false);
        }
        catch
        {
            // Expected — reader may throw on cancellation.
        }

        _call.Dispose();
        _cts.Dispose();
    }

    internal async Task WriteAsync(
        KubeMQ.Grpc.QueuesDownstreamRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_streamBroken)
        {
            throw new KubeMQOperationException(
                "Downstream stream is broken. The server has NACKed all unsettled messages.");
        }

        await _writeChannel.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<KubeMQ.Grpc.QueuesDownstreamResponse> PollAsync(
        KubeMQ.Grpc.QueuesDownstreamRequest getRequest,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<KubeMQ.Grpc.QueuesDownstreamResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pollLock)
        {
            if (_pendingPoll != null)
            {
                throw new InvalidOperationException(
                    "A poll is already in progress. Wait for the previous poll to complete.");
            }

            _pendingPoll = tcs;
            _expectedPollRequestId = getRequest.RequestID;
        }

        try
        {
            await using (cancellationToken.Register(() =>
                tcs.TrySetCanceled(cancellationToken)).ConfigureAwait(false))
            {
                await _writeChannel.Writer.WriteAsync(getRequest, cancellationToken)
                    .ConfigureAwait(false);
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_pollLock)
            {
                _pendingPoll = null;
                _expectedPollRequestId = null;
            }
        }
    }

    internal async Task SendCloseAsync(string? lastTransactionId = null)
    {
        if (_disposed || _streamBroken)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(lastTransactionId))
            {
                var closeRequest = new KubeMQ.Grpc.QueuesDownstreamRequest
                {
                    RequestID = Guid.NewGuid().ToString("N"),
                    ClientID = _clientId,
                    RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.CloseByClient,
                    RefTransactionId = lastTransactionId,
                };

                await _writeChannel.Writer.WriteAsync(closeRequest).ConfigureAwait(false);
            }

            _writeChannel.Writer.TryComplete();

            // Wait for the writer to drain all buffered requests.
            await _writerTask.ConfigureAwait(false);

            await _call.RequestStream.CompleteAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort close — stream may already be broken.
        }
    }

    private async Task WriterLoopAsync()
    {
        var reader = _writeChannel.Reader;

        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var request))
                {
                    await _call.RequestStream.WriteAsync(request, _cts.Token)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose.
        }
        catch (Exception ex)
        {
            _streamBroken = true;
            Log.DownstreamReaderFailed(_logger, ex);
        }
    }

    private async Task ReaderLoopAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested &&
                   await _call.ResponseStream.MoveNext(_cts.Token).ConfigureAwait(false))
            {
                var response = _call.ResponseStream.Current;

                if (response.RequestTypeData ==
                    KubeMQ.Grpc.QueuesDownstreamRequestType.CloseByServer)
                {
                    _streamBroken = true;
                    Log.DownstreamCloseByServer(_logger, response.RefRequestId);
                    FailPendingPoll(
                        new KubeMQOperationException(
                            "Server closed the downstream stream."));
                    return;
                }

                if (response.RequestTypeData == KubeMQ.Grpc.QueuesDownstreamRequestType.Get)
                {
                    CompletePendingPoll(response);
                    continue;
                }

                if (response.IsError)
                {
                    try
                    {
                        _onError?.Invoke(response.RefRequestId, response.Error);
                    }
                    catch
                    {
                        // _onError must not kill the reader.
                    }

                    Log.DownstreamSettlementError(
                        _logger, response.RefRequestId, response.Error);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose.
        }
        catch (Exception ex)
        {
            _streamBroken = true;
            Log.DownstreamReaderFailed(_logger, ex);
            FailPendingPoll(ex);
        }
        finally
        {
            _streamBroken = true;
            FailPendingPoll(new KubeMQOperationException("Downstream stream closed."));

            try
            {
                _onTerminated?.Invoke();
            }
            catch
            {
                // _onTerminated must not prevent cleanup.
            }
        }
    }

    private void CompletePendingPoll(KubeMQ.Grpc.QueuesDownstreamResponse response)
    {
        lock (_pollLock)
        {
            if (_pendingPoll != null &&
                response.RefRequestId == _expectedPollRequestId)
            {
                _pendingPoll.TrySetResult(response);
            }
        }
    }

    private void FailPendingPoll(Exception ex)
    {
        lock (_pollLock)
        {
            _pendingPoll?.TrySetException(ex);
        }
    }
}
