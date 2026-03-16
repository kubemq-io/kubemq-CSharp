using FluentAssertions;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Transport;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class ReconnectBufferTests : IDisposable
{
    private readonly ReconnectBuffer _sut;
    private readonly ReconnectOptions _options;

    public ReconnectBufferTests()
    {
        _options = new ReconnectOptions { BufferSize = 1024 };
        _sut = new ReconnectBuffer(_options);
    }

    public void Dispose() => _sut.Dispose();

    private static BufferedMessage Msg(int sizeBytes = 100, string channel = "ch") =>
        new(new byte[sizeBytes], channel, "event", sizeBytes);

    [Fact]
    public async Task EnqueueAsync_SingleMessage_CanBeReadByFlush()
    {
        var message = Msg(100);
        await _sut.EnqueueAsync(message, CancellationToken.None);

        var flushed = new List<BufferedMessage>();
        await _sut.FlushAsync(
            (msg, _) => { flushed.Add(msg); return Task.CompletedTask; },
            CancellationToken.None);

        flushed.Should().ContainSingle()
            .Which.Channel.Should().Be("ch");
    }

    [Fact]
    public async Task EnqueueAsync_MultipleMessages_FlushedInOrder()
    {
        var msg1 = new BufferedMessage(new byte[50], "ch1", "event", 50);
        var msg2 = new BufferedMessage(new byte[50], "ch2", "event", 50);
        var msg3 = new BufferedMessage(new byte[50], "ch3", "event", 50);

        await _sut.EnqueueAsync(msg1, CancellationToken.None);
        await _sut.EnqueueAsync(msg2, CancellationToken.None);
        await _sut.EnqueueAsync(msg3, CancellationToken.None);

        var flushed = new List<BufferedMessage>();
        await _sut.FlushAsync(
            (msg, _) => { flushed.Add(msg); return Task.CompletedTask; },
            CancellationToken.None);

        flushed.Select(m => m.Channel).Should().ContainInOrder("ch1", "ch2", "ch3");
    }

    [Fact]
    public async Task EnqueueAsync_ExceedsByteBudget_ThrowsKubeMQBufferFullException()
    {
        await _sut.EnqueueAsync(Msg(900), CancellationToken.None);

        Func<Task> act = () => _sut.EnqueueAsync(Msg(200), CancellationToken.None).AsTask();

        var ex = (await act.Should().ThrowAsync<KubeMQBufferFullException>()).Which;
        ex.BufferCapacityBytes.Should().Be(1024);
    }

    [Fact]
    public async Task EnqueueAsync_ExactlyAtBudget_Succeeds()
    {
        await _sut.EnqueueAsync(Msg(1024), CancellationToken.None);
    }

    [Fact]
    public async Task EnqueueAsync_OneByteOverBudget_Throws()
    {
        await _sut.EnqueueAsync(Msg(1024), CancellationToken.None);

        Func<Task> act = () => _sut.EnqueueAsync(Msg(1), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<KubeMQBufferFullException>();
    }

    [Fact]
    public async Task EnqueueAsync_CancellationRequested_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => _sut.EnqueueAsync(Msg(10), cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FlushAsync_EmptyBuffer_InvokesSendFuncZeroTimes()
    {
        int invocations = 0;

        await _sut.FlushAsync(
            (_, _) => { invocations++; return Task.CompletedTask; },
            CancellationToken.None);

        invocations.Should().Be(0);
    }

    [Fact]
    public async Task FlushAsync_AfterFlush_BufferIsEmpty()
    {
        await _sut.EnqueueAsync(Msg(100), CancellationToken.None);

        await _sut.FlushAsync(
            (_, _) => Task.CompletedTask, CancellationToken.None);

        var flushed = new List<BufferedMessage>();
        await _sut.FlushAsync(
            (msg, _) => { flushed.Add(msg); return Task.CompletedTask; },
            CancellationToken.None);

        flushed.Should().BeEmpty();
    }

    [Fact]
    public async Task FlushAsync_ReleasesBytes_AllowsNewEnqueue()
    {
        await _sut.EnqueueAsync(Msg(900), CancellationToken.None);
        await _sut.FlushAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        Func<Task> act = () => _sut.EnqueueAsync(Msg(900), CancellationToken.None).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void DiscardAll_EmptyBuffer_ReturnsZero()
    {
        int count = _sut.DiscardAll();

        count.Should().Be(0);
    }

    [Fact]
    public async Task DiscardAll_WithMessages_ReturnsCorrectCount()
    {
        await _sut.EnqueueAsync(Msg(50), CancellationToken.None);
        await _sut.EnqueueAsync(Msg(50), CancellationToken.None);
        await _sut.EnqueueAsync(Msg(50), CancellationToken.None);

        int count = _sut.DiscardAll();

        count.Should().Be(3);
    }

    [Fact]
    public async Task DiscardAll_ResetsBytes_AllowsNewEnqueue()
    {
        await _sut.EnqueueAsync(Msg(900), CancellationToken.None);

        _sut.DiscardAll();

        Func<Task> act = () => _sut.EnqueueAsync(Msg(900), CancellationToken.None).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dispose_PreventsSubsequentEnqueue()
    {
        _sut.Dispose();

        Func<Task> act = () => _sut.EnqueueAsync(Msg(10), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<Exception>();
    }
}
