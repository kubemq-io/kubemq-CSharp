using FluentAssertions;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Queries;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class SubscriptionTests
{
    public class EventsSubscriptionTests
    {
        [Fact]
        public void Construction_SetsChannel()
        {
            var sub = new EventsSubscription { Channel = "events.orders" };

            sub.Channel.Should().Be("events.orders");
        }

        [Fact]
        public void Group_IsOptional_DefaultsToNull()
        {
            var sub = new EventsSubscription { Channel = "ch" };

            sub.Group.Should().BeNull();
        }

        [Fact]
        public void Group_CanBeSet()
        {
            var sub = new EventsSubscription { Channel = "ch", Group = "workers" };

            sub.Group.Should().Be("workers");
        }

        [Fact]
        public void Validate_ValidChannel_DoesNotThrow()
        {
            var sub = new EventsSubscription { Channel = "events.test" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_EmptyChannel_ThrowsConfigurationException(string? channel)
        {
            var sub = new EventsSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*Channel*required*");
        }
    }

    public class CommandsSubscriptionTests
    {
        [Fact]
        public void Construction_SetsChannel()
        {
            var sub = new CommandsSubscription { Channel = "commands.process" };

            sub.Channel.Should().Be("commands.process");
        }

        [Fact]
        public void Group_IsOptional_DefaultsToNull()
        {
            var sub = new CommandsSubscription { Channel = "ch" };

            sub.Group.Should().BeNull();
        }

        [Fact]
        public void Validate_ValidChannel_DoesNotThrow()
        {
            var sub = new CommandsSubscription { Channel = "commands.test" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_EmptyChannel_ThrowsConfigurationException(string? channel)
        {
            var sub = new CommandsSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*Channel*required*");
        }
    }

    public class QueriesSubscriptionTests
    {
        [Fact]
        public void Construction_SetsChannel()
        {
            var sub = new QueriesSubscription { Channel = "queries.lookup" };

            sub.Channel.Should().Be("queries.lookup");
        }

        [Fact]
        public void Group_IsOptional_DefaultsToNull()
        {
            var sub = new QueriesSubscription { Channel = "ch" };

            sub.Group.Should().BeNull();
        }

        [Fact]
        public void Validate_ValidChannel_DoesNotThrow()
        {
            var sub = new QueriesSubscription { Channel = "queries.test" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_EmptyChannel_ThrowsConfigurationException(string? channel)
        {
            var sub = new QueriesSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*Channel*required*");
        }
    }

    public class EventStoreSubscriptionTests
    {
        [Fact]
        public void Construction_SetsChannelAndDefaults()
        {
            var sub = new EventStoreSubscription { Channel = "store.orders" };

            sub.Channel.Should().Be("store.orders");
            sub.Group.Should().BeNull();
            sub.StartPosition.Should().Be(EventStoreStartPosition.FromNew);
            sub.StartSequence.Should().BeNull();
            sub.StartTime.Should().BeNull();
            sub.StartTimeDeltaSeconds.Should().BeNull();
        }

        [Fact]
        public void Validate_ValidChannel_FromNew_DoesNotThrow()
        {
            var sub = new EventStoreSubscription { Channel = "ch" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_FromFirst_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromFirst,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_FromLast_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromLast,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_EmptyChannel_Throws(string? channel)
        {
            var sub = new EventStoreSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*Channel*required*");
        }

        [Fact]
        public void Validate_FromSequence_WithValidSequence_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromSequence,
                StartSequence = 100,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_FromSequence_WithZeroSequence_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromSequence,
                StartSequence = 0,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartSequence*positive*");
        }

        [Fact]
        public void Validate_FromSequence_WithNullSequence_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromSequence,
                StartSequence = null,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartSequence*positive*");
        }

        [Fact]
        public void Validate_FromSequence_WithNegativeSequence_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromSequence,
                StartSequence = -1,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartSequence*positive*");
        }

        [Fact]
        public void Validate_FromTime_WithValidTime_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromTime,
                StartTime = DateTimeOffset.UtcNow.AddHours(-1),
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_FromTime_WithNullTime_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromTime,
                StartTime = null,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTime*required*");
        }

        [Fact]
        public void Validate_FromTimeDelta_WithPositiveValue_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromTimeDelta,
                StartTimeDeltaSeconds = 3600,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_FromTimeDelta_WithNull_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromTimeDelta,
                StartTimeDeltaSeconds = null,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTimeDeltaSeconds*positive*");
        }

        [Fact]
        public void Validate_FromTimeDelta_WithZero_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromTimeDelta,
                StartTimeDeltaSeconds = 0,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTimeDeltaSeconds*positive*");
        }

        [Fact]
        public void Validate_FromTimeDelta_WithNegative_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.FromTimeDelta,
                StartTimeDeltaSeconds = -10,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTimeDeltaSeconds*positive*");
        }

        [Fact]
        public void Group_CanBeSet()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                Group = "consumer-group-1",
            };

            sub.Group.Should().Be("consumer-group-1");
        }
    }
}
