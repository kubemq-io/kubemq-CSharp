using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class QueueMessageTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsCorrectly()
    {
        var body = Encoding.UTF8.GetBytes("test-body");
        var tags = new Dictionary<string, string> { ["key"] = "value" };

        var message = new QueueMessage
        {
            Channel = "queue-ch",
            Body = body,
            Tags = tags,
            ClientId = "client-1",
            DelaySeconds = 30,
            ExpirationSeconds = 600,
            MaxReceiveCount = 5,
            MaxReceiveQueue = "dlq",
        };

        message.Channel.Should().Be("queue-ch");
        message.Body.ToArray().Should().BeEquivalentTo(body);
        message.Tags.Should().ContainKey("key").WhoseValue.Should().Be("value");
        message.ClientId.Should().Be("client-1");
        message.DelaySeconds.Should().Be(30);
        message.ExpirationSeconds.Should().Be(600);
        message.MaxReceiveCount.Should().Be(5);
        message.MaxReceiveQueue.Should().Be("dlq");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var message = new QueueMessage { Channel = "ch" };

        message.Body.Length.Should().Be(0);
        message.Tags.Should().BeNull();
        message.ClientId.Should().BeNull();
        message.DelaySeconds.Should().BeNull();
        message.ExpirationSeconds.Should().BeNull();
        message.MaxReceiveCount.Should().BeNull();
        message.MaxReceiveQueue.Should().BeNull();
    }

    [Fact]
    public void Channel_Property_ReturnsSetValue()
    {
        var message = new QueueMessage { Channel = "my-queue" };

        message.Channel.Should().Be("my-queue");
    }

    [Fact]
    public void Body_Property_WithReadOnlyMemory_WorksCorrectly()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        ReadOnlyMemory<byte> rom = data;

        var message = new QueueMessage
        {
            Channel = "ch",
            Body = rom,
        };

        message.Body.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public void Tags_Property_ReturnsSetDictionary()
    {
        var tags = new Dictionary<string, string>
        {
            ["env"] = "production",
            ["version"] = "2.0",
        };

        var message = new QueueMessage
        {
            Channel = "ch",
            Tags = tags,
        };

        message.Tags.Should().HaveCount(2);
        message.Tags!["env"].Should().Be("production");
        message.Tags["version"].Should().Be("2.0");
    }

    [Fact]
    public void EmptyBody_HasZeroLength()
    {
        var message = new QueueMessage
        {
            Channel = "ch",
            Body = ReadOnlyMemory<byte>.Empty,
        };

        message.Body.Length.Should().Be(0);
        message.Body.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void NullTags_IsNull()
    {
        var message = new QueueMessage
        {
            Channel = "ch",
            Tags = null,
        };

        message.Tags.Should().BeNull();
    }
}
