using FluentAssertions;
using KubeMQ.Sdk.Common;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class ChannelInfoTests
{
    [Fact]
    public void Construction_WithRequiredProperties_SetsValues()
    {
        var info = new ChannelInfo
        {
            Name = "orders",
            Type = "events",
        };

        info.Name.Should().Be("orders");
        info.Type.Should().Be("events");
    }

    [Fact]
    public void Construction_WithAllProperties_SetsValues()
    {
        var incoming = new ChannelStats { Messages = 100, Volume = 5000 };
        var outgoing = new ChannelStats { Messages = 90, Volume = 4500 };

        var info = new ChannelInfo
        {
            Name = "orders",
            Type = "events_store",
            LastActivity = 1700000000000L,
            IsActive = true,
            Incoming = incoming,
            Outgoing = outgoing,
        };

        info.Name.Should().Be("orders");
        info.Type.Should().Be("events_store");
        info.LastActivity.Should().Be(1700000000000L);
        info.IsActive.Should().BeTrue();
        info.Incoming.Should().BeSameAs(incoming);
        info.Outgoing.Should().BeSameAs(outgoing);
    }

    [Fact]
    public void OptionalStats_DefaultToNull()
    {
        var info = new ChannelInfo
        {
            Name = "ch",
            Type = "queues",
        };

        info.Incoming.Should().BeNull();
        info.Outgoing.Should().BeNull();
    }

    [Fact]
    public void DefaultValues_AreZeroAndFalse()
    {
        var info = new ChannelInfo
        {
            Name = "ch",
            Type = "events",
        };

        info.LastActivity.Should().Be(0);
        info.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new ChannelInfo { Name = "ch", Type = "events" };
        var b = new ChannelInfo { Name = "ch", Type = "events" };

        a.Should().Be(b);
    }
}
