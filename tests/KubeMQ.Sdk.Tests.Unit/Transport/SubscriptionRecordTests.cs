using FluentAssertions;
using KubeMQ.Sdk.Internal.Transport;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class SubscriptionRecordTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var original = new { Channel = "ch" };
        Func<object, CancellationToken, Task> resubFunc = (_, _) => Task.CompletedTask;

        var record = new SubscriptionRecord(
            "my-channel", SubscriptionPattern.Events, original, resubFunc);

        record.Channel.Should().Be("my-channel");
        record.Pattern.Should().Be(SubscriptionPattern.Events);
        record.OriginalParams.Should().BeSameAs(original);
        record.ResubscribeFunc.Should().BeSameAs(resubFunc);
        record.AdjustFunc.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAdjustFunc_SetsProperty()
    {
        Func<long, object> adjustFunc = seq => new { Seq = seq };

        var record = new SubscriptionRecord(
            "ch", SubscriptionPattern.EventsStore, new object(),
            (_, _) => Task.CompletedTask, adjustFunc);

        record.AdjustFunc.Should().BeSameAs(adjustFunc);
    }

    [Fact]
    public void AdjustForReconnect_WithAdjustFunc_ReturnsAdjustedParams()
    {
        var record = new SubscriptionRecord(
            "ch", SubscriptionPattern.EventsStore,
            new { StartSequence = 0 },
            (_, _) => Task.CompletedTask,
            AdjustFunc: seq => new { StartSequence = seq + 1 });

        object result = record.AdjustForReconnect(99);

        result.GetType().GetProperty("StartSequence")!.GetValue(result)
            .Should().Be(100L);
    }

    [Fact]
    public void AdjustForReconnect_WithoutAdjustFunc_ReturnsOriginalParams()
    {
        var original = new { Channel = "ch" };
        var record = new SubscriptionRecord(
            "ch", SubscriptionPattern.Events, original, (_, _) => Task.CompletedTask);

        object result = record.AdjustForReconnect(42);

        result.Should().BeSameAs(original);
    }

    [Fact]
    public void AdjustForReconnect_WithAdjustFunc_ZeroSequence()
    {
        var record = new SubscriptionRecord(
            "ch", SubscriptionPattern.EventsStore,
            new { StartSequence = 0 },
            (_, _) => Task.CompletedTask,
            AdjustFunc: seq => new { ResumeFrom = seq });

        object result = record.AdjustForReconnect(0);

        result.GetType().GetProperty("ResumeFrom")!.GetValue(result)
            .Should().Be(0L);
    }

    [Fact]
    public void AdjustForReconnect_WithAdjustFunc_LargeSequence()
    {
        var record = new SubscriptionRecord(
            "ch", SubscriptionPattern.EventsStore,
            new { StartSequence = 0 },
            (_, _) => Task.CompletedTask,
            AdjustFunc: seq => new { ResumeFrom = seq });

        object result = record.AdjustForReconnect(long.MaxValue);

        result.GetType().GetProperty("ResumeFrom")!.GetValue(result)
            .Should().Be(long.MaxValue);
    }

    public static IEnumerable<object[]> AllPatterns()
    {
        yield return new object[] { SubscriptionPattern.Events };
        yield return new object[] { SubscriptionPattern.EventsStore };
        yield return new object[] { SubscriptionPattern.Queue };
        yield return new object[] { SubscriptionPattern.Commands };
        yield return new object[] { SubscriptionPattern.Queries };
    }

    [Theory]
    [MemberData(nameof(AllPatterns))]
    internal void Pattern_AllValues_CanBeAssigned(SubscriptionPattern pattern)
    {
        var record = new SubscriptionRecord(
            "ch", pattern, new object(), (_, _) => Task.CompletedTask);

        record.Pattern.Should().Be(pattern);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        Func<object, CancellationToken, Task> resubFunc = (_, _) => Task.CompletedTask;
        var original = new object();

        var r1 = new SubscriptionRecord("ch", SubscriptionPattern.Events, original, resubFunc);
        var r2 = new SubscriptionRecord("ch", SubscriptionPattern.Events, original, resubFunc);

        r1.Should().Be(r2);
    }

    [Fact]
    public void RecordEquality_DifferentChannel_AreNotEqual()
    {
        Func<object, CancellationToken, Task> resubFunc = (_, _) => Task.CompletedTask;
        var original = new object();

        var r1 = new SubscriptionRecord("ch1", SubscriptionPattern.Events, original, resubFunc);
        var r2 = new SubscriptionRecord("ch2", SubscriptionPattern.Events, original, resubFunc);

        r1.Should().NotBe(r2);
    }

    [Fact]
    public async Task ResubscribeFunc_IsInvokable()
    {
        int invoked = 0;
        var record = new SubscriptionRecord(
            "ch", SubscriptionPattern.Events, "params",
            (p, _) => { invoked++; return Task.CompletedTask; });

        await record.ResubscribeFunc("params", CancellationToken.None);

        invoked.Should().Be(1);
    }
}
