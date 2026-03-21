using System.Collections.Concurrent;
using FluentAssertions;
using Grpc.Core;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Queues;
using KubeMQ.Sdk.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Internal;

public class DownstreamStreamHandleTests
{
    private const string ClientId = "test-client";

    private static KubeMQ.Grpc.QueuesDownstreamRequest MakeGetRequest(string? requestId = null) =>
        new()
        {
            RequestID = requestId ?? Guid.NewGuid().ToString("N"),
            ClientID = ClientId,
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.Get,
            Channel = "test-channel",
        };

    private static KubeMQ.Grpc.QueuesDownstreamResponse MakeGetResponse() =>
        new()
        {
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.Get,
        };

    private static AsyncDuplexStreamingCall<KubeMQ.Grpc.QueuesDownstreamRequest, KubeMQ.Grpc.QueuesDownstreamResponse>
        CreateCustomCall(
            IClientStreamWriter<KubeMQ.Grpc.QueuesDownstreamRequest> writer,
            IAsyncStreamReader<KubeMQ.Grpc.QueuesDownstreamResponse> reader,
            Action? disposeAction = null) =>
        new(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: disposeAction ?? (() => { }));

    // ──────────────────────────── WriteAsync ────────────────────────────

    [Fact]
    public async Task WriteAsync_SerializesConcurrentWrites()
    {
        var responses = Enumerable.Range(0, 5).Select(_ => MakeGetResponse()).ToArray();
        var (call, captured) = MockDownstreamStream.Create(responses);
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        var requests = Enumerable.Range(0, 5)
            .Select(i => MakeGetRequest($"req-{i}"))
            .ToArray();

        await Task.WhenAll(requests.Select(r => handle.WriteAsync(r)));

        // The writes go through Channel<T> asynchronously; give the writer loop time to process.
        await Task.Delay(200);

        captured.Should().HaveCount(5);
        captured.Select(r => r.RequestID).Should()
            .BeEquivalentTo(requests.Select(r => r.RequestID));
    }

    [Fact]
    public async Task WriteAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var (call, _) = MockDownstreamStream.Create(Array.Empty<KubeMQ.Grpc.QueuesDownstreamResponse>());
        var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        await handle.DisposeAsync();

        Func<Task> act = () => handle.WriteAsync(MakeGetRequest());
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task WriteAsync_AfterStreamBroken_ThrowsKubeMQOperationException()
    {
        var call = MockDownstreamStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "broken")));
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        await WaitUntilAsync(() => handle.IsStreamBroken, TimeSpan.FromSeconds(2));

        Func<Task> act = () => handle.WriteAsync(MakeGetRequest());
        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*broken*");
    }

    [Fact]
    public async Task WriteAsync_StreamWriteThrows_MarksStreamBroken()
    {
        var reader = new NeverEndingStreamReader();
        var writer = new ThrowingClientStreamWriter(new IOException("pipe broken"));
        var call = CreateCustomCall(writer, reader, () => reader.Cancel());
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        // WriteAsync enqueues to the channel (succeeds). The writer loop
        // processes the item and hits the IOException, marking the stream broken.
        await handle.WriteAsync(MakeGetRequest());

        // Give the writer loop time to process and mark broken.
        await Task.Delay(200);

        handle.IsStreamBroken.Should().BeTrue();
    }

    // ──────────────────────────── PollAsync ─────────────────────────────

    [Fact]
    public async Task PollAsync_SendsGetAndAwaitsResponse()
    {
        var expectedResponse = MakeGetResponse();
        expectedResponse.TransactionId = "txn-123";
        var (call, captured) = MockDownstreamStream.Create(expectedResponse);
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        var request = MakeGetRequest("poll-req-1");
        var response = await handle.PollAsync(request);

        response.Should().NotBeNull();
        response.RefRequestId.Should().Be("poll-req-1");
        response.TransactionId.Should().Be("txn-123");
        response.RequestTypeData.Should().Be(KubeMQ.Grpc.QueuesDownstreamRequestType.Get);
        captured.Should().ContainSingle(r => r.RequestID == "poll-req-1");
    }

    [Fact]
    public async Task PollAsync_ConcurrentPolls_ThrowsInvalidOperationException()
    {
        var reader = new NeverEndingStreamReader();
        var writer = new CapturingClientStreamWriter();
        var call = CreateCustomCall(writer, reader, () => reader.Cancel());
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        _ = handle.PollAsync(MakeGetRequest("poll-1"));
        await Task.Delay(50);

        Func<Task> act = () => handle.PollAsync(MakeGetRequest("poll-2"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already in progress*");
    }

    [Fact]
    public async Task PollAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var reader = new NeverEndingStreamReader();
        var writer = new CapturingClientStreamWriter();
        var call = CreateCustomCall(writer, reader, () => reader.Cancel());
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Func<Task> act = () => handle.PollAsync(MakeGetRequest(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PollAsync_StreamBroken_FailsPendingPoll()
    {
        var closeResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.CloseByServer,
        };
        var (call, _) = MockDownstreamStream.Create(closeResponse);
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        Func<Task> act = () => handle.PollAsync(MakeGetRequest("poll-broken"));
        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*closed*");
    }

    // ──────────────────────────── ReaderLoop ────────────────────────────

    [Fact]
    public async Task ReaderLoop_SettlementError_InvokesOnError()
    {
        var errorResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.AckAll,
            IsError = true,
            Error = "settlement failed",
        };
        var (call, _) = MockDownstreamStream.Create(errorResponse);

        string? capturedError = null;
        var errorReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var handle = new DownstreamStreamHandle(
            call, ClientId, NullLogger.Instance,
            onError: (_, error) =>
            {
                capturedError = error;
                errorReceived.TrySetResult();
            });

        await handle.WriteAsync(MakeGetRequest("trigger"));

        var winner = await Task.WhenAny(errorReceived.Task, Task.Delay(2000));
        winner.Should().Be(errorReceived.Task, "onError should be invoked within timeout");
        capturedError.Should().Be("settlement failed");
    }

    [Fact]
    public async Task ReaderLoop_CloseByServer_MarksStreamBroken()
    {
        var closeResponse = new KubeMQ.Grpc.QueuesDownstreamResponse
        {
            RequestTypeData = KubeMQ.Grpc.QueuesDownstreamRequestType.CloseByServer,
        };
        var (call, _) = MockDownstreamStream.Create(closeResponse);

        var terminated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var handle = new DownstreamStreamHandle(
            call, ClientId, NullLogger.Instance,
            onTerminated: () => terminated.TrySetResult());

        await handle.WriteAsync(MakeGetRequest());

        var winner = await Task.WhenAny(terminated.Task, Task.Delay(2000));
        winner.Should().Be(terminated.Task, "onTerminated should fire after CloseByServer");
        handle.IsStreamBroken.Should().BeTrue();
    }

    [Fact]
    public async Task ReaderLoop_StreamReadFailure_MarksStreamBrokenAndInvokesOnTerminated()
    {
        var call = MockDownstreamStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "stream broken")));

        var terminated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var handle = new DownstreamStreamHandle(
            call, ClientId, NullLogger.Instance,
            onTerminated: () => terminated.TrySetResult());

        var winner = await Task.WhenAny(terminated.Task, Task.Delay(2000));
        winner.Should().Be(terminated.Task, "onTerminated should fire after stream failure");
        handle.IsStreamBroken.Should().BeTrue();
    }

    [Fact]
    public async Task ReaderLoop_StreamEndsNormally_InvokesOnTerminated()
    {
        var writer = new CapturingClientStreamWriter();
        var reader = new EmptyStreamReader();
        var call = CreateCustomCall(writer, reader);

        var terminated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var handle = new DownstreamStreamHandle(
            call, ClientId, NullLogger.Instance,
            onTerminated: () => terminated.TrySetResult());

        var winner = await Task.WhenAny(terminated.Task, Task.Delay(2000));
        winner.Should().Be(terminated.Task, "onTerminated should fire when stream ends gracefully");
        handle.IsStreamBroken.Should().BeTrue();
    }

    // ──────────────────────────── SendCloseAsync ────────────────────────

    [Fact]
    public async Task SendCloseAsync_SendsCloseByClient_WithAllFields()
    {
        var reader = new NeverEndingStreamReader();
        var writer = new CapturingClientStreamWriter();
        var call = CreateCustomCall(writer, reader, () => reader.Cancel());
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        await handle.SendCloseAsync("txn-456");

        writer.Requests.Should().ContainSingle();
        var closeReq = writer.Requests.Single();
        closeReq.ClientID.Should().Be(ClientId);
        closeReq.RequestTypeData.Should().Be(KubeMQ.Grpc.QueuesDownstreamRequestType.CloseByClient);
        closeReq.RefTransactionId.Should().Be("txn-456");
        closeReq.RequestID.Should().NotBeNullOrEmpty();
        writer.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SendCloseAsync_StreamBroken_Skips()
    {
        var call = MockDownstreamStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "broken")));
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        await WaitUntilAsync(() => handle.IsStreamBroken, TimeSpan.FromSeconds(2));

        await handle.SendCloseAsync("txn-789");

        handle.IsStreamBroken.Should().BeTrue();
    }

    [Fact]
    public async Task SendCloseAsync_NullTransactionId_OnlyCompletes()
    {
        var reader = new NeverEndingStreamReader();
        var writer = new CapturingClientStreamWriter();
        var call = CreateCustomCall(writer, reader, () => reader.Cancel());
        await using var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        await handle.SendCloseAsync(null);

        writer.Requests.Should().BeEmpty("no close request when transactionId is null");
        writer.IsCompleted.Should().BeTrue("CompleteAsync should still be called");
    }

    // ──────────────────────────── DisposeAsync ──────────────────────────

    [Fact]
    public async Task DisposeAsync_NoProtocolWrite()
    {
        var reader = new NeverEndingStreamReader();
        var writer = new CapturingClientStreamWriter();
        var call = CreateCustomCall(writer, reader, () => reader.Cancel());
        var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        await handle.DisposeAsync();

        writer.Requests.Should().BeEmpty("DisposeAsync must not write a close request");
        writer.IsCompleted.Should().BeFalse("DisposeAsync must not call CompleteAsync");
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var (call, _) = MockDownstreamStream.Create(Array.Empty<KubeMQ.Grpc.QueuesDownstreamResponse>());
        var handle = new DownstreamStreamHandle(call, ClientId, NullLogger.Instance);

        await handle.DisposeAsync();
        await handle.DisposeAsync();

        handle.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_StopsReaderTask()
    {
        var readerAlive = true;
        var reader = new NeverEndingStreamReader();
        var writer = new CapturingClientStreamWriter();
        var call = CreateCustomCall(writer, reader, () => reader.Cancel());

        var terminated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = new DownstreamStreamHandle(
            call, ClientId, NullLogger.Instance,
            onTerminated: () => { readerAlive = false; terminated.TrySetResult(); });

        readerAlive.Should().BeTrue("reader should be alive before dispose");

        await handle.DisposeAsync();

        var winner = await Task.WhenAny(terminated.Task, Task.Delay(2000));
        winner.Should().Be(terminated.Task, "reader should terminate on dispose");
        readerAlive.Should().BeFalse();
    }

    // ──────────────────────────── Helpers ───────────────────────────────

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    /// <summary>
    /// Response reader that blocks on MoveNext until externally canceled.
    /// </summary>
    private sealed class NeverEndingStreamReader
        : IAsyncStreamReader<KubeMQ.Grpc.QueuesDownstreamResponse>
    {
        private readonly CancellationTokenSource _cts = new();

        public KubeMQ.Grpc.QueuesDownstreamResponse Current => default!;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, _cts.Token);
            try
            {
                await Task.Delay(Timeout.Infinite, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            return false;
        }

        public void Cancel() => _cts.Cancel();
    }

    /// <summary>
    /// Response reader that returns false immediately (graceful stream end).
    /// </summary>
    private sealed class EmptyStreamReader
        : IAsyncStreamReader<KubeMQ.Grpc.QueuesDownstreamResponse>
    {
        public KubeMQ.Grpc.QueuesDownstreamResponse Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>
    /// Request writer that captures written requests and tracks CompleteAsync.
    /// </summary>
    private sealed class CapturingClientStreamWriter
        : IClientStreamWriter<KubeMQ.Grpc.QueuesDownstreamRequest>
    {
        public ConcurrentBag<KubeMQ.Grpc.QueuesDownstreamRequest> Requests { get; } = [];
        public bool IsCompleted { get; private set; }
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.QueuesDownstreamRequest message)
        {
            Requests.Add(message);
            return Task.CompletedTask;
        }

        public Task WriteAsync(
            KubeMQ.Grpc.QueuesDownstreamRequest message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(message);
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            IsCompleted = true;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Request writer that throws the provided exception on every WriteAsync call.
    /// </summary>
    private sealed class ThrowingClientStreamWriter
        : IClientStreamWriter<KubeMQ.Grpc.QueuesDownstreamRequest>
    {
        private readonly Exception _exception;

        public ThrowingClientStreamWriter(Exception exception) =>
            _exception = exception;

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.QueuesDownstreamRequest message) =>
            Task.FromException(_exception);

        public Task WriteAsync(
            KubeMQ.Grpc.QueuesDownstreamRequest message,
            CancellationToken cancellationToken) =>
            Task.FromException(_exception);

        public Task CompleteAsync() => Task.CompletedTask;
    }
}
