using Grpc.Core;

namespace KubeMQ.Sdk.Tests.Unit.Helpers;

/// <summary>
/// Helper to create a mock <see cref="AsyncDuplexStreamingCall{TRequest,TResponse}"/>
/// for QueuesUpstream, usable in unit tests.
/// </summary>
internal static class MockUpstreamStream
{
    /// <summary>
    /// Creates a mock duplex streaming call that returns a single response when
    /// the request stream is completed.
    /// </summary>
    internal static AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse> Create(
        KubeMQ.Grpc.QueuesUpstreamResponse response)
    {
        var reader = new SingleResponseStreamReader(response);
        var writer = new SimpleUpstreamWriter(reader);

        return new AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });
    }

    /// <summary>
    /// Creates a mock duplex streaming call that captures written requests and returns
    /// a single response.
    /// </summary>
    internal static AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse> CreateCapturing(
        KubeMQ.Grpc.QueuesUpstreamResponse response,
        Action<KubeMQ.Grpc.QueuesUpstreamRequest> onWrite)
    {
        var reader = new SingleResponseStreamReader(response);
        var writer = new CapturingUpstreamWriter(reader, onWrite);

        return new AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesUpstreamRequest, KubeMQ.Grpc.QueuesUpstreamResponse>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });
    }

    private sealed class SingleResponseStreamReader : IAsyncStreamReader<KubeMQ.Grpc.QueuesUpstreamResponse>
    {
        private readonly KubeMQ.Grpc.QueuesUpstreamResponse _response;
        private readonly SemaphoreSlim _signal = new(0);
        private bool _delivered;

        public SingleResponseStreamReader(KubeMQ.Grpc.QueuesUpstreamResponse response)
        {
            _response = response;
        }

        public KubeMQ.Grpc.QueuesUpstreamResponse Current { get; private set; } = default!;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_delivered)
            {
                return false;
            }

            await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            Current = _response;
            _delivered = true;
            return true;
        }

        internal void Signal() => _signal.Release();
    }

    private sealed class SimpleUpstreamWriter : IClientStreamWriter<KubeMQ.Grpc.QueuesUpstreamRequest>
    {
        private readonly SingleResponseStreamReader _reader;

        public SimpleUpstreamWriter(SingleResponseStreamReader reader)
        {
            _reader = reader;
        }

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message)
        {
            return Task.CompletedTask;
        }

        public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            _reader.Signal();
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingUpstreamWriter : IClientStreamWriter<KubeMQ.Grpc.QueuesUpstreamRequest>
    {
        private readonly SingleResponseStreamReader _reader;
        private readonly Action<KubeMQ.Grpc.QueuesUpstreamRequest> _onWrite;

        public CapturingUpstreamWriter(
            SingleResponseStreamReader reader,
            Action<KubeMQ.Grpc.QueuesUpstreamRequest> onWrite)
        {
            _reader = reader;
            _onWrite = onWrite;
        }

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message)
        {
            _onWrite(message);
            return Task.CompletedTask;
        }

        public Task WriteAsync(KubeMQ.Grpc.QueuesUpstreamRequest message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _onWrite(message);
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            _reader.Signal();
            return Task.CompletedTask;
        }
    }
}
