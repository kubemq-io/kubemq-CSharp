using FluentAssertions;
using KubeMQ.Sdk.Commands;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.EventsStore;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Queries;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class SubscriptionValidationTests
{
    public class CommandsSubscriptionValidation
    {
        [Fact]
        public void Validate_ValidChannel_DoesNotThrow()
        {
            var sub = new CommandsSubscription { Channel = "commands.orders" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_NullOrEmptyOrWhitespaceChannel_Throws(string? channel)
        {
            var sub = new CommandsSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>();
        }

        [Theory]
        [InlineData("orders.*")]
        [InlineData("orders.>")]
        public void Validate_ChannelWithWildcard_Throws(string channel)
        {
            var sub = new CommandsSubscription { Channel = channel };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*wildcard*");
        }

        [Fact]
        public void Validate_ChannelWithWhitespaceInMiddle_Throws()
        {
            var sub = new CommandsSubscription { Channel = "orders test" };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*whitespace*");
        }

        [Fact]
        public void Validate_ChannelEndingWithDot_Throws()
        {
            var sub = new CommandsSubscription { Channel = "orders." };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*'.'*");
        }

        [Fact]
        public void Group_IsOptional_NullIsFine()
        {
            var sub = new CommandsSubscription { Channel = "ch" };

            sub.Group.Should().BeNull();

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }
    }

    public class QueriesSubscriptionValidation
    {
        [Fact]
        public void Validate_ValidChannel_DoesNotThrow()
        {
            var sub = new QueriesSubscription { Channel = "queries.lookup" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_NullOrEmptyOrWhitespaceChannel_Throws(string? channel)
        {
            var sub = new QueriesSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>();
        }

        [Theory]
        [InlineData("queries.*")]
        [InlineData("queries.>")]
        public void Validate_ChannelWithWildcard_Throws(string channel)
        {
            var sub = new QueriesSubscription { Channel = channel };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*wildcard*");
        }

        [Fact]
        public void Validate_ChannelWithWhitespaceInMiddle_Throws()
        {
            var sub = new QueriesSubscription { Channel = "queries test" };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*whitespace*");
        }

        [Fact]
        public void Validate_ChannelEndingWithDot_Throws()
        {
            var sub = new QueriesSubscription { Channel = "queries." };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*'.'*");
        }

        [Fact]
        public void Group_IsOptional_NullIsFine()
        {
            var sub = new QueriesSubscription { Channel = "ch" };

            sub.Group.Should().BeNull();

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }
    }

    public class EventsSubscriptionValidation
    {
        [Fact]
        public void Validate_ValidChannel_DoesNotThrow()
        {
            var sub = new EventsSubscription { Channel = "events.orders" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_NullOrEmptyOrWhitespaceChannel_Throws(string? channel)
        {
            var sub = new EventsSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>();
        }

        [Theory]
        [InlineData("orders.*")]
        [InlineData("orders.>")]
        public void Validate_ChannelWithWildcard_DoesNotThrow(string channel)
        {
            var sub = new EventsSubscription { Channel = channel };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_ChannelWithWhitespaceInMiddle_Throws()
        {
            var sub = new EventsSubscription { Channel = "events test" };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*whitespace*");
        }

        [Fact]
        public void Validate_ChannelEndingWithDot_Throws()
        {
            var sub = new EventsSubscription { Channel = "events." };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*'.'*");
        }
    }

    public class EventStoreSubscriptionValidation
    {
        [Fact]
        public void Validate_ValidChannel_DoesNotThrow()
        {
            var sub = new EventStoreSubscription { Channel = "store.orders" };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_NullOrEmptyOrWhitespaceChannel_Throws(string? channel)
        {
            var sub = new EventStoreSubscription { Channel = channel! };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>();
        }

        [Theory]
        [InlineData("store.*")]
        [InlineData("store.>")]
        public void Validate_ChannelWithWildcard_Throws(string channel)
        {
            var sub = new EventStoreSubscription { Channel = channel };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*wildcard*");
        }

        [Fact]
        public void Validate_ChannelWithWhitespaceInMiddle_Throws()
        {
            var sub = new EventStoreSubscription { Channel = "store test" };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*whitespace*");
        }

        [Fact]
        public void Validate_ChannelEndingWithDot_Throws()
        {
            var sub = new EventStoreSubscription { Channel = "store." };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*'.'*");
        }

        // --- StartAtSequence ---

        [Fact]
        public void Validate_StartAtSequence_WithNullSequence_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtSequence,
                StartSequence = null,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartSequence*positive*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Validate_StartAtSequence_WithZeroOrNegative_Throws(long sequence)
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtSequence,
                StartSequence = sequence,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartSequence*positive*");
        }

        [Fact]
        public void Validate_StartAtSequence_WithValidPositiveValue_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtSequence,
                StartSequence = 42,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        // --- StartAtTime ---

        [Fact]
        public void Validate_StartAtTime_WithNullTime_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtTime,
                StartTime = null,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTime*required*");
        }

        [Fact]
        public void Validate_StartAtTime_WithValidTime_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtTime,
                StartTime = DateTimeOffset.UtcNow.AddHours(-1),
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_StartAtTime_WithTimeBeforeEpoch_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtTime,
                StartTime = new DateTimeOffset(1969, 1, 1, 0, 0, 0, TimeSpan.Zero),
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTime*valid time*epoch*");
        }

        [Fact]
        public void Validate_StartAtTime_WithExactEpoch_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtTime,
                StartTime = DateTimeOffset.UnixEpoch,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTime*valid time*epoch*");
        }

        // --- StartAtTimeDelta ---

        [Fact]
        public void Validate_StartAtTimeDelta_WithNull_Throws()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtTimeDelta,
                StartTimeDeltaSeconds = null,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTimeDeltaSeconds*positive*");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Validate_StartAtTimeDelta_WithZeroOrNegative_Throws(int delta)
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtTimeDelta,
                StartTimeDeltaSeconds = delta,
            };

            var act = () => sub.Validate();

            act.Should().Throw<KubeMQConfigurationException>()
                .WithMessage("*StartTimeDeltaSeconds*positive*");
        }

        [Fact]
        public void Validate_StartAtTimeDelta_WithPositiveValue_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartAtTimeDelta,
                StartTimeDeltaSeconds = 3600,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        // --- Simple start positions that need no extra params ---

        [Fact]
        public void Validate_StartFromNew_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartFromNew,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_StartFromFirst_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartFromFirst,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_StartFromLast_DoesNotThrow()
        {
            var sub = new EventStoreSubscription
            {
                Channel = "ch",
                StartPosition = EventStoreStartPosition.StartFromLast,
            };

            var act = () => sub.Validate();

            act.Should().NotThrow();
        }
    }
}
