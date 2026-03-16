using FluentAssertions;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Queues;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class QueuePollRequestTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var request = new QueuePollRequest { Channel = "ch" };

        request.MaxMessages.Should().Be(1);
        request.WaitTimeoutSeconds.Should().Be(10);
        request.VisibilitySeconds.Should().BeNull();
        request.AutoAck.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithValidRequest_DoesNotThrow()
    {
        var request = new QueuePollRequest
        {
            Channel = "valid-channel",
            MaxMessages = 5,
            WaitTimeoutSeconds = 30,
        };

        var act = () => request.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithNullOrEmptyChannel_Throws(string? channel)
    {
        var request = new QueuePollRequest { Channel = channel! };

        var act = () => request.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Channel*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithMaxMessagesLessThanOrEqualZero_Throws(int maxMessages)
    {
        var request = new QueuePollRequest
        {
            Channel = "ch",
            MaxMessages = maxMessages,
        };

        var act = () => request.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxMessages*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void Validate_WithWaitTimeoutLessThanOrEqualZero_Throws(int waitTimeout)
    {
        var request = new QueuePollRequest
        {
            Channel = "ch",
            WaitTimeoutSeconds = waitTimeout,
        };

        var act = () => request.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*WaitTimeoutSeconds*");
    }

    [Fact]
    public void Validate_WithNegativeVisibilitySeconds_Throws()
    {
        var request = new QueuePollRequest
        {
            Channel = "ch",
            VisibilitySeconds = -1,
        };

        var act = () => request.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*VisibilitySeconds*");
    }

    [Fact]
    public void Validate_WithZeroVisibilitySeconds_Throws()
    {
        var request = new QueuePollRequest
        {
            Channel = "ch",
            VisibilitySeconds = 0,
        };

        var act = () => request.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*VisibilitySeconds*");
    }

    [Fact]
    public void AllPropertySetters_Work()
    {
        var request = new QueuePollRequest
        {
            Channel = "test-channel",
            MaxMessages = 10,
            WaitTimeoutSeconds = 60,
            VisibilitySeconds = 120,
            AutoAck = true,
        };

        request.Channel.Should().Be("test-channel");
        request.MaxMessages.Should().Be(10);
        request.WaitTimeoutSeconds.Should().Be(60);
        request.VisibilitySeconds.Should().Be(120);
        request.AutoAck.Should().BeTrue();
    }
}
