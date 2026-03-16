using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Commands;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class CommandMessageTests
{
    [Fact]
    public void Construction_WithRequiredProperties_SetsCorrectly()
    {
        var message = new CommandMessage { Channel = "cmd-ch" };

        message.Channel.Should().Be("cmd-ch");
    }

    [Fact]
    public void TimeoutInSeconds_Property_SetsAndGetsCorrectly()
    {
        var message = new CommandMessage
        {
            Channel = "ch",
            TimeoutInSeconds = 30,
        };

        message.TimeoutInSeconds.Should().Be(30);
    }

    [Fact]
    public void TimeoutInSeconds_DefaultIsNull()
    {
        var message = new CommandMessage { Channel = "ch" };

        message.TimeoutInSeconds.Should().BeNull();
    }

    [Fact]
    public void Body_Property_SetsAndGetsCorrectly()
    {
        var body = Encoding.UTF8.GetBytes("cmd-payload");

        var message = new CommandMessage
        {
            Channel = "ch",
            Body = body,
        };

        message.Body.ToArray().Should().BeEquivalentTo(body);
    }

    [Fact]
    public void Tags_Property_SetsAndGetsCorrectly()
    {
        var tags = new Dictionary<string, string>
        {
            ["action"] = "create",
            ["source"] = "test",
        };

        var message = new CommandMessage
        {
            Channel = "ch",
            Tags = tags,
        };

        message.Tags.Should().HaveCount(2);
        message.Tags!["action"].Should().Be("create");
    }

    [Fact]
    public void DefaultBody_IsEmpty()
    {
        var message = new CommandMessage { Channel = "ch" };

        message.Body.Length.Should().Be(0);
    }

    [Fact]
    public void DefaultTags_IsNull()
    {
        var message = new CommandMessage { Channel = "ch" };

        message.Tags.Should().BeNull();
    }

    [Fact]
    public void ClientId_Property_SetsAndGetsCorrectly()
    {
        var message = new CommandMessage
        {
            Channel = "ch",
            ClientId = "cmd-client",
        };

        message.ClientId.Should().Be("cmd-client");
    }

    [Fact]
    public void DefaultClientId_IsNull()
    {
        var message = new CommandMessage { Channel = "ch" };

        message.ClientId.Should().BeNull();
    }
}
