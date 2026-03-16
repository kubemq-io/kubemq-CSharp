using System.Text;
using FluentAssertions;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Protocol;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Tests.Unit.Validation;

public class MessageValidatorTests
{
    [Fact]
    public void ValidateEventMessage_ValidMessage_DoesNotThrow()
    {
        var message = new EventMessage
        {
            Channel = "test-channel",
            Body = Encoding.UTF8.GetBytes("hello"),
        };

        var act = () => MessageValidator.ValidateEventMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEventMessage_NullMessage_ThrowsArgumentNull()
    {
        var act = () => MessageValidator.ValidateEventMessage(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateEventMessage_EmptyChannel_ThrowsConfiguration(string? channel)
    {
        var message = new EventMessage { Channel = channel! };

        var act = () => MessageValidator.ValidateEventMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Channel*");
    }

    [Fact]
    public void ValidateEventMessage_EmptyBody_DoesNotThrow()
    {
        var message = new EventMessage
        {
            Channel = "ch",
            Body = ReadOnlyMemory<byte>.Empty,
        };

        var act = () => MessageValidator.ValidateEventMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEventMessage_NullTags_DoesNotThrow()
    {
        var message = new EventMessage
        {
            Channel = "ch",
            Tags = null,
        };

        var act = () => MessageValidator.ValidateEventMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEventStoreMessage_NullMessage_ThrowsArgumentNull()
    {
        var act = () => MessageValidator.ValidateEventStoreMessage(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateEventStoreMessage_EmptyChannel_ThrowsConfiguration()
    {
        var message = new EventStoreMessage { Channel = "" };

        var act = () => MessageValidator.ValidateEventStoreMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Channel*");
    }

    [Fact]
    public void ValidateQueueMessage_ValidMessage_DoesNotThrow()
    {
        var message = new QueueMessage { Channel = "q-ch" };

        var act = () => MessageValidator.ValidateQueueMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateQueueMessage_NullMessage_ThrowsArgumentNull()
    {
        var act = () => MessageValidator.ValidateQueueMessage(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateQueueMessage_EmptyChannel_ThrowsConfiguration()
    {
        var message = new QueueMessage { Channel = "" };

        var act = () => MessageValidator.ValidateQueueMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Channel*");
    }

    [Fact]
    public void ValidateQueueMessage_NegativeDelaySeconds_ThrowsConfiguration()
    {
        var message = new QueueMessage
        {
            Channel = "q-ch",
            DelaySeconds = -1,
        };

        var act = () => MessageValidator.ValidateQueueMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*DelaySeconds*");
    }

    [Fact]
    public void ValidateQueueMessage_NegativeExpirationSeconds_ThrowsConfiguration()
    {
        var message = new QueueMessage
        {
            Channel = "q-ch",
            ExpirationSeconds = -1,
        };

        var act = () => MessageValidator.ValidateQueueMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*ExpirationSeconds*");
    }

    [Fact]
    public void ValidateQueueMessage_NegativeMaxReceiveCount_ThrowsConfiguration()
    {
        var message = new QueueMessage
        {
            Channel = "q-ch",
            MaxReceiveCount = -1,
        };

        var act = () => MessageValidator.ValidateQueueMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxReceiveCount*");
    }

    [Fact]
    public void ValidateCommandMessage_ValidMessage_DoesNotThrow()
    {
        var message = new CommandMessage
        {
            Channel = "cmd-ch",
            TimeoutInSeconds = 10,
        };

        var act = () => MessageValidator.ValidateCommandMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateCommandMessage_NullMessage_ThrowsArgumentNull()
    {
        var act = () => MessageValidator.ValidateCommandMessage(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateCommandMessage_EmptyChannel_ThrowsConfiguration()
    {
        var message = new CommandMessage { Channel = "" };

        var act = () => MessageValidator.ValidateCommandMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Channel*");
    }

    [Fact]
    public void ValidateCommandMessage_ZeroTimeout_ThrowsConfiguration()
    {
        var message = new CommandMessage
        {
            Channel = "cmd-ch",
            TimeoutInSeconds = 0,
        };

        var act = () => MessageValidator.ValidateCommandMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*TimeoutInSeconds*");
    }

    [Fact]
    public void ValidateCommandMessage_NegativeTimeout_ThrowsConfiguration()
    {
        var message = new CommandMessage
        {
            Channel = "cmd-ch",
            TimeoutInSeconds = -5,
        };

        var act = () => MessageValidator.ValidateCommandMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*TimeoutInSeconds*");
    }

    [Fact]
    public void ValidateCommandMessage_NullTimeout_DoesNotThrow()
    {
        var message = new CommandMessage
        {
            Channel = "cmd-ch",
            TimeoutInSeconds = null,
        };

        var act = () => MessageValidator.ValidateCommandMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateQueryMessage_ValidMessage_DoesNotThrow()
    {
        var message = new QueryMessage
        {
            Channel = "qry-ch",
            TimeoutInSeconds = 10,
        };

        var act = () => MessageValidator.ValidateQueryMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateQueryMessage_NullMessage_ThrowsArgumentNull()
    {
        var act = () => MessageValidator.ValidateQueryMessage(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateQueryMessage_EmptyChannel_ThrowsConfiguration()
    {
        var message = new QueryMessage { Channel = "" };

        var act = () => MessageValidator.ValidateQueryMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Channel*");
    }

    [Fact]
    public void ValidateQueryMessage_ZeroTimeout_ThrowsConfiguration()
    {
        var message = new QueryMessage
        {
            Channel = "qry-ch",
            TimeoutInSeconds = 0,
        };

        var act = () => MessageValidator.ValidateQueryMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*TimeoutInSeconds*");
    }

    [Fact]
    public void ValidateQueryMessage_ZeroCacheTtl_ThrowsConfiguration()
    {
        var message = new QueryMessage
        {
            Channel = "qry-ch",
            CacheTtlSeconds = 0,
        };

        var act = () => MessageValidator.ValidateQueryMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*CacheTtlSeconds*");
    }

    [Fact]
    public void ValidateQueryMessage_NegativeCacheTtl_ThrowsConfiguration()
    {
        var message = new QueryMessage
        {
            Channel = "qry-ch",
            CacheTtlSeconds = -1,
        };

        var act = () => MessageValidator.ValidateQueryMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*CacheTtlSeconds*");
    }

    [Fact]
    public void ValidateQueryMessage_NullCacheTtl_DoesNotThrow()
    {
        var message = new QueryMessage
        {
            Channel = "qry-ch",
            CacheTtlSeconds = null,
        };

        var act = () => MessageValidator.ValidateQueryMessage(message);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateEventMessage_ChannelWithWildcard_Throws()
    {
        var message = new EventMessage { Channel = "orders.*" };

        var act = () => MessageValidator.ValidateEventMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*wildcard*");
    }

    [Fact]
    public void ValidateEventMessage_ChannelWithWhitespace_Throws()
    {
        var message = new EventMessage { Channel = "my channel" };

        var act = () => MessageValidator.ValidateEventMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*whitespace*");
    }

    [Fact]
    public void ValidateEventMessage_ChannelEndingWithDot_Throws()
    {
        var message = new EventMessage { Channel = "my.channel." };

        var act = () => MessageValidator.ValidateEventMessage(message);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*'.'*");
    }

    [Fact]
    public void EventsSubscription_ChannelWithWildcard_DoesNotThrow()
    {
        var subscription = new EventsSubscription { Channel = "orders.*" };

        var act = () => subscription.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void EventStoreSubscription_ChannelWithWildcard_Throws()
    {
        var subscription = new EventStoreSubscription { Channel = "orders.*" };

        var act = () => subscription.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*wildcard*");
    }
}
