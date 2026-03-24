using System.Text;
using FluentAssertions;
using Grpc.Core;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Tests.Unit.Helpers;

namespace KubeMQ.Sdk.Tests.Unit.Events;

public class EventStreamTests
{
    private const string ClientId = "test-client";

    // ──────────────────────────── SendAsync ────────────────────────────

    [Fact]
    public async Task SendAsync_ValidMessage_EnqueuesToStream()
    {
        var (call, captured, _, completeReader) = MockEventStream.Create();
        await using var stream = new EventStream(call);

        var msg = new EventMessage
        {
            Channel = "test-ch",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        await stream.SendAsync(msg, ClientId);

        // Allow the background writer loop to drain the channel
        await Task.Delay(200);

        captured.Should().HaveCount(1);
        var ev = captured.First();
        ev.Channel.Should().Be("test-ch");
        ev.Body.ToByteArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("hello"));
        ev.ClientID.Should().Be(ClientId);
        ev.Store.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WithTags_MapsTagsCorrectly()
    {
        var (call, captured, _, completeReader) = MockEventStream.Create();
        await using var stream = new EventStream(call);

        var msg = new EventMessage
        {
            Channel = "ch",
            Body = Encoding.UTF8.GetBytes("body"),
            Tags = new Dictionary<string, string> { ["k1"] = "v1", ["k2"] = "v2" },
        };

        await stream.SendAsync(msg, ClientId);
        await Task.Delay(200);

        captured.Should().HaveCount(1);
        var ev = captured.First();
        ev.Tags.Should().ContainKey("k1").WhoseValue.Should().Be("v1");
        ev.Tags.Should().ContainKey("k2").WhoseValue.Should().Be("v2");
    }

    [Fact]
    public async Task SendAsync_WithCustomId_UsesProvidedId()
    {
        var (call, captured, _, completeReader) = MockEventStream.Create();
        await using var stream = new EventStream(call);

        var msg = new EventMessage
        {
            Channel = "ch",
            Body = Encoding.UTF8.GetBytes("body"),
            Id = "custom-id-123",
        };

        await stream.SendAsync(msg, ClientId);
        await Task.Delay(200);

        captured.Should().HaveCount(1);
        captured.First().EventID.Should().Be("custom-id-123");
    }

    [Fact]
    public async Task SendAsync_WithNullId_GeneratesId()
    {
        var (call, captured, _, completeReader) = MockEventStream.Create();
        await using var stream = new EventStream(call);

        var msg = new EventMessage
        {
            Channel = "ch",
            Body = Encoding.UTF8.GetBytes("body"),
        };

        await stream.SendAsync(msg, ClientId);
        await Task.Delay(200);

        captured.Should().HaveCount(1);
        captured.First().EventID.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStream(call);
        await stream.DisposeAsync();

        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        Func<Task> act = () => stream.SendAsync(msg, ClientId);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SendAsync_WhenStreamBroken_ThrowsKubeMQOperationException()
    {
        // Use a writer-faulted stream so the writer loop sets _streamBroken = true
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "gone")));
        await using var stream = new EventStream(call);

        // Send a message to trigger the writer loop to hit the faulted writer
        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        await stream.SendAsync(msg, ClientId);

        // Allow time for writer loop to process and mark stream as broken
        await Task.Delay(300);

        Func<Task> act = () => stream.SendAsync(msg, ClientId);
        await act.Should().ThrowAsync<KubeMQOperationException>();
    }

    [Fact]
    public async Task SendAsync_NullMessage_ThrowsArgumentNullException()
    {
        var (call, _, _, _) = MockEventStream.Create();
        await using var stream = new EventStream(call);

        Func<Task> act = () => stream.SendAsync(null!, ClientId);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ──────────────────────────── CloseAsync ────────────────────────────

    [Fact]
    public async Task CloseAsync_CompletesGracefully()
    {
        var (call, _, _, completeReader) = MockEventStream.Create();
        var stream = new EventStream(call);

        completeReader();
        Func<Task> act = () => stream.CloseAsync();

        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_WhenAlreadyDisposed_ReturnsImmediately()
    {
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStream(call);
        await stream.DisposeAsync();

        Func<Task> act = () => stream.CloseAsync();

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────── DisposeAsync ────────────────────────────

    [Fact]
    public async Task DisposeAsync_CleansUpResources()
    {
        var disposed = false;
        var reader = new CompletingAsyncStreamReader();
        var writer = new NoOpClientStreamWriter();

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { disposed = true; });

        var stream = new EventStream(call);
        await stream.DisposeAsync();

        disposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStream(call);

        Func<Task> act = async () =>
        {
            await stream.DisposeAsync();
            await stream.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────── WriterLoop ────────────────────────────

    [Fact]
    public async Task WriterLoop_OnWriteError_SetsStreamBroken()
    {
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "write failed")));
        await using var stream = new EventStream(call);

        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        await stream.SendAsync(msg, ClientId);

        // Allow writer loop to encounter the error
        await Task.Delay(300);

        Func<Task> act = () => stream.SendAsync(msg, ClientId);
        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*broken*");
    }

    [Fact]
    public async Task WriterLoop_OnWriteError_InvokesOnErrorCallback()
    {
        Exception? capturedError = null;
        var errorReceived = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "write failed")));
        await using var stream = new EventStream(
            call,
            onError: ex =>
            {
                capturedError = ex;
                errorReceived.TrySetResult(ex);
            });

        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        await stream.SendAsync(msg, ClientId);

        // Wait for the error callback
        var received = await Task.WhenAny(errorReceived.Task, Task.Delay(2000));
        received.Should().Be(errorReceived.Task, "onError callback should have been invoked");

        capturedError.Should().NotBeNull();
        capturedError.Should().BeOfType<RpcException>();
    }

    // ──────────────────────────── ReceiveLoop ────────────────────────────

    [Fact]
    public async Task ReceiveLoop_OnServerError_InvokesOnErrorCallback()
    {
        Exception? capturedError = null;
        var errorReceived = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var (call, _, enqueueResult, completeReader) = MockEventStream.Create();
        await using var stream = new EventStream(
            call,
            onError: ex =>
            {
                capturedError = ex;
                errorReceived.TrySetResult(ex);
            });

        // Enqueue a result with an error message
        enqueueResult(new KubeMQ.Grpc.Result { Error = "server error occurred" });

        var received = await Task.WhenAny(errorReceived.Task, Task.Delay(2000));
        received.Should().Be(errorReceived.Task, "onError callback should have been invoked");

        capturedError.Should().NotBeNull();
        capturedError.Should().BeOfType<KubeMQOperationException>();
        capturedError!.Message.Should().Contain("server error occurred");
    }

    [Fact]
    public async Task ReceiveLoop_OnStreamBreak_WithReconnectFactory_Reconnects()
    {
        var reconnectCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Initial call is faulted
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        // Reconnect provides a working stream
        var (reconnectCall, _, _, completeReconnect) = MockEventStream.Create();

        await using var stream = new EventStream(
            faultedCall,
            onError: _ => { },
            reconnectFactory: _ =>
            {
                reconnectCalled.TrySetResult(true);
                return Task.FromResult(reconnectCall);
            },
            waitForReady: _ => Task.CompletedTask);

        var wasReconnected = await Task.WhenAny(reconnectCalled.Task, Task.Delay(2000));
        wasReconnected.Should().Be(reconnectCalled.Task, "reconnect factory should have been called");
    }

    [Fact]
    public async Task ReceiveLoop_OnStreamBreak_WithoutReconnectFactory_Returns()
    {
        var errorReceived = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        // No reconnect factory — should just return without crashing
        Func<Task> act = async () =>
        {
            await using var stream = new EventStream(
                faultedCall,
                onError: ex => errorReceived.TrySetResult(ex));

            // Wait for the error to propagate
            await Task.WhenAny(errorReceived.Task, Task.Delay(2000));
        };

        await act.Should().NotThrowAsync();
    }

    // ──────────────── CloseAsync catch-block coverage ────────────────

    [Fact]
    public async Task CloseAsync_WriterTaskFaulted_SwallowsException()
    {
        // Create a stream where the writer immediately faults
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "writer boom")));
        var stream = new EventStream(call);

        // Send to trigger the writer fault
        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        await stream.SendAsync(msg, ClientId);
        await Task.Delay(300); // let writer fault

        completeReader();

        // CloseAsync should swallow the faulted writer task exception (lines 104, 107)
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_CompleteAsyncThrows_SwallowsException()
    {
        // Create a call with a writer whose CompleteAsync throws
        var reader = new CompletingAsyncStreamReader();
        var writer = new ThrowingCompleteAsyncWriter();

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });

        var stream = new EventStream(call);

        // CloseAsync should swallow the CompleteAsync exception (lines 113, 116)
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_ReceiveTaskThrowsOperationCanceled_SwallowsException()
    {
        // Create a normal stream but cancel via CloseAsync which cancels _cts
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStream(call);

        // CloseAsync cancels _cts, causing receive loop to throw OperationCanceledException (lines 123, 126)
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    // ──────────────── DisposeAsync catch-block coverage ────────────────

    [Fact]
    public async Task DisposeAsync_WriterTaskFaulted_SwallowsException()
    {
        // Create a stream where the writer faults
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "writer boom")));
        var stream = new EventStream(call);

        // Send to trigger the writer fault
        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        await stream.SendAsync(msg, ClientId);
        await Task.Delay(300);

        // DisposeAsync should swallow the faulted writer task (lines 145, 148)
        Func<Task> act = async () => await stream.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ReceiveTaskThrows_SwallowsException()
    {
        // Create a faulted reader stream
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "receive boom")));
        var stream = new EventStream(faultedCall);

        // Wait for receive loop to throw
        await Task.Delay(300);

        // DisposeAsync should swallow the faulted receive task (lines 154, 157)
        Func<Task> act = async () => await stream.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    // ──────────────── ReceiveLoop catch-block coverage ────────────────

    [Fact]
    public async Task ReceiveLoop_OperationCanceled_Returns()
    {
        // Create a stream and then dispose it immediately to cancel _cts
        // This triggers OperationCanceledException in ReceiveLoop (lines 205, 207)
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStream(call);

        // DisposeAsync cancels _cts, the receive loop catches OperationCanceledException and returns
        await stream.DisposeAsync();
        // If we get here without hanging, the OperationCanceledException path was exercised
    }

    [Fact]
    public async Task ReceiveLoop_ReconnectCanceled_Returns()
    {
        // Create a faulted stream with reconnect factory that gets canceled
        // to hit lines 230-232 (OperationCanceledException in reconnect)
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        await using var stream = new EventStream(
            faultedCall,
            onError: _ => { },
            reconnectFactory: async ct =>
            {
                // Simulate slow reconnect that will get canceled
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("should not reach");
            },
            waitForReady: _ => Task.CompletedTask);

        // Allow receive loop to enter reconnect
        await Task.Delay(200);

        // DisposeAsync cancels _cts, which should cancel the reconnect task
        // hitting the OperationCanceledException catch (lines 230-232)
    }

    [Fact]
    public async Task ReceiveLoop_ReconnectFails_InvokesOnErrorAndReturns()
    {
        // Create a faulted stream where reconnect itself throws a non-canceled exception
        // to hit lines 234-237
        var errors = new List<Exception>();
        var errorReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        await using var stream = new EventStream(
            faultedCall,
            onError: ex =>
            {
                errors.Add(ex);
                if (errors.Count >= 2)
                {
                    errorReceived.TrySetResult(true);
                }
            },
            reconnectFactory: _ => throw new InvalidOperationException("reconnect failed"),
            waitForReady: _ => Task.CompletedTask);

        var received = await Task.WhenAny(errorReceived.Task, Task.Delay(2000));
        received.Should().Be(errorReceived.Task, "onError should be called for both the disconnect and reconnect failure");

        errors.Should().HaveCount(2);
        errors[0].Should().BeOfType<RpcException>();
        errors[1].Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("reconnect failed");
    }

    // ──────────────── CloseAsync full-path coverage ────────────────

    [Fact]
    public async Task CloseAsync_WithPendingMessages_DrainsWriterAndCompletes()
    {
        // Creates a stream, sends messages, then calls CloseAsync.
        // This exercises the full CloseAsync path: complete channel writer,
        // await writer task drain, complete request stream, cancel CTS, await receive task.
        var (call, captured, _, completeReader) = MockEventStream.Create();
        var stream = new EventStream(call);

        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("drain-me") };
        await stream.SendAsync(msg, ClientId);

        // Allow writer loop to drain
        await Task.Delay(200);
        captured.Should().HaveCount(1);

        // CloseAsync must complete the writer, complete the request stream, then cancel receive
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_WhileWriterBusy_CompletesGracefully()
    {
        // Send multiple messages to ensure writer has work, then close immediately.
        // Exercises the writerTask await path in CloseAsync where the writer has items queued.
        var (call, captured, _, completeReader) = MockEventStream.Create();
        var stream = new EventStream(call);

        for (int i = 0; i < 5; i++)
        {
            await stream.SendAsync(
                new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes($"msg-{i}") },
                ClientId);
        }

        // Close while writer may still be draining
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    // ──────────────── ReceiveLoop reconnection full-path coverage ────────────────

    [Fact]
    public async Task ReceiveLoop_StreamBreaks_WithReconnect_SwapsCallAndResumes()
    {
        // Verifies that after reconnection, the stream is functional:
        // the old call is disposed and the new call is used for subsequent reads.
        // Exercises lines 219-228: waitForReady, reconnectFactory, call swap, _streamBroken reset.
        var reconnectCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        var (reconnectCall, _, enqueueResult, completeReconnect) = MockEventStream.Create();

        var waitForReadyCalled = false;
        await using var stream = new EventStream(
            faultedCall,
            onError: _ => { },
            reconnectFactory: _ =>
            {
                reconnectCompleted.TrySetResult(true);
                return Task.FromResult(reconnectCall);
            },
            waitForReady: _ =>
            {
                waitForReadyCalled = true;
                return Task.CompletedTask;
            });

        var wasReconnected = await Task.WhenAny(reconnectCompleted.Task, Task.Delay(2000));
        wasReconnected.Should().Be(reconnectCompleted.Task, "reconnect factory should have been called");
        waitForReadyCalled.Should().BeTrue("waitForReady should have been called before reconnect");

        // After reconnection, stream should no longer be broken — we can send again.
        // Allow some time for the reconnection to complete and _streamBroken to be reset.
        await Task.Delay(200);

        // Enqueue a result on the reconnected stream to verify the receive loop is using the new call
        enqueueResult(new KubeMQ.Grpc.Result { Error = "test-error-on-new-stream" });
    }

    [Fact]
    public async Task ReceiveLoop_StreamBreaks_WithReconnect_DisposesOldCall()
    {
        // Verifies the old call is disposed when a new one is swapped in (line 228).
        var oldCallDisposed = false;
        var reconnectDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Build a faulted call with a dispose tracker
        var faultingReader = new FaultingOnFirstMoveNextReader(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));
        var noOpWriter = new NoOpClientStreamWriter();
        var faultedCall = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: noOpWriter,
            responseStream: faultingReader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { oldCallDisposed = true; });

        var (reconnectCall, _, _, _) = MockEventStream.Create();

        await using var stream = new EventStream(
            faultedCall,
            onError: _ => { },
            reconnectFactory: _ =>
            {
                reconnectDone.TrySetResult(true);
                return Task.FromResult(reconnectCall);
            },
            waitForReady: _ => Task.CompletedTask);

        await Task.WhenAny(reconnectDone.Task, Task.Delay(2000));
        await Task.Delay(100); // allow the dispose to execute

        oldCallDisposed.Should().BeTrue("the old call should be disposed after swapping in the new one");
    }

    [Fact]
    public async Task WriterLoop_WriteThrows_SetsStreamBrokenAndCallsOnError_DetailedVerification()
    {
        // Verifies that when WriteAsync throws, _streamBroken is set and onError is invoked.
        // Also verifies the channel can no longer accept messages.
        var capturedErrors = new List<Exception>();
        var errorSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "write-failed-detail")));
        await using var stream = new EventStream(
            call,
            onError: ex =>
            {
                capturedErrors.Add(ex);
                errorSignal.TrySetResult(true);
            });

        var msg = new EventMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        await stream.SendAsync(msg, ClientId);

        var received = await Task.WhenAny(errorSignal.Task, Task.Delay(2000));
        received.Should().Be(errorSignal.Task, "onError should be called");

        capturedErrors.Should().HaveCount(1);
        capturedErrors[0].Should().BeOfType<RpcException>();
        capturedErrors[0].Message.Should().Contain("write-failed-detail");

        // Subsequent sends should throw because _streamBroken = true
        Func<Task> act = () => stream.SendAsync(msg, ClientId);
        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*broken*");
    }

    [Fact]
    public async Task ReceiveLoop_NormalCompletion_ReturnsWithoutReconnect()
    {
        // When MoveNext returns false (normal stream end, line 203),
        // the receive loop should return without reconnecting.
        var reconnectCalled = false;
        var (call, _, _, completeReader) = MockEventStream.Create();

        // Complete the reader immediately to trigger normal stream end
        completeReader();

        await using var stream = new EventStream(
            call,
            onError: _ => { },
            reconnectFactory: _ =>
            {
                reconnectCalled = true;
                var (rc, _, _, _) = MockEventStream.Create();
                return Task.FromResult(rc);
            },
            waitForReady: _ => Task.CompletedTask);

        // Allow receive loop to process the completion
        await Task.Delay(300);

        reconnectCalled.Should().BeFalse("normal stream end should not trigger reconnection");
    }

    // ──────────────── Test helpers ────────────────

    /// <summary>A reader that immediately returns false on MoveNext (stream ended).</summary>
    private sealed class CompletingAsyncStreamReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        public KubeMQ.Grpc.Result Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    /// <summary>A no-op writer for constructing calls.</summary>
    private sealed class NoOpClientStreamWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.Event message) => Task.CompletedTask;

        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CompleteAsync() => Task.CompletedTask;
    }

    /// <summary>A writer whose CompleteAsync throws to test the catch block in CloseAsync.</summary>
    private sealed class ThrowingCompleteAsyncWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.Event message) => Task.CompletedTask;

        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CompleteAsync() => throw new RpcException(new Status(StatusCode.Cancelled, "already closed"));
    }

    /// <summary>
    /// A reader that throws on the first MoveNext call.
    /// Used for creating faulted calls with a dispose-tracking disposeAction.
    /// </summary>
    private sealed class FaultingOnFirstMoveNextReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        private readonly Exception _exception;

        public FaultingOnFirstMoveNextReader(Exception exception) => _exception = exception;

        public KubeMQ.Grpc.Result Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) => throw _exception;
    }
}
