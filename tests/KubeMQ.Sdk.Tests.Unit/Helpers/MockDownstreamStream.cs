using System.Collections.Concurrent;
using Grpc.Core;

namespace KubeMQ.Sdk.Tests.Unit.Helpers;

/// <summary>
/// Helper to create a mock <see cref="AsyncDuplexStreamingCall{TRequest,TResponse}"/>
/// for QueuesDownstream, usable in unit tests.
/// </summary>
internal static class MockDownstreamStream
{
    /// <summary>
    /// Creates a mock duplex streaming call that returns a single response and captures requests.
    /// The response stream is reactive: it blocks until a request is written, then delivers
    /// the next queued response with RefRequestId and RequestTypeData set to match.
    /// </summary>
    internal static (
        AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse> Call,
        ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest> CapturedRequests
    ) Create(KubeMQ.Grpc.QueuesDownstreamResponse response)
    {
        return Create(new[] { response });
    }

    /// <summary>
    /// Creates a mock duplex streaming call that returns the given responses in order.
    /// The response stream is reactive: each response is delivered only after a request
    /// is written, with RefRequestId set to the request's RequestID.
    /// </summary>
    internal static (
        AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse> Call,
        ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest> CapturedRequests
    ) Create(IEnumerable<KubeMQ.Grpc.QueuesDownstreamResponse> responses)
    {
        var capturedRequests = new ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest>();
        var responseQueue = new Queue<KubeMQ.Grpc.QueuesDownstreamResponse>(responses);
        var responseReader = new ReactiveAsyncStreamReader(responseQueue);
        var requestWriter = new ReactiveClientStreamWriter(capturedRequests, responseReader);

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse>(
            requestStream: requestWriter,
            responseStream: responseReader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { responseReader.Complete(); });

        return (call, capturedRequests);
    }

    /// <summary>
    /// Creates a mock duplex streaming call that throws the given exception on MoveNext.
    /// </summary>
    internal static AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse> CreateFaulted(
        Exception exception)
    {
        var capturedRequests = new ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest>();
        var requestWriter = new FakeClientStreamWriter<KubeMQ.Grpc.QueuesDownstreamRequest>(capturedRequests);
        var responseReader = new FaultingAsyncStreamReader<KubeMQ.Grpc.QueuesDownstreamResponse>(exception);

        return new AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse>(
            requestStream: requestWriter,
            responseStream: responseReader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });
    }

    /// <summary>
    /// A reactive response reader that blocks on MoveNext until a request triggers
    /// delivery of the next queued response. This mimics real gRPC server behavior
    /// where the server responds after receiving a request.
    /// </summary>
    internal sealed class ReactiveAsyncStreamReader : IAsyncStreamReader<KubeMQ.Grpc.QueuesDownstreamResponse>
    {
        private readonly Queue<KubeMQ.Grpc.QueuesDownstreamResponse> _pendingResponses;
        private readonly SemaphoreSlim _signal = new(0);
        private readonly ConcurrentQueue<string> _requestIds = new();
        private volatile bool _completed;

        public ReactiveAsyncStreamReader(Queue<KubeMQ.Grpc.QueuesDownstreamResponse> responses)
        {
            _pendingResponses = responses;
        }

        public KubeMQ.Grpc.QueuesDownstreamResponse Current { get; private set; } = default!;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            while (!_completed)
            {
                try
                {
                    await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                if (_completed)
                {
                    return false;
                }

                lock (_pendingResponses)
                {
                    if (_pendingResponses.Count > 0)
                    {
                        var response = _pendingResponses.Dequeue();
                        if (_requestIds.TryDequeue(out var requestId))
                        {
                            response.RefRequestId = requestId;
                            if (response.RequestTypeData == 0)
                            {
                                response.RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.Get;
                            }
                        }

                        Current = response;
                        return true;
                    }
                }
            }

            return false;
        }

        internal void EnqueueRequestAndSignal(string requestId)
        {
            _requestIds.Enqueue(requestId);
            _signal.Release();
        }

        internal void Complete()
        {
            _completed = true;
            _signal.Release();
        }
    }

    /// <summary>
    /// A request writer that captures requests and signals the reactive response reader
    /// to deliver the next response.
    /// </summary>
    private sealed class ReactiveClientStreamWriter : IClientStreamWriter<KubeMQ.Grpc.QueuesDownstreamRequest>
    {
        private readonly ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest> _captured;
        private readonly ReactiveAsyncStreamReader _responseReader;

        public ReactiveClientStreamWriter(
            ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest> captured,
            ReactiveAsyncStreamReader responseReader)
        {
            _captured = captured;
            _responseReader = responseReader;
        }

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.QueuesDownstreamRequest message)
        {
            _captured.Enqueue(message);
            _responseReader.EnqueueRequestAndSignal(message.RequestID);
            return Task.CompletedTask;
        }

        public Task WriteAsync(KubeMQ.Grpc.QueuesDownstreamRequest message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _captured.Enqueue(message);
            _responseReader.EnqueueRequestAndSignal(message.RequestID);
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            _responseReader.Complete();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClientStreamWriter<T> : IClientStreamWriter<T>
    {
        private readonly ConcurrentQueue<T> _captured;

        public FakeClientStreamWriter(ConcurrentQueue<T> captured)
        {
            _captured = captured;
        }

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message)
        {
            _captured.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task WriteAsync(T message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _captured.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FaultingAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly Exception _exception;

        public FaultingAsyncStreamReader(Exception exception)
        {
            _exception = exception;
        }

        public T Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }
}
