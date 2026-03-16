using FluentAssertions;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Tests.Unit.Config;

public sealed class SubscriptionOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new SubscriptionOptions();

        options.MaxConcurrentCallbacks.Should().Be(1);
        options.CallbackBufferSize.Should().Be(256);
    }

    [Fact]
    public void Validate_WithDefaultValues_DoesNotThrow()
    {
        var options = new SubscriptionOptions();

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MaxConcurrentCallbacks_Zero_ThrowsConfigurationException()
    {
        var options = new SubscriptionOptions { MaxConcurrentCallbacks = 0 };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxConcurrentCallbacks must be >= 1*");
    }

    [Fact]
    public void Validate_MaxConcurrentCallbacks_Negative_ThrowsConfigurationException()
    {
        var options = new SubscriptionOptions { MaxConcurrentCallbacks = -1 };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxConcurrentCallbacks must be >= 1*");
    }

    [Fact]
    public void Validate_MaxConcurrentCallbacks_ExceedsMax_ThrowsConfigurationException()
    {
        var options = new SubscriptionOptions { MaxConcurrentCallbacks = 1025 };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxConcurrentCallbacks must be <= 1024*");
    }

    [Fact]
    public void Validate_MaxConcurrentCallbacks_AtMax_DoesNotThrow()
    {
        var options = new SubscriptionOptions { MaxConcurrentCallbacks = 1024 };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_CallbackBufferSize_Zero_ThrowsConfigurationException()
    {
        var options = new SubscriptionOptions { CallbackBufferSize = 0 };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*CallbackBufferSize must be >= 1*");
    }

    [Fact]
    public void Validate_CallbackBufferSize_Negative_ThrowsConfigurationException()
    {
        var options = new SubscriptionOptions { CallbackBufferSize = -5 };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*CallbackBufferSize must be >= 1*");
    }

    [Fact]
    public void Validate_CallbackBufferSize_One_DoesNotThrow()
    {
        var options = new SubscriptionOptions { CallbackBufferSize = 1 };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(1024)]
    public void Validate_ValidConcurrency_DoesNotThrow(int concurrency)
    {
        var options = new SubscriptionOptions { MaxConcurrentCallbacks = concurrency };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }
}
