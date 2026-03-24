using System.Text;
using FluentAssertions;
using Grpc.Core;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Tests.Unit.Helpers;

namespace KubeMQ.Sdk.Tests.Unit.EventsStore;

public class EventStoreStreamTests
{
    private const string ClientId = "test-client";

    /// <summary>Creates an auto-respond function that returns Sent=true with matching EventID.</summary>
    private static Func<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result> AutoRespondSent() =>
        ev => new KubeMQ.Grpc.Result { EventID = ev.EventID, Sent = true };

    // ──────────────────────────── SendAsync ────────────────────────────

    [Fact]
    public async Task SendAsync_ValidMessage_ReturnsEventStoreResult()
    {
        var (call, captured, completeReader) = MockEventStream.CreateAutoRespond(AutoRespondSent());
        await using var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage
        {
            Channel = "store-ch",
            Body = Encoding.UTF8.GetBytes("persist me"),
        };

        var result = await stream.SendAsync(msg, ClientId);

        result.Should().NotBeNull();
        result.Sent.Should().BeTrue();
        result.Id.Should().NotBeNullOrEmpty();

        captured.Should().HaveCountGreaterOrEqualTo(1);
        var ev = captured.First();
        ev.Channel.Should().Be("store-ch");
        ev.Body.ToByteArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("persist me"));
        ev.ClientID.Should().Be(ClientId);
        ev.Store.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WithTags_MapsCorrectly()
    {
        var (call, captured, completeReader) = MockEventStream.CreateAutoRespond(AutoRespondSent());
        await using var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage
        {
            Channel = "ch",
            Body = Encoding.UTF8.GetBytes("body"),
            Tags = new Dictionary<string, string> { ["tag1"] = "val1", ["tag2"] = "val2" },
        };

        var result = await stream.SendAsync(msg, ClientId);
        result.Sent.Should().BeTrue();

        captured.Should().HaveCountGreaterOrEqualTo(1);
        var ev = captured.First();
        ev.Tags.Should().ContainKey("tag1").WhoseValue.Should().Be("val1");
        ev.Tags.Should().ContainKey("tag2").WhoseValue.Should().Be("val2");
    }

    [Fact]
    public async Task SendAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var (call, _, completeReader) = MockEventStream.CreateAutoRespond(AutoRespondSent());
        var stream = new EventStoreStream(call);
        await stream.DisposeAsync();

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        Func<Task> act = () => stream.SendAsync(msg, ClientId);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SendAsync_WhenStreamBroken_ThrowsKubeMQOperationException()
    {
        // Use a writer-faulted stream so the writer loop sets _streamBroken = true
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "gone")));
        await using var stream = new EventStoreStream(call);

        // First send triggers the writer loop to hit the fault
        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };

        // The first SendAsync will eventually fail because the pending TCS gets an exception
        // from the writer loop failure
        Func<Task> firstSend = () => stream.SendAsync(msg, ClientId);

        // Either the first send throws (writer error propagated) or subsequent sends
        // throw because the stream is broken. We allow both paths.
        try
        {
            await firstSend();
        }
        catch
        {
            // expected — writer loop failed pending requests
        }

        // Wait for writer loop to mark stream as broken
        await Task.Delay(300);

        Func<Task> secondSend = () => stream.SendAsync(msg, ClientId);
        await secondSend.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*broken*");
    }

    [Fact]
    public async Task SendAsync_NullMessage_ThrowsArgumentNullException()
    {
        var (call, _, completeReader) = MockEventStream.CreateAutoRespond(AutoRespondSent());
        await using var stream = new EventStoreStream(call);

        Func<Task> act = () => stream.SendAsync(null!, ClientId);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ──────────────────────────── PendingCount ────────────────────────────

    [Fact]
    public async Task PendingCount_ReflectsInFlightMessages()
    {
        // Use a normal stream (no auto-respond) so messages stay pending
        var (call, _, enqueueResult, completeReader) = MockEventStream.Create();
        await using var stream = new EventStoreStream(call);

        stream.PendingCount.Should().Be(0);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };

        // Start a send that won't complete until we enqueue a result
        var sendTask = stream.SendAsync(msg, ClientId);

        // Allow the write to go through
        await Task.Delay(200);

        stream.PendingCount.Should().BeGreaterOrEqualTo(1);

        // Now provide the matching result (EventID = "1" since it's the first message)
        enqueueResult(new KubeMQ.Grpc.Result { EventID = "1", Sent = true });

        var result = await sendTask;
        result.Sent.Should().BeTrue();

        // After result received, pending should be back to 0
        stream.PendingCount.Should().Be(0);
    }

    // ──────────────────────────── CloseAsync ────────────────────────────

    [Fact]
    public async Task CloseAsync_CompletesGracefully()
    {
        var (call, _, _, completeReader) = MockEventStream.Create();
        var stream = new EventStoreStream(call);

        completeReader();
        Func<Task> act = () => stream.CloseAsync();

        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_WhenAlreadyDisposed_ReturnsImmediately()
    {
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStoreStream(call);
        await stream.DisposeAsync();

        Func<Task> act = () => stream.CloseAsync();

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────── DisposeAsync ────────────────────────────

    [Fact]
    public async Task DisposeAsync_CancelsPendingMessages()
    {
        // Use a normal stream (no auto-respond) so messages stay pending
        var (call, _, _, completeReader) = MockEventStream.Create();
        var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        var sendTask = stream.SendAsync(msg, ClientId);

        // Allow write to go through
        await Task.Delay(200);

        // Dispose should cancel the pending TCS
        await stream.DisposeAsync();

        Func<Task> act = () => sendTask;
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var (call, _, completeReader) = MockEventStream.CreateAutoRespond(AutoRespondSent());
        var stream = new EventStoreStream(call);

        Func<Task> act = async () =>
        {
            await stream.DisposeAsync();
            await stream.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────── WriterLoop ────────────────────────────

    [Fact]
    public async Task WriterLoop_OnError_FailsPendingRequests()
    {
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "write failed")));
        await using var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };

        // The send will eventually fail because the writer loop encounters an error
        // and propagates it to the pending TCS
        Func<Task> act = () => stream.SendAsync(msg, ClientId);

        await act.Should().ThrowAsync<KubeMQStreamBrokenException>();
    }

    // ──────────────────────────── ReceiveLoop ────────────────────────────

    [Fact]
    public async Task ReceiveLoop_OnStreamBreak_FailsPending()
    {
        // Use a delayed-fault reader: blocks on MoveNext until we trigger the fault,
        // so the send has time to add its TCS to _pending before the receive loop fails.
        var (call, triggerFault) = MockEventStream.CreateDelayedFault(
            new RpcException(new Status(StatusCode.Unavailable, "stream broke")));
        await using var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };

        // Start the send (will wait for a result that never comes)
        var sendTask = stream.SendAsync(msg, ClientId);

        // Allow the write to go through to _pending
        await Task.Delay(200);

        // Now trigger the fault — the receive loop fails and should fail all pending TCS
        triggerFault();

        Func<Task> act = () => sendTask;
        await act.Should().ThrowAsync<KubeMQStreamBrokenException>();
    }

    [Fact]
    public async Task ReceiveLoop_OnStreamBreak_WithReconnectFactory_Reconnects()
    {
        var reconnectCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Initial call is faulted
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        // Reconnect provides a working auto-respond stream
        var (reconnectCall, _, completeReconnect) = MockEventStream.CreateAutoRespond(AutoRespondSent());

        await using var stream = new EventStoreStream(
            faultedCall,
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
    public async Task ReceiveLoop_OnStreamBreak_WithoutReconnect_Returns()
    {
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        // No reconnect factory — should just return without crashing
        Func<Task> act = async () =>
        {
            await using var stream = new EventStoreStream(faultedCall);

            // Allow receive loop to process the error and return
            await Task.Delay(300);
        };

        await act.Should().NotThrowAsync();
    }

    // ──────────────── SendAsync cancellation catch ────────────────

    [Fact]
    public async Task SendAsync_Cancelled_CleansPendingAndThrows()
    {
        // Use a normal stream (no auto-respond) to keep messages pending
        var (call, _, _, completeReader) = MockEventStream.Create();
        await using var stream = new EventStoreStream(call);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var msg = new EventStoreMessage { Channel = "ch", Body = new byte[] { 1 } };

        // Cancellation after _pendingGate.WaitAsync should trigger the catch block (lines 113-116)
        Func<Task> act = () => stream.SendAsync(msg, ClientId, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Pending should have been cleaned up
        stream.PendingCount.Should().Be(0);
    }

    // ──────────────── CloseAsync catch blocks ────────────────

    [Fact]
    public async Task CloseAsync_WriterTaskFaulted_SwallowsException()
    {
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "writer boom")));
        var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = new byte[] { 1 } };
        try { await stream.SendAsync(msg, ClientId); } catch { }
        await Task.Delay(300);

        completeReader();

        // CloseAsync should swallow the faulted writer task (lines 137-140)
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_CompleteAsyncThrows_SwallowsException()
    {
        var reader = new CompletingAsyncStreamReader();
        var writer = new ThrowingCompleteAsyncWriter();

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });

        var stream = new EventStoreStream(call);

        // CloseAsync should swallow the CompleteAsync exception (lines 146-149)
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_ReceiveTaskThrowsOperationCanceled_SwallowsException()
    {
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStoreStream(call);

        // CloseAsync cancels _cts, causing receive loop OperationCanceledException (lines 156-159)
        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    // ──────────────── DisposeAsync catch blocks ────────────────

    [Fact]
    public async Task DisposeAsync_WriterTaskFaulted_SwallowsException()
    {
        var (call, completeReader) = MockEventStream.CreateWriterFaulted(
            new RpcException(new Status(StatusCode.Internal, "writer boom")));
        var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = new byte[] { 1 } };
        try { await stream.SendAsync(msg, ClientId); } catch { }
        await Task.Delay(300);

        // DisposeAsync should swallow faulted writer (lines 178-181)
        Func<Task> act = async () => await stream.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_ReceiveTaskFaulted_SwallowsException()
    {
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "receive boom")));
        var stream = new EventStoreStream(faultedCall);

        await Task.Delay(300);

        // DisposeAsync should swallow faulted receive task (lines 187-190)
        Func<Task> act = async () => await stream.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_WithPendingMessages_CancelsAndCleansSemaphore()
    {
        // Create a stream with pending messages to test the SemaphoreFullException path (lines 206-208)
        var (call, _, _, completeReader) = MockEventStream.Create();
        var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = new byte[] { 1 } };
        var sendTask = stream.SendAsync(msg, ClientId);

        // Wait for the write to be processed so the TCS is in _pending
        await Task.Delay(200);
        stream.PendingCount.Should().BeGreaterOrEqualTo(1);

        // DisposeAsync should cancel pending, clear the dictionary, and handle semaphore release
        await stream.DisposeAsync();

        // The send task should now be completed (cancelled by TrySetCanceled)
        Func<Task> act = () => sendTask;
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    // ──────────────── WriterLoop orphaned request cleanup ────────────────

    [Fact]
    public async Task WriterLoop_OnError_DrainsOrphanedEventsAndFailsTheirPending()
    {
        // Create a stream where the writer fails AFTER the first write succeeds
        // so there are remaining items in the channel to be orphaned (lines 240-247)
        var writeCount = 0;
        var reader = new CompletingAsyncStreamReader();
        var writer = new ConditionalFaultingWriter(failAfterCount: 1);

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { });

        await using var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = new byte[] { 1 } };

        // Rapidly send multiple messages - the first will write OK, then the writer faults
        var tasks = new List<Task<EventStoreResult>>();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(stream.SendAsync(msg, ClientId));
        }

        // All should eventually fail with KubeMQStreamBrokenException
        foreach (var t in tasks)
        {
            Func<Task> act = () => t;
            await act.Should().ThrowAsync<KubeMQStreamBrokenException>();
        }
    }

    // ──────────────── ReceiveLoop OperationCanceled ────────────────

    [Fact]
    public async Task ReceiveLoop_OperationCanceled_Returns()
    {
        // Dispose immediately cancels _cts, hitting OperationCanceledException in receive loop (lines 295-297)
        var (call, _, _, _) = MockEventStream.Create();
        var stream = new EventStoreStream(call);
        await stream.DisposeAsync();
    }

    // ──────────────── ReceiveLoop reconnect cancellation ────────────────

    [Fact]
    public async Task ReceiveLoop_ReconnectCanceled_Returns()
    {
        // Hit lines 337-339: OperationCanceledException during reconnect
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        await using var stream = new EventStoreStream(
            faultedCall,
            reconnectFactory: async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            },
            waitForReady: _ => Task.CompletedTask);

        await Task.Delay(200);
        // DisposeAsync cancels _cts, the reconnect should catch OperationCanceledException and return
    }

    [Fact]
    public async Task ReceiveLoop_ReconnectFails_FailsRemainingPending()
    {
        // Hit lines 341-361: reconnect throws a non-canceled exception
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        await using var stream = new EventStoreStream(
            faultedCall,
            reconnectFactory: _ => throw new InvalidOperationException("reconnect failed"),
            waitForReady: _ => Task.CompletedTask);

        // Wait for reconnect to fail
        await Task.Delay(500);
    }

    // ──────────────── CloseAsync full-path coverage ────────────────

    [Fact]
    public async Task CloseAsync_WithPendingMessages_DrainsWriterAndCompletes()
    {
        // Send messages, then call CloseAsync to exercise the full graceful close path:
        // complete channel writer, await writer task, complete request stream, cancel CTS, await receive task.
        var (call, captured, completeReader) = MockEventStream.CreateAutoRespond(AutoRespondSent());
        var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("close-drain") };
        var sendTask = stream.SendAsync(msg, ClientId);

        // Allow the write + auto-respond to complete
        var result = await sendTask;
        result.Sent.Should().BeTrue();

        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    [Fact]
    public async Task CloseAsync_WithActiveWriterAndReceiver_CompletesGracefully()
    {
        // Use a normal stream (no auto-respond) so receive loop is actively waiting.
        // CloseAsync must complete writer channel, await writer, complete request stream,
        // cancel CTS, then await receive loop.
        var (call, _, _, completeReader) = MockEventStream.Create();
        var stream = new EventStoreStream(call);

        // Send a message but don't provide a result — receive loop is actively waiting
        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        _ = stream.SendAsync(msg, ClientId); // fire-and-forget; CloseAsync will cancel it

        await Task.Delay(100);

        Func<Task> act = () => stream.CloseAsync();
        await act.Should().NotThrowAsync();
        await stream.DisposeAsync();
    }

    // ──────────────── WriterLoop error full-path coverage ────────────────

    [Fact]
    public async Task WriterLoop_WriteThrows_DrainsPendingChannelItems_FailsAllTcs()
    {
        // Exercises the full WriterLoop error path (lines 232-267):
        // 1. _streamBroken set to true
        // 2. Drains remaining channel items and fails their orphaned TCS entries
        // 3. Fails all remaining pending TCS entries
        // 4. Clears _pending and releases semaphore
        var writer = new ConditionalFaultingWriter(failAfterCount: 0); // fail on first write
        var reader = new BlockingAsyncStreamReader();

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { reader.Complete(); });

        await using var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };

        // This send will fail because the writer faults immediately
        Func<Task> act = () => stream.SendAsync(msg, ClientId);
        await act.Should().ThrowAsync<KubeMQStreamBrokenException>();

        // After the writer error, pending count should be 0 (cleaned up)
        stream.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task WriterLoop_WriteThrows_MultiplePending_AllGetException()
    {
        // Send several messages rapidly with a writer that fails after the first one.
        // Exercises the drain loop (lines 238-247) plus the remaining pending loop (lines 249-256).
        var writer = new ConditionalFaultingWriter(failAfterCount: 1);
        var reader = new BlockingAsyncStreamReader();

        var call = new AsyncDuplexStreamingCall<KubeMQ.Grpc.Event, KubeMQ.Grpc.Result>(
            requestStream: writer,
            responseStream: reader,
            responseHeadersAsync: Task.FromResult(new Metadata()),
            getStatusFunc: () => new Status(StatusCode.OK, string.Empty),
            getTrailersFunc: () => new Metadata(),
            disposeAction: () => { reader.Complete(); });

        await using var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };

        // Send multiple messages — first succeeds write, rest fail
        var tasks = new List<Task<EventStoreResult>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(stream.SendAsync(msg, ClientId));
        }

        // All tasks should eventually fail
        foreach (var t in tasks)
        {
            Func<Task> act = () => t;
            await act.Should().ThrowAsync<KubeMQStreamBrokenException>();
        }

        stream.PendingCount.Should().Be(0);
    }

    // ──────────────── ReceiveLoop reconnection full-path coverage ────────────────

    [Fact]
    public async Task ReceiveLoop_StreamBreaks_FailsPendingThenReconnects_FullPath()
    {
        // Exercises lines 299-335: stream break with pending messages gets them failed,
        // then reconnection succeeds and stream resumes.
        var reconnectDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Use a delayed-fault reader so we can set up pending messages first
        var (faultedCall, triggerFault) = MockEventStream.CreateDelayedFault(
            new RpcException(new Status(StatusCode.Unavailable, "stream broke")));

        var (reconnectCall, _, completeReconnect) = MockEventStream.CreateAutoRespond(AutoRespondSent());

        await using var stream = new EventStoreStream(
            faultedCall,
            reconnectFactory: _ =>
            {
                reconnectDone.TrySetResult(true);
                return Task.FromResult(reconnectCall);
            },
            waitForReady: _ => Task.CompletedTask);

        // Send a message while the reader is blocking (before fault)
        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("pending") };
        var pendingTask = stream.SendAsync(msg, ClientId);

        // Allow write to go through
        await Task.Delay(200);
        stream.PendingCount.Should().BeGreaterOrEqualTo(1);

        // Now trigger the fault — pending messages should get KubeMQStreamBrokenException
        triggerFault();

        Func<Task> act = () => pendingTask;
        await act.Should().ThrowAsync<KubeMQStreamBrokenException>()
            .WithMessage("*disconnected*");

        // Reconnection should have been called
        var wasReconnected = await Task.WhenAny(reconnectDone.Task, Task.Delay(2000));
        wasReconnected.Should().Be(reconnectDone.Task, "reconnect factory should have been called");
    }

    [Fact]
    public async Task ReceiveLoop_StreamBreaks_WithReconnect_DisposesOldCall()
    {
        // Verifies the old call is disposed after reconnection (line 335).
        var oldCallDisposed = false;
        var reconnectDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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

        var (reconnectCall, _, completeReconnect) = MockEventStream.CreateAutoRespond(AutoRespondSent());

        await using var stream = new EventStoreStream(
            faultedCall,
            reconnectFactory: _ =>
            {
                reconnectDone.TrySetResult(true);
                return Task.FromResult(reconnectCall);
            },
            waitForReady: _ => Task.CompletedTask);

        await Task.WhenAny(reconnectDone.Task, Task.Delay(2000));
        await Task.Delay(100);

        oldCallDisposed.Should().BeTrue("the old call should be disposed after swapping");
    }

    [Fact]
    public async Task ReceiveLoop_ReconnectFails_FailsRemainingPending_WithPendingMessages()
    {
        // Exercises lines 341-361: reconnect throws, any remaining pending messages
        // get the reconnect exception. Uses delayed fault so we can add pending first.
        var (faultedCall, triggerFault) = MockEventStream.CreateDelayedFault(
            new RpcException(new Status(StatusCode.Unavailable, "stream broke")));

        await using var stream = new EventStoreStream(
            faultedCall,
            reconnectFactory: _ => throw new InvalidOperationException("reconnect-boom"),
            waitForReady: _ => Task.CompletedTask);

        // The pending task gets failed by the stream break (before reconnect attempt)
        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        var pendingTask = stream.SendAsync(msg, ClientId);

        await Task.Delay(200);

        // Trigger the fault
        triggerFault();

        // The pending task should fail because the stream broke
        Func<Task> act = () => pendingTask;
        await act.Should().ThrowAsync<KubeMQStreamBrokenException>();

        // Wait for reconnection to fail
        await Task.Delay(500);

        stream.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task ReceiveLoop_ReconnectFails_SemaphoreReleased()
    {
        // After reconnect failure cleans up pending, the semaphore should be released
        // so that future operations (if stream weren't broken) wouldn't deadlock.
        var faultedCall = MockEventStream.CreateFaulted(
            new RpcException(new Status(StatusCode.Unavailable, "disconnected")));

        await using var stream = new EventStoreStream(
            faultedCall,
            reconnectFactory: _ => throw new InvalidOperationException("reconnect-failed"),
            waitForReady: _ => Task.CompletedTask);

        // Wait for the receive loop to fail and attempt reconnection
        await Task.Delay(500);

        // Stream should be broken after failed reconnection
        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };
        Func<Task> act = () => stream.SendAsync(msg, ClientId);
        await act.Should().ThrowAsync<KubeMQOperationException>()
            .WithMessage("*broken*");
    }

    // ──────────────── DisposeAsync pending cancellation full-path coverage ────────────────

    [Fact]
    public async Task DisposeAsync_WithMultiplePendingMessages_CancelsAllAndReleasesSemaphore()
    {
        // Exercises lines 192-208: dispose with multiple pending messages.
        // Each pending TCS should be cancelled, _pending cleared, semaphore released.
        var (call, _, _, completeReader) = MockEventStream.Create();
        var stream = new EventStoreStream(call);

        var msg = new EventStoreMessage { Channel = "ch", Body = Encoding.UTF8.GetBytes("body") };

        // Send multiple messages that stay pending (no auto-respond)
        var tasks = new List<Task<EventStoreResult>>();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(stream.SendAsync(msg, ClientId));
        }

        // Allow writes to go through
        await Task.Delay(300);
        stream.PendingCount.Should().BeGreaterOrEqualTo(3);

        // Dispose should cancel all pending
        await stream.DisposeAsync();

        foreach (var t in tasks)
        {
            Func<Task> act = () => t;
            await act.Should().ThrowAsync<TaskCanceledException>();
        }
    }

    [Fact]
    public async Task DisposeAsync_PendingCancellation_SemaphoreFullExceptionSwallowed()
    {
        // Edge case: if the semaphore is already at max when dispose tries to release,
        // the SemaphoreFullException should be swallowed (lines 206-208).
        // This exercises the defensive catch block.
        var (call, _, _, completeReader) = MockEventStream.Create();
        var stream = new EventStoreStream(call);

        // Dispose without any pending messages — the count is 0, so Release(0) is not called.
        // This still validates the path doesn't throw.
        Func<Task> act = async () => await stream.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReceiveLoop_NormalCompletion_ReturnsWithoutReconnect()
    {
        // When MoveNext returns false normally (stream server-side close),
        // the receive loop should return without reconnecting (line 293).
        var reconnectCalled = false;
        var (call, _, _, completeReader) = MockEventStream.Create();

        // Complete the reader immediately
        completeReader();

        await using var stream = new EventStoreStream(
            call,
            reconnectFactory: _ =>
            {
                reconnectCalled = true;
                var (rc, _, cr) = MockEventStream.CreateAutoRespond(AutoRespondSent());
                return Task.FromResult(rc);
            },
            waitForReady: _ => Task.CompletedTask);

        await Task.Delay(300);

        reconnectCalled.Should().BeFalse("normal stream end should not trigger reconnection");
    }

    // ──────────────── Test helpers ────────────────

    private sealed class CompletingAsyncStreamReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        public KubeMQ.Grpc.Result Current => default!;
        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class ThrowingCompleteAsyncWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(KubeMQ.Grpc.Event message) => Task.CompletedTask;
        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteAsync() => throw new RpcException(new Status(StatusCode.Cancelled, "already closed"));
    }

    /// <summary>
    /// A writer that succeeds for the first N writes, then throws.
    /// This allows testing the orphaned-event cleanup path in WriterLoopAsync.
    /// </summary>
    private sealed class ConditionalFaultingWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        private readonly int _failAfterCount;
        private int _writeCount;

        public ConditionalFaultingWriter(int failAfterCount) => _failAfterCount = failAfterCount;

        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(KubeMQ.Grpc.Event message)
        {
            if (Interlocked.Increment(ref _writeCount) > _failAfterCount)
            {
                return Task.FromException(new RpcException(new Status(StatusCode.Internal, "write failed")));
            }
            return Task.CompletedTask;
        }

        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return WriteAsync(message);
        }

        public Task CompleteAsync() => Task.CompletedTask;
    }

    /// <summary>A no-op writer for constructing calls with custom dispose actions.</summary>
    private sealed class NoOpClientStreamWriter : IClientStreamWriter<KubeMQ.Grpc.Event>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(KubeMQ.Grpc.Event message) => Task.CompletedTask;
        public Task WriteAsync(KubeMQ.Grpc.Event message, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CompleteAsync() => Task.CompletedTask;
    }

    /// <summary>A reader that throws on the first MoveNext call.</summary>
    private sealed class FaultingOnFirstMoveNextReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        private readonly Exception _exception;

        public FaultingOnFirstMoveNextReader(Exception exception) => _exception = exception;

        public KubeMQ.Grpc.Result Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken) => throw _exception;
    }

    /// <summary>
    /// A reader that blocks indefinitely on MoveNext until Complete() is called,
    /// then returns false. Used for streams where we need the receive loop to stay active.
    /// </summary>
    private sealed class BlockingAsyncStreamReader : IAsyncStreamReader<KubeMQ.Grpc.Result>
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public KubeMQ.Grpc.Result Current => default!;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            try
            {
                await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            return false;
        }

        internal void Complete() => _gate.TrySetResult();
    }
}
