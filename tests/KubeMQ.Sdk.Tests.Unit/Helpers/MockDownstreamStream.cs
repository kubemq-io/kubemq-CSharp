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
    /// </summary>
    internal static (
        AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse> Call,
        ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest> CapturedRequests
    ) Create(IEnumerable<KubeMQ.Grpc.QueuesDownstreamResponse> responses)
    {
        var capturedRequests = new ConcurrentQueue<KubeMQ.Grpc.QueuesDownstreamRequest>();
        var requestWriter = new FakeClientStreamWriter<KubeMQ.Grpc.QueuesDownstreamRequest>(capturedRequests);
        var responseReader = new FakeAsyncStreamReader<KubeMQ.Grpc.QueuesDownstreamResponse>(responses);

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse>(
            requestStream: requestWriter,
            responseStream: responseReader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });

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

    private sealed class FakeAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly Queue<T> _items;

        public FakeAsyncStreamReader(IEnumerable<T> items)
        {
            _items = new Queue<T>(items);
        }

        public T Current { get; private set; } = default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_items.Count > 0)
            {
                Current = _items.Dequeue();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
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
