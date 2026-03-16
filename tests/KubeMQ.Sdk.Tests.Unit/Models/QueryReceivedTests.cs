using System.Collections.Generic;
using FluentAssertions;
using KubeMQ.Sdk.Queries;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class QueryReceivedTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsValues()
    {
        var body = new byte[] { 10, 11 };
        var tags = new Dictionary<string, string> { ["k"] = "v" };

        var query = new QueryReceived
        {
            Channel = "queries.test",
            RequestId = "q-001",
            Body = body,
            Tags = tags,
            ReplyChannel = "reply.q",
            CacheKey = "cache-key-1",
        };

        query.Channel.Should().Be("queries.test");
        query.RequestId.Should().Be("q-001");
        query.Body.ToArray().Should().Equal(10, 11);
        query.Tags.Should().ContainKey("k");
        query.ReplyChannel.Should().Be("reply.q");
        query.CacheKey.Should().Be("cache-key-1");
    }

    [Fact]
    public void Construction_WithRequiredOnly_HasDefaults()
    {
        var query = new QueryReceived
        {
            Channel = "ch",
            RequestId = "req",
        };

        query.Body.Length.Should().Be(0);
        query.Tags.Should().BeNull();
        query.ReplyChannel.Should().BeNull();
        query.CacheKey.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new QueryReceived { Channel = "ch", RequestId = "r1" };
        var b = new QueryReceived { Channel = "ch", RequestId = "r1" };

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentRequestId_AreNotEqual()
    {
        var a = new QueryReceived { Channel = "ch", RequestId = "r1" };
        var b = new QueryReceived { Channel = "ch", RequestId = "r2" };

        a.Should().NotBe(b);
    }
}
