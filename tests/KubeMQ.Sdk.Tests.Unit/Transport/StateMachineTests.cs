using FluentAssertions;
using KubeMQ.Sdk.Common;
using KubeMQ.Sdk.Internal.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class StateMachineTests : IDisposable
{
    private readonly StateMachine _sut = new(NullLogger.Instance);

    public void Dispose() => _sut.Dispose();

    [Fact]
    public void Current_InitialState_IsDisconnected()
    {
        _sut.Current.Should().Be(ConnectionState.Disconnected);
    }

    [Theory]
    [InlineData(ConnectionState.Disconnected, ConnectionState.Connecting)]
    [InlineData(ConnectionState.Connecting, ConnectionState.Connected)]
    [InlineData(ConnectionState.Connected, ConnectionState.Reconnecting)]
    [InlineData(ConnectionState.Reconnecting, ConnectionState.Connected)]
    [InlineData(ConnectionState.Connected, ConnectionState.Disposed)]
    [InlineData(ConnectionState.Reconnecting, ConnectionState.Disposed)]
    [InlineData(ConnectionState.Disconnected, ConnectionState.Disposed)]
    public void TryTransition_ValidTransition_ReturnsTrueAndUpdatesState(
        ConnectionState from, ConnectionState to)
    {
        SetState(from);

        bool result = _sut.TryTransition(from, to);

        result.Should().BeTrue();
        _sut.Current.Should().Be(to);
    }

    [Fact]
    public void TryTransition_WrongCurrentState_ReturnsFalseAndStateUnchanged()
    {
        _sut.Current.Should().Be(ConnectionState.Disconnected);

        bool result = _sut.TryTransition(ConnectionState.Connected, ConnectionState.Reconnecting);

        result.Should().BeFalse();
        _sut.Current.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task TransitionAsync_ValidTransition_ReturnsTrueAndUpdatesState()
    {
        bool result = await _sut.TransitionAsync(
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            onTransition: null,
            CancellationToken.None);

        result.Should().BeTrue();
        _sut.Current.Should().Be(ConnectionState.Connecting);
    }

    [Fact]
    public async Task TransitionAsync_WrongCurrentState_ReturnsFalse()
    {
        bool result = await _sut.TransitionAsync(
            ConnectionState.Connected,
            ConnectionState.Reconnecting,
            onTransition: null,
            CancellationToken.None);

        result.Should().BeFalse();
        _sut.Current.Should().Be(ConnectionState.Disconnected);
    }

    [Fact]
    public async Task TransitionAsync_InvokesCallbackBeforeStateChange()
    {
        ConnectionState capturedDuringCallback = ConnectionState.Disposed;

        bool result = await _sut.TransitionAsync(
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            onTransition: () =>
            {
                capturedDuringCallback = _sut.Current;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        result.Should().BeTrue();
        capturedDuringCallback.Should().Be(ConnectionState.Disconnected,
            "callback runs before state is updated");
        _sut.Current.Should().Be(ConnectionState.Connecting);
    }

    [Fact]
    public async Task TransitionAsync_NullCallback_SucceedsWithoutError()
    {
        bool result = await _sut.TransitionAsync(
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            onTransition: null,
            CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => _sut.TransitionAsync(
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            onTransition: null,
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TransitionAsync_CallbackThrows_PropagatesException()
    {
        Func<Task> act = () => _sut.TransitionAsync(
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            onTransition: () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
    }

    [Theory]
    [InlineData(ConnectionState.Disconnected)]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Connected)]
    [InlineData(ConnectionState.Reconnecting)]
    [InlineData(ConnectionState.Disposed)]
    public void ForceDisposed_FromAnyState_TransitionsToDisposedAndReturnsPrevious(
        ConnectionState initial)
    {
        SetState(initial);

        ConnectionState previous = _sut.ForceDisposed();

        previous.Should().Be(initial);
        _sut.Current.Should().Be(ConnectionState.Disposed);
    }

    [Fact]
    public async Task TransitionAsync_ConcurrentTransitions_OnlyOneSucceeds()
    {
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task1 = _sut.TransitionAsync(
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            onTransition: async () => await barrier.Task,
            CancellationToken.None);

        await Task.Delay(50);

        var task2 = Task.Run(() => _sut.TransitionAsync(
            ConnectionState.Disconnected,
            ConnectionState.Connected,
            onTransition: null,
            CancellationToken.None));

        await Task.Delay(50);

        barrier.SetResult();

        bool result1 = await task1;
        bool result2 = await task2;

        result1.Should().BeTrue();
        result2.Should().BeFalse("state already changed to Connecting before task2 acquires lock");
        _sut.Current.Should().Be(ConnectionState.Connecting);
    }

    [Fact]
    public void TryTransition_ConcurrentCASFromSameState_OnlyOneSucceeds()
    {
        int successCount = 0;
        var barrier = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            barrier.Wait();
            if (_sut.TryTransition(ConnectionState.Disconnected, ConnectionState.Connecting))
            {
                Interlocked.Increment(ref successCount);
            }
        })).ToArray();

        barrier.Set();
        Task.WaitAll(tasks);

        successCount.Should().Be(1);
        _sut.Current.Should().Be(ConnectionState.Connecting);
    }

    private void SetState(ConnectionState target)
    {
        if (target == ConnectionState.Disconnected)
        {
            return;
        }

        ConnectionState[] path = target switch
        {
            ConnectionState.Connecting => [ConnectionState.Connecting],
            ConnectionState.Connected =>
                [ConnectionState.Connecting, ConnectionState.Connected],
            ConnectionState.Reconnecting =>
                [ConnectionState.Connecting, ConnectionState.Connected, ConnectionState.Reconnecting],
            ConnectionState.Disposed => [ConnectionState.Disposed],
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

        ConnectionState current = ConnectionState.Disconnected;
        foreach (ConnectionState next in path)
        {
            if (next == ConnectionState.Disposed)
            {
                _sut.ForceDisposed();
                return;
            }

            _sut.TryTransition(current, next).Should().BeTrue();
            current = next;
        }
    }
}
