using System.Collections.Generic;
using FluentAssertions;
using KubeMQ.Sdk.Events;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class EventReceivedTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsValues()
    {
        var body = new byte[] { 4, 5, 6 };
        var tags = new Dictionary<string, string> { ["env"] = "test" };
        var ts = DateTimeOffset.UtcNow;

        var evt = new EventReceived
        {
            Channel = "events.test",
            Body = body,
            Tags = tags,
            ClientId = "publisher-1",
            Timestamp = ts,
        };

        evt.Channel.Should().Be("events.test");
        evt.Body.ToArray().Should().Equal(4, 5, 6);
        evt.Tags.Should().ContainKey("env");
        evt.ClientId.Should().Be("publisher-1");
        evt.Timestamp.Should().Be(ts);
    }

    [Fact]
    public void Construction_WithRequiredOnly_HasDefaults()
    {
        var evt = new EventReceived { Channel = "ch" };

        evt.Body.Length.Should().Be(0);
        evt.Tags.Should().BeNull();
        evt.ClientId.Should().BeNull();
        evt.Timestamp.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void Tags_CanBeNull()
    {
        var evt = new EventReceived { Channel = "ch", Tags = null };

        evt.Tags.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = new EventReceived { Channel = "ch", Timestamp = ts };
        var b = new EventReceived { Channel = "ch", Timestamp = ts };

        a.Should().Be(b);
    }
}
