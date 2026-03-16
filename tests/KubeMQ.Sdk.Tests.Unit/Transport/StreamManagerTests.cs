using FluentAssertions;
using KubeMQ.Sdk.Internal.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class StreamManagerTests
{
    private readonly StreamManager _sut = new(NullLogger.Instance);

    private static SubscriptionRecord MakeRecord(
        string channel,
        SubscriptionPattern pattern = SubscriptionPattern.Events,
        Func<object, CancellationToken, Task>? resubFunc = null,
        Func<long, object>? adjustFunc = null)
    {
        resubFunc ??= (_, _) => Task.CompletedTask;
        return new SubscriptionRecord(channel, pattern, new { Channel = channel }, resubFunc, adjustFunc);
    }

    [Fact]
    public async Task ResubscribeAllAsync_NoSubscriptions_CompletesWithoutError()
    {
        Func<Task> act = () => _sut.ResubscribeAllAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TrackAndResubscribe_SingleSubscription_InvokesResubscribeFunc()
    {
        int invocationCount = 0;

        _sut.TrackSubscription("sub-1", MakeRecord("events-channel",
            resubFunc: (_, _) => { invocationCount++; return Task.CompletedTask; }));

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        invocationCount.Should().Be(1);
    }

    [Fact]
    public async Task TrackAndResubscribe_MultipleSubscriptions_InvokesAll()
    {
        var invoked = new List<string>();

        _sut.TrackSubscription("sub-1", MakeRecord("ch1",
            resubFunc: (_, _) => { invoked.Add("sub-1"); return Task.CompletedTask; }));
        _sut.TrackSubscription("sub-2", MakeRecord("ch2",
            resubFunc: (_, _) => { invoked.Add("sub-2"); return Task.CompletedTask; }));
        _sut.TrackSubscription("sub-3", MakeRecord("ch3",
            resubFunc: (_, _) => { invoked.Add("sub-3"); return Task.CompletedTask; }));

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        invoked.Should().HaveCount(3);
        invoked.Should().Contain("sub-1").And.Contain("sub-2").And.Contain("sub-3");
    }

    [Fact]
    public async Task UntrackSubscription_RemovedSubscription_IsNotResubscribed()
    {
        int invocationCount = 0;

        _sut.TrackSubscription("sub-1", MakeRecord("ch1",
            resubFunc: (_, _) => { invocationCount++; return Task.CompletedTask; }));
        _sut.UntrackSubscription("sub-1");

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        invocationCount.Should().Be(0);
    }

    [Fact]
    public void UntrackSubscription_NonExistentId_DoesNotThrow()
    {
        Action act = () => _sut.UntrackSubscription("non-existent");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task TrackSubscription_SameId_OverwritesPrevious()
    {
        var invoked = new List<string>();

        _sut.TrackSubscription("sub-1", MakeRecord("ch-old",
            resubFunc: (_, _) => { invoked.Add("old"); return Task.CompletedTask; }));
        _sut.TrackSubscription("sub-1", MakeRecord("ch-new",
            resubFunc: (_, _) => { invoked.Add("new"); return Task.CompletedTask; }));

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        invoked.Should().ContainSingle().Which.Should().Be("new");
    }

    [Fact]
    public async Task ResubscribeAllAsync_EventsStorePattern_CallsAdjustForReconnect()
    {
        object? capturedParams = null;
        _sut.UpdateLastSequence("sub-store", 42);

        _sut.TrackSubscription("sub-store", new SubscriptionRecord(
            "store-ch",
            SubscriptionPattern.EventsStore,
            new { StartSequence = 0 },
            (p, _) => { capturedParams = p; return Task.CompletedTask; },
            AdjustFunc: seq => new { StartSequence = seq + 1 }));

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        capturedParams.Should().NotBeNull();
        capturedParams!.GetType().GetProperty("StartSequence")!.GetValue(capturedParams)
            .Should().Be(43L);
    }

    [Fact]
    public async Task ResubscribeAllAsync_EventsPattern_UsesOriginalParams()
    {
        var original = new { Channel = "ev-ch" };
        object? capturedParams = null;

        _sut.TrackSubscription("sub-events", new SubscriptionRecord(
            "ev-ch",
            SubscriptionPattern.Events,
            original,
            (p, _) => { capturedParams = p; return Task.CompletedTask; }));

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        capturedParams.Should().BeSameAs(original);
    }

    public static IEnumerable<object[]> NonStorePatterns()
    {
        yield return new object[] { SubscriptionPattern.Queue };
        yield return new object[] { SubscriptionPattern.Commands };
        yield return new object[] { SubscriptionPattern.Queries };
    }

    [Theory]
    [MemberData(nameof(NonStorePatterns))]
    internal async Task ResubscribeAllAsync_NonStorePatterns_UseOriginalParams(
        SubscriptionPattern pattern)
    {
        var original = new { Channel = "ch" };
        object? capturedParams = null;

        _sut.TrackSubscription("sub-x", new SubscriptionRecord(
            "ch",
            pattern,
            original,
            (p, _) => { capturedParams = p; return Task.CompletedTask; }));

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        capturedParams.Should().BeSameAs(original);
    }

    [Fact]
    public async Task ResubscribeAllAsync_WhenOneThrows_ContinuesWithOthers()
    {
        var invoked = new List<string>();

        _sut.TrackSubscription("sub-1", MakeRecord("ch1",
            resubFunc: (_, _) => { invoked.Add("sub-1"); return Task.CompletedTask; }));
        _sut.TrackSubscription("sub-2", MakeRecord("ch2",
            resubFunc: (_, _) => throw new InvalidOperationException("test")));
        _sut.TrackSubscription("sub-3", MakeRecord("ch3",
            resubFunc: (_, _) => { invoked.Add("sub-3"); return Task.CompletedTask; }));

        Func<Task> act = () => _sut.ResubscribeAllAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        invoked.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResubscribeAllAsync_EventsStoreWithNoLastSequence_UsesZero()
    {
        object? capturedParams = null;

        _sut.TrackSubscription("sub-store", new SubscriptionRecord(
            "store-ch",
            SubscriptionPattern.EventsStore,
            new { StartSequence = 0 },
            (p, _) => { capturedParams = p; return Task.CompletedTask; },
            AdjustFunc: seq => new { ResumeFrom = seq }));

        await _sut.ResubscribeAllAsync(CancellationToken.None);

        capturedParams.Should().NotBeNull();
        capturedParams!.GetType().GetProperty("ResumeFrom")!.GetValue(capturedParams)
            .Should().Be(0L);
    }
}
