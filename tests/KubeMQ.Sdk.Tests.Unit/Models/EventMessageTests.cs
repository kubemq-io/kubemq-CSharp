using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Events;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class EventMessageTests
{
    [Fact]
    public void Construction_WithRequiredChannel_SetsChannel()
    {
        var message = new EventMessage { Channel = "event-ch" };

        message.Channel.Should().Be("event-ch");
    }

    [Fact]
    public void Body_Property_SetsAndGetsCorrectly()
    {
        var body = Encoding.UTF8.GetBytes("event-payload");

        var message = new EventMessage
        {
            Channel = "ch",
            Body = body,
        };

        message.Body.ToArray().Should().BeEquivalentTo(body);
    }

    [Fact]
    public void Tags_Property_SetsAndGetsCorrectly()
    {
        var tags = new Dictionary<string, string> { ["key"] = "val" };

        var message = new EventMessage
        {
            Channel = "ch",
            Tags = tags,
        };

        message.Tags.Should().NotBeNull();
        message.Tags!["key"].Should().Be("val");
    }

    [Fact]
    public void DefaultBody_IsEmpty()
    {
        var message = new EventMessage { Channel = "ch" };

        message.Body.Length.Should().Be(0);
    }

    [Fact]
    public void DefaultTags_IsNull()
    {
        var message = new EventMessage { Channel = "ch" };

        message.Tags.Should().BeNull();
    }

    [Fact]
    public void DefaultClientId_IsNull()
    {
        var message = new EventMessage { Channel = "ch" };

        message.ClientId.Should().BeNull();
    }

    [Fact]
    public void ClientId_Property_SetsAndGetsCorrectly()
    {
        var message = new EventMessage
        {
            Channel = "ch",
            ClientId = "custom-client",
        };

        message.ClientId.Should().Be("custom-client");
    }

    [Fact]
    public void RecordEquality_SameChannel_AreEqual()
    {
        var msg1 = new EventMessage { Channel = "ch" };
        var msg2 = new EventMessage { Channel = "ch" };

        msg1.Should().Be(msg2);
    }

    [Fact]
    public void RecordEquality_DifferentChannel_AreNotEqual()
    {
        var msg1 = new EventMessage { Channel = "ch1" };
        var msg2 = new EventMessage { Channel = "ch2" };

        msg1.Should().NotBe(msg2);
    }
}
