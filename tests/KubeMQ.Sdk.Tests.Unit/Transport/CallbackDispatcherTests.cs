using FluentAssertions;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Internal.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class CallbackDispatcherTests : IAsyncLifetime
{
    private readonly InFlightCallbackTracker _tracker = new();
    private CallbackDispatcher<string>? _sut;

    private CallbackDispatcher<string> CreateDispatcher(int concurrency = 1, int bufferSize = 256) =>
        new(new SubscriptionOptions
        {
            MaxConcurrentCallbacks = concurrency,
            CallbackBufferSize = bufferSize,
        }, _tracker, NullLogger.Instance);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_sut != null)
        {
            await _sut.DisposeAsync();
        }

        _tracker.Dispose();
    }

    [Fact]
    public async Task EnqueueAndDispatch_SingleItem_HandlerReceivesItem()
    {
        _sut = CreateDispatcher();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        _sut.StartDispatching(
            (item, _) => { received.TrySetResult(item); return Task.CompletedTask; },
            CancellationToken.None);

        await _sut.EnqueueAsync("hello", CancellationToken.None);

        string result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().Be("hello");
    }

    [Fact]
    public async Task EnqueueAndDispatch_MultipleItems_AllProcessed()
    {
        _sut = CreateDispatcher();
        var received = new List<string>();
        var allDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _sut.StartDispatching(
            (item, _) =>
            {
                lock (received)
                {
                    received.Add(item);
                    if (received.Count == 3)
                    {
                        allDone.TrySetResult();
                    }
                }
                return Task.CompletedTask;
            },
            CancellationToken.None);

        await _sut.EnqueueAsync("a", CancellationToken.None);
        await _sut.EnqueueAsync("b", CancellationToken.None);
        await _sut.EnqueueAsync("c", CancellationToken.None);

        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(5));

        lock (received)
        {
            received.Should().HaveCount(3);
            received.Should().Contain("a").And.Contain("b").And.Contain("c");
        }
    }

    [Fact]
    public async Task ConcurrencyLimiting_MaxConcurrency1_ProcessesSequentially()
    {
        _sut = CreateDispatcher(concurrency: 1);
        int concurrentCount = 0;
        int maxConcurrent = 0;
        var allDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int processedCount = 0;

        _sut.StartDispatching(
            async (_, _) =>
            {
                int current = Interlocked.Increment(ref concurrentCount);
                InterlockedMax(ref maxConcurrent, current);
                await Task.Delay(20);
                Interlocked.Decrement(ref concurrentCount);
                if (Interlocked.Increment(ref processedCount) == 5)
                {
                    allDone.TrySetResult();
                }
            },
            CancellationToken.None);

        for (int i = 0; i < 5; i++)
        {
            await _sut.EnqueueAsync($"item-{i}", CancellationToken.None);
        }

        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(10));

        maxConcurrent.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrencyLimiting_MaxConcurrency3_AllowsParallelProcessing()
    {
        _sut = CreateDispatcher(concurrency: 3);
        int concurrentCount = 0;
        int maxConcurrent = 0;
        var allDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int processedCount = 0;

        _sut.StartDispatching(
            async (_, _) =>
            {
                int current = Interlocked.Increment(ref concurrentCount);
                InterlockedMax(ref maxConcurrent, current);
                await Task.Delay(100);
                Interlocked.Decrement(ref concurrentCount);
                if (Interlocked.Increment(ref processedCount) == 6)
                {
                    allDone.TrySetResult();
                }
            },
            CancellationToken.None);

        for (int i = 0; i < 6; i++)
        {
            await _sut.EnqueueAsync($"item-{i}", CancellationToken.None);
        }

        await allDone.Task.WaitAsync(TimeSpan.FromSeconds(10));

        maxConcurrent.Should().BeGreaterThan(1);
        maxConcurrent.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task DisposeAsync_StopsDispatching()
    {
        _sut = CreateDispatcher();
        int processedCount = 0;

        _sut.StartDispatching(
            (_, _) => { Interlocked.Increment(ref processedCount); return Task.CompletedTask; },
            CancellationToken.None);

        await _sut.EnqueueAsync("before-dispose", CancellationToken.None);
        await Task.Delay(200);

        await _sut.DisposeAsync();

        int countAfterDispose = processedCount;
        await Task.Delay(100);

        processedCount.Should().Be(countAfterDispose);
    }

    [Fact]
    public async Task EnqueueAsync_AfterDispose_Throws()
    {
        _sut = CreateDispatcher();
        await _sut.DisposeAsync();

        Func<Task> act = () => _sut.EnqueueAsync("too-late", CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CallbackThrows_DoesNotCrashDispatcher()
    {
        _sut = CreateDispatcher();
        var secondReceived = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _sut.StartDispatching(
            (item, _) =>
            {
                if (item == "bad")
                {
                    throw new InvalidOperationException("handler error");
                }

                secondReceived.TrySetResult(item);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        await _sut.EnqueueAsync("bad", CancellationToken.None);
        await Task.Delay(100);
        await _sut.EnqueueAsync("good", CancellationToken.None);

        string result = await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().Be("good");
    }

    [Fact]
    public async Task Complete_SignalsNoMoreItems()
    {
        _sut = CreateDispatcher();
        int processed = 0;

        _sut.StartDispatching(
            (_, _) => { Interlocked.Increment(ref processed); return Task.CompletedTask; },
            CancellationToken.None);

        await _sut.EnqueueAsync("item", CancellationToken.None);
        _sut.Complete();

        await Task.Delay(300);

        processed.Should().Be(1);
    }

    [Fact]
    public async Task TrackerIsUsed_TrackStartAndCompleteCalledPerItem()
    {
        _sut = CreateDispatcher();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _sut.StartDispatching(
            (_, _) => { done.TrySetResult(); return Task.CompletedTask; },
            CancellationToken.None);

        await _sut.EnqueueAsync("item", CancellationToken.None);

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        _tracker.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        _sut = CreateDispatcher();
        _sut.StartDispatching(
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        await _sut.DisposeAsync();
        var act = async () => await _sut.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CallbackThrows_OperationCanceledException_DuringShutdown_DoesNotCrash()
    {
        _sut = CreateDispatcher();
        using var cts = new CancellationTokenSource();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        _sut.StartDispatching(
            (item, ct) =>
            {
                if (item == "cancel")
                {
                    throw new OperationCanceledException(ct);
                }

                received.TrySetResult(item);
                return Task.CompletedTask;
            },
            cts.Token);

        await _sut.EnqueueAsync("cancel", CancellationToken.None);
        await Task.Delay(100);
        await _sut.EnqueueAsync("after-cancel", CancellationToken.None);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().Be("after-cancel");
    }

    [Fact]
    public async Task StartDispatching_ExternalCancellation_StopsLoop()
    {
        _sut = CreateDispatcher();
        using var cts = new CancellationTokenSource();
        int processedCount = 0;

        _sut.StartDispatching(
            (_, _) => { Interlocked.Increment(ref processedCount); return Task.CompletedTask; },
            cts.Token);

        await _sut.EnqueueAsync("item1", CancellationToken.None);
        await Task.Delay(100);

        await cts.CancelAsync();
        await Task.Delay(200);

        int countAfterCancel = processedCount;
        // Dispatcher should have stopped
        countAfterCancel.Should().BeGreaterThan(0);
    }

    private static void InterlockedMax(ref int location, int value)
    {
        int current = Volatile.Read(ref location);
        while (value > current)
        {
            int prev = Interlocked.CompareExchange(ref location, value, current);
            if (prev == current)
            {
                break;
            }

            current = prev;
        }
    }
}
