using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Queries;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class QueryMessageTests
{
    [Fact]
    public void Construction_WithRequiredProperties_SetsCorrectly()
    {
        var message = new QueryMessage { Channel = "query-ch" };

        message.Channel.Should().Be("query-ch");
    }

    [Fact]
    public void TimeoutInSeconds_Property_SetsAndGetsCorrectly()
    {
        var message = new QueryMessage
        {
            Channel = "ch",
            TimeoutInSeconds = 60,
        };

        message.TimeoutInSeconds.Should().Be(60);
    }

    [Fact]
    public void TimeoutInSeconds_DefaultIsNull()
    {
        var message = new QueryMessage { Channel = "ch" };

        message.TimeoutInSeconds.Should().BeNull();
    }

    [Fact]
    public void CacheKey_Property_SetsAndGetsCorrectly()
    {
        var message = new QueryMessage
        {
            Channel = "ch",
            CacheKey = "user:123",
        };

        message.CacheKey.Should().Be("user:123");
    }

    [Fact]
    public void CacheKey_DefaultIsNull()
    {
        var message = new QueryMessage { Channel = "ch" };

        message.CacheKey.Should().BeNull();
    }

    [Fact]
    public void CacheTtlSeconds_Property_SetsAndGetsCorrectly()
    {
        var message = new QueryMessage
        {
            Channel = "ch",
            CacheTtlSeconds = 300,
        };

        message.CacheTtlSeconds.Should().Be(300);
    }

    [Fact]
    public void CacheTtlSeconds_DefaultIsNull()
    {
        var message = new QueryMessage { Channel = "ch" };

        message.CacheTtlSeconds.Should().BeNull();
    }

    [Fact]
    public void Body_Property_SetsAndGetsCorrectly()
    {
        var body = Encoding.UTF8.GetBytes("query-payload");

        var message = new QueryMessage
        {
            Channel = "ch",
            Body = body,
        };

        message.Body.ToArray().Should().BeEquivalentTo(body);
    }

    [Fact]
    public void Tags_Property_SetsAndGetsCorrectly()
    {
        var tags = new Dictionary<string, string> { ["type"] = "lookup" };

        var message = new QueryMessage
        {
            Channel = "ch",
            Tags = tags,
        };

        message.Tags.Should().ContainKey("type").WhoseValue.Should().Be("lookup");
    }

    [Fact]
    public void DefaultBody_IsEmpty()
    {
        var message = new QueryMessage { Channel = "ch" };

        message.Body.Length.Should().Be(0);
    }

    [Fact]
    public void DefaultTags_IsNull()
    {
        var message = new QueryMessage { Channel = "ch" };

        message.Tags.Should().BeNull();
    }

    [Fact]
    public void ClientId_Property_SetsAndGetsCorrectly()
    {
        var message = new QueryMessage
        {
            Channel = "ch",
            ClientId = "query-client",
        };

        message.ClientId.Should().Be("query-client");
    }

    [Fact]
    public void Construction_WithAllProperties_SetsCorrectly()
    {
        var body = Encoding.UTF8.GetBytes("full");
        var tags = new Dictionary<string, string> { ["k"] = "v" };

        var message = new QueryMessage
        {
            Channel = "full-ch",
            Body = body,
            Tags = tags,
            ClientId = "full-client",
            TimeoutInSeconds = 45,
            CacheKey = "cache-key",
            CacheTtlSeconds = 120,
        };

        message.Channel.Should().Be("full-ch");
        message.Body.ToArray().Should().BeEquivalentTo(body);
        message.Tags!["k"].Should().Be("v");
        message.ClientId.Should().Be("full-client");
        message.TimeoutInSeconds.Should().Be(45);
        message.CacheKey.Should().Be("cache-key");
        message.CacheTtlSeconds.Should().Be(120);
    }
}
