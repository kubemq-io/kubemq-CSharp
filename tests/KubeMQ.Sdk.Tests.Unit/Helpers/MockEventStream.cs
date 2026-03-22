using System.Collections.Concurrent;
using Grpc.Core;

namespace KubeMQ.Sdk.Tests.Unit.Helpers;

/// <summary>
/// Helper to create a mock <see cref="AsyncDuplexStreamingCall{TRequest,TResponse}"/>
/// for EventStream and EventStoreStream, usable in unit tests.
/// </summary>
internal static class MockEventStream
{
    /// <summary>
    /// Creates a normal mock stream where:
    /// - WriteAsync captures events to a ConcurrentQueue
    /// - ResponseStream delivers results from a provided queue
    /// - CompleteAsync signals the reader to stop
    /// </summary>
    internal static (
        AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> Call,
        ConcurrentQueue<KubeMQ.Grpc.Event> CapturedEvents,
        Action<KubeMQ.Grpc.Result> EnqueueResult,
        Action CompleteReader
    ) Create()
    {
        var capturedEvents = new ConcurrentQueue<KubeMQ.Grpc.Event>();
        var reader = new SemaphoreAsyncStreamReader();
        var writer = new CapturingClientStreamWriter(capturedEvents, reader);

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { reader.Complete(); });

        return (call, capturedEvents, reader.EnqueueResult, reader.Complete);
    }

    /// <summary>
    /// Creates a normal mock stream with an auto-respond function:
    /// whenever WriteAsync is called, the autoRespond function is invoked with
    /// the written event to produce a matching Result that is enqueued to the reader.
    /// </summary>
    internal static (
        AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> Call,
        ConcurrentQueue<KubeMQ.Grpc.Event> CapturedEvents,
        Action CompleteReader
    ) CreateAutoRespond(Func<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> autoRespond)
    {
        var capturedEvents = new ConcurrentQueue<KubeMQ.Grpc.Event>();
        var reader = new SemaphoreAsyncStreamReader();
        var writer = new CapturingClientStreamWriter(capturedEvents, reader, autoRespond);

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { reader.Complete(); });

        return (call, capturedEvents, reader.Complete);
    }

    /// <summary>
    /// Creates a faulted stream that throws on MoveNext.
    /// </summary>
    internal static AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> CreateFaulted(
        Exception exception)
    {
        var capturedEvents = new ConcurrentQueue<KubeMQ.Grpc.Event>();
        var writer = new SimpleClientStreamWriter(capturedEvents);
        var reader = new FaultingAsyncStreamReader(exception);

        return new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });
    }

    /// <summary>
    /// Creates a stream where WriteAsync throws (for writer error testing).
    /// </summary>
    internal static (
        AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> Call,
        Action CompleteReader
    ) CreateWriterFaulted(Exception writeException)
    {
        var reader = new SemaphoreAsyncStreamReader();
        var writer = new FaultingClientStreamWriter(writeException);

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { reader.Complete(); });

        return (call, reader.Complete);
    }

    /// <summary>
    /// A response reader that blocks on MoveNext until results are enqueued via semaphore.
    /// </summary>
    private sealed class SemaphoreAsyncStreamReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        private readonly ConcurrentQueue<KubeMQ.Grpc.Result> _results = new();
        private readonly SemaphoreSlim _signal = new(0);
        private volatile bool _completed;

        public KubeMQ.Grpc.Result Current { get; private set; } = default!;

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

                if (_results.TryDequeue(out var result))
                {
                    Current = result;
                    return true;
                }
            }

            return false;
        }

        internal void EnqueueResult(KubeMQ.Grpc.Result result)
        {
            _results.Enqueue(result);
            _signal.Release();
        }

        internal void Complete()
        {
            _completed = true;
            _signal.Release();
        }
    }

    /// <summary>
    /// A request writer that captures events and optionally signals the response reader.
    /// </summary>
    private sealed class CapturingClientStreamWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        private readonly ConcurrentQueue<KubeMQ.Grpc.Event> _captured;
        private readonly SemaphoreAsyncStreamReader _responseReader;
        private readonly Func<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>? _autoRespond;

        public CapturingClientStreamWriter(
            ConcurrentQueue<KubeMQ.Grpc.Event> captured,
            SemaphoreAsyncStreamReader responseReader,
            Func<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>? autoRespond = null)
        {
            _captured = captured;
            _responseReader = responseReader;
            _autoRespond = autoRespond;
        }

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.Event message)
        {
            _captured.Enqueue(message);
            if (_autoRespond != null)
            {
                _responseReader.EnqueueResult(_autoRespond(message));
            }

            return Task.CompletedTask;
        }

        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return WriteAsync(message);
        }

        public Task CompleteAsync()
        {
            _responseReader.Complete();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A simple writer that captures events without signaling a reader.
    /// </summary>
    private sealed class SimpleClientStreamWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        private readonly ConcurrentQueue<KubeMQ.Grpc.Event> _captured;

        public SimpleClientStreamWriter(ConcurrentQueue<KubeMQ.Grpc.Event> captured)
        {
            _captured = captured;
        }

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.Event message)
        {
            _captured.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken)
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

    /// <summary>
    /// A writer that throws an exception on WriteAsync.
    /// </summary>
    private sealed class FaultingClientStreamWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        private readonly Exception _exception;

        public FaultingClientStreamWriter(Exception exception)
        {
            _exception = exception;
        }

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.Event message) => throw _exception;

        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken) => throw _exception;

        public Task CompleteAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Creates a stream where the reader delays, then throws on second MoveNext.
    /// First MoveNext blocks until signaled, allowing time to set up pending messages.
    /// </summary>
    internal static (
        AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> Call,
        Action TriggerFault
    ) CreateDelayedFault(Exception exception)
    {
        var capturedEvents = new ConcurrentQueue<KubeMQ.Grpc.Event>();
        var writer = new SimpleClientStreamWriter(capturedEvents);
        var reader = new DelayedFaultingAsyncStreamReader(exception);

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });

        return (call, reader.TriggerFault);
    }

    /// <summary>
    /// A reader that throws the given exception on MoveNext.
    /// </summary>
    private sealed class FaultingAsyncStreamReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        private readonly Exception _exception;

        public FaultingAsyncStreamReader(Exception exception)
        {
            _exception = exception;
        }

        public KubeMQ.Grpc.Result Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) => throw _exception;
    }

    /// <summary>
    /// A reader that blocks on first MoveNext until signaled, then throws.
    /// </summary>
    private sealed class DelayedFaultingAsyncStreamReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        private readonly Exception _exception;
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public DelayedFaultingAsyncStreamReader(Exception exception)
        {
            _exception = exception;
        }

        public KubeMQ.Grpc.Result Current => default!;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            throw _exception;
        }

        internal void TriggerFault() => _gate.TrySetResult();
    }
}
