using System.Collections.Generic;
using FluentAssertions;
using KubeMQ.Sdk.Commands;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class CommandReceivedTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsValues()
    {
        var body = new byte[] { 1, 2, 3 };
        var tags = new Dictionary<string, string> { ["key"] = "value" };

        var cmd = new CommandReceived
        {
            Channel = "commands.test",
            RequestId = "req-001",
            Body = body,
            Tags = tags,
            ReplyChannel = "reply.test",
        };

        cmd.Channel.Should().Be("commands.test");
        cmd.RequestId.Should().Be("req-001");
        cmd.Body.ToArray().Should().Equal(1, 2, 3);
        cmd.Tags.Should().ContainKey("key").WhoseValue.Should().Be("value");
        cmd.ReplyChannel.Should().Be("reply.test");
    }

    [Fact]
    public void Construction_WithRequiredPropertiesOnly_HasDefaults()
    {
        var cmd = new CommandReceived
        {
            Channel = "ch",
            RequestId = "req",
        };

        cmd.Body.Length.Should().Be(0);
        cmd.Tags.Should().BeNull();
        cmd.ReplyChannel.Should().BeNull();
    }

    [Fact]
    public void Tags_CanBeNull()
    {
        var cmd = new CommandReceived
        {
            Channel = "ch",
            RequestId = "req",
            Tags = null,
        };

        cmd.Tags.Should().BeNull();
    }

    [Fact]
    public void Body_IsReadOnlyMemory()
    {
        var data = new byte[] { 10, 20, 30 };
        var cmd = new CommandReceived
        {
            Channel = "ch",
            RequestId = "req",
            Body = data,
        };

        cmd.Body.Span[0].Should().Be(10);
        cmd.Body.Span[2].Should().Be(30);
    }

    [Fact]
    public void RecordEquality_WorksCorrectly()
    {
        var a = new CommandReceived { Channel = "ch", RequestId = "req" };
        var b = new CommandReceived { Channel = "ch", RequestId = "req" };

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new CommandReceived { Channel = "ch1", RequestId = "req" };
        var b = new CommandReceived { Channel = "ch2", RequestId = "req" };

        a.Should().NotBe(b);
    }
}
