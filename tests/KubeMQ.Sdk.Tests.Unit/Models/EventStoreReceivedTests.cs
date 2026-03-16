using System.Collections.Generic;
using FluentAssertions;
using KubeMQ.Sdk.EventsStore;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class EventStoreReceivedTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsValues()
    {
        var body = new byte[] { 7, 8, 9 };
        var tags = new Dictionary<string, string> { ["trace"] = "abc" };
        var ts = DateTimeOffset.Parse("2026-01-15T12:00:00Z");

        var evt = new EventStoreReceived
        {
            Channel = "store.test",
            Body = body,
            Tags = tags,
            ClientId = "pub-2",
            Sequence = 42,
            Timestamp = ts,
        };

        evt.Channel.Should().Be("store.test");
        evt.Body.ToArray().Should().Equal(7, 8, 9);
        evt.Tags.Should().ContainKey("trace");
        evt.ClientId.Should().Be("pub-2");
        evt.Sequence.Should().Be(42);
        evt.Timestamp.Should().Be(ts);
    }

    [Fact]
    public void Construction_WithRequiredOnly_HasDefaults()
    {
        var evt = new EventStoreReceived { Channel = "ch" };

        evt.Body.Length.Should().Be(0);
        evt.Tags.Should().BeNull();
        evt.ClientId.Should().BeNull();
        evt.Sequence.Should().Be(0);
        evt.Timestamp.Should().Be(default(DateTimeOffset));
    }

    [Fact]
    public void Sequence_CanBeLargeValue()
    {
        var evt = new EventStoreReceived
        {
            Channel = "ch",
            Sequence = long.MaxValue,
        };

        evt.Sequence.Should().Be(long.MaxValue);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new EventStoreReceived { Channel = "ch", Sequence = 5 };
        var b = new EventStoreReceived { Channel = "ch", Sequence = 5 };

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentSequence_AreNotEqual()
    {
        var a = new EventStoreReceived { Channel = "ch", Sequence = 1 };
        var b = new EventStoreReceived { Channel = "ch", Sequence = 2 };

        a.Should().NotBe(b);
    }
}
