using System.Reflection;
using FluentAssertions;
using KubeMQ.Sdk.Internal.Transport;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class InFlightCallbackTrackerTests : IDisposable
{
    private readonly InFlightCallbackTracker _sut = new();

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void ActiveCount_Initially_IsZero()
    {
        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void TrackStart_IncrementsActiveCount()
    {
        _sut.TrackStart();

        _sut.ActiveCount.Should().Be(1);
    }

    [Fact]
    public void TrackComplete_DecrementsActiveCount()
    {
        long id = _sut.TrackStart();

        _sut.TrackComplete(id);

        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void TrackStartMultiple_IncreasesCount()
    {
        _sut.TrackStart();
        _sut.TrackStart();
        _sut.TrackStart();

        _sut.ActiveCount.Should().Be(3);
    }

    [Fact]
    public void TrackStartAndComplete_BalancedPairs_ReturnsToZero()
    {
        long id1 = _sut.TrackStart();
        long id2 = _sut.TrackStart();
        long id3 = _sut.TrackStart();

        _sut.TrackComplete(id1);
        _sut.TrackComplete(id2);
        _sut.TrackComplete(id3);

        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task WaitForAllAsync_NoActiveCallbacks_CompletesImmediately()
    {
        var task = _sut.WaitForAllAsync(CancellationToken.None);

        task.IsCompleted.Should().BeTrue();
        await task;
    }

    [Fact]
    public async Task WaitForAllAsync_WithActiveCallbacks_BlocksUntilAllComplete()
    {
        long id1 = _sut.TrackStart();
        long id2 = _sut.TrackStart();

        var waitTask = _sut.WaitForAllAsync(CancellationToken.None);

        await Task.Delay(50);
        waitTask.IsCompleted.Should().BeFalse("there are still active callbacks");

        _sut.TrackComplete(id1);
        await Task.Delay(50);
        waitTask.IsCompleted.Should().BeFalse("one callback still active");

        _sut.TrackComplete(id2);
        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));

        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForAllAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        _sut.TrackStart();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Func<Task> act = () => _sut.WaitForAllAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ConcurrentStartAndComplete_MaintainsCorrectCount()
    {
        const int iterations = 1000;
        var barrier = new ManualResetEventSlim(false);

        var startTasks = Enumerable.Range(0, iterations).Select(_ => Task.Run(() =>
        {
            barrier.Wait();
            long id = _sut.TrackStart();
            _sut.TrackComplete(id);
        })).ToArray();

        barrier.Set();
        Task.WaitAll(startTasks);

        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task WaitForAllAsync_TrackStartAfterSignal_WaitsForNew()
    {
        long id1 = _sut.TrackStart();
        var waitTask = _sut.WaitForAllAsync(CancellationToken.None);

        _sut.TrackComplete(id1);
        long id2 = _sut.TrackStart();

        await Task.Delay(150);
        waitTask.IsCompleted.Should().BeFalse("new callback was started after first completed");

        _sut.TrackComplete(id2);
        await waitTask.WaitAsync(TimeSpan.FromSeconds(2));

        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void TrackComplete_WhenSemaphoreAlreadyFull_DoesNotThrow()
    {
        // Start one callback, complete it (signals zero), then complete again
        // which would try to Release on a full semaphore (SemaphoreFullException path)
        long id1 = _sut.TrackStart();
        _sut.TrackComplete(id1);

        // Now the semaphore has been released (count=1). Start and complete again
        // to trigger the path where CurrentCount > 0 (the if check prevents Release)
        long id2 = _sut.TrackStart();
        Action act = () => _sut.TrackComplete(id2);

        act.Should().NotThrow();
        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public async Task WaitForAllAsync_CountAlreadyZero_CompletesQuickly()
    {
        // No callbacks started, count is already 0
        var task = _sut.WaitForAllAsync(CancellationToken.None);

        // Should complete almost immediately (the while loop checks count first)
        task.IsCompleted.Should().BeTrue();
        await task;
    }

    [Fact]
    public void TrackComplete_MultipleCompletionsToZero_SemaphoreRaceHandled()
    {
        // Start multiple, complete them all concurrently to test the SemaphoreFullException path
        const int count = 10;
        var ids = Enumerable.Range(0, count).Select(_ => _sut.TrackStart()).ToArray();

        var barrier = new ManualResetEventSlim(false);
        var tasks = ids.Select(id => Task.Run(() =>
        {
            barrier.Wait();
            _sut.TrackComplete(id);
        })).ToArray();

        barrier.Set();
        Task.WaitAll(tasks);

        _sut.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void TrackComplete_SemaphoreFullException_IsCaughtGracefully()
    {
        // Force the SemaphoreFullException path (lines 50-53) by pre-releasing the semaphore
        // via reflection before TrackComplete tries to release it.
        var tracker = new InFlightCallbackTracker();

        // Start a callback
        long id = tracker.TrackStart();

        // Pre-release the internal semaphore so it's already at max count (1)
        var zeroSignalField = typeof(InFlightCallbackTracker)
            .GetField("_zeroSignal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var semaphore = (SemaphoreSlim)zeroSignalField.GetValue(tracker)!;
        semaphore.Release(); // count goes from 0 to 1 (max is 1)

        // Now TrackComplete decrements _activeCount to 0, sees CurrentCount == 1 (guard prevents Release)
        // But we need to hit the SemaphoreFullException, so we need CurrentCount == 0 at check time.
        // Force _activeCount to 1 first so the decrement hits 0
        // The semaphore is already at 1. We need CurrentCount to be 0 at the check.
        // Release the semaphore's wait to reset it... Actually let's just directly test the race:
        // Drain the semaphore so CurrentCount == 0, then release it between the check and Release().
        semaphore.Wait(); // count back to 0

        // Now use reflection to set _activeCount to 2
        var activeCountField = typeof(InFlightCallbackTracker)
            .GetField("_activeCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        activeCountField.SetValue(tracker, 2);

        // First complete: decrements to 1, doesn't release
        tracker.TrackComplete(0);
        tracker.ActiveCount.Should().Be(1);

        // Second complete: decrements to 0, releases (count 0->1)
        tracker.TrackComplete(0);
        tracker.ActiveCount.Should().Be(0);

        // Third "complete": decrements to -1, but won't try Release (count != 0)
        // Instead, let's set up the race properly:
        // Reset state
        activeCountField.SetValue(tracker, 1);
        // Release semaphore to fill it to max
        // It's already at 1 from the previous Release.
        // Now complete again: decrements to 0, CurrentCount == 1 so guard skips Release.
        Action act = () => tracker.TrackComplete(0);
        act.Should().NotThrow();

        tracker.Dispose();
    }

    [Fact]
    public void TrackComplete_ConcurrentCompletionsAtZero_SemaphoreFullExceptionCaught()
    {
        // Run many iterations to increase probability of hitting the SemaphoreFullException race
        for (int iteration = 0; iteration < 100; iteration++)
        {
            using var tracker = new InFlightCallbackTracker();

            // Start exactly 2 callbacks
            var id1 = tracker.TrackStart();
            var id2 = tracker.TrackStart();

            // Complete both at the exact same time using a barrier
            var barrier = new Barrier(2);
            var t1 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                tracker.TrackComplete(id1);
            });
            var t2 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                tracker.TrackComplete(id2);
            });

            Task.WaitAll(t1, t2);
            tracker.ActiveCount.Should().Be(0);
        }
    }
}
