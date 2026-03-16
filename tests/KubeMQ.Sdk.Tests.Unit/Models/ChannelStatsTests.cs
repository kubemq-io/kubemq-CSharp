using FluentAssertions;
using KubeMQ.Sdk.Common;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class ChannelStatsTests
{
    [Fact]
    public void DefaultConstruction_AllPropertiesAreZero()
    {
        var stats = new ChannelStats();

        stats.Messages.Should().Be(0);
        stats.Volume.Should().Be(0);
        stats.Waiting.Should().Be(0);
        stats.Expired.Should().Be(0);
        stats.Delayed.Should().Be(0);
    }

    [Fact]
    public void Construction_WithInitSetters_SetsValues()
    {
        var stats = new ChannelStats
        {
            Messages = 1000,
            Volume = 50000,
            Waiting = 5,
            Expired = 2,
            Delayed = 3,
        };

        stats.Messages.Should().Be(1000);
        stats.Volume.Should().Be(50000);
        stats.Waiting.Should().Be(5);
        stats.Expired.Should().Be(2);
        stats.Delayed.Should().Be(3);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new ChannelStats { Messages = 10, Volume = 100 };
        var b = new ChannelStats { Messages = 10, Volume = 100 };

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new ChannelStats { Messages = 10 };
        var b = new ChannelStats { Messages = 20 };

        a.Should().NotBe(b);
    }
}
