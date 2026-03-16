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
}
