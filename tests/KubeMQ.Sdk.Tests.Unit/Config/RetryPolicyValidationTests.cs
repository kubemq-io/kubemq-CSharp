using FluentAssertions;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Tests.Unit.Config;

public class RetryPolicyValidationTests
{
    [Fact]
    public void Validate_Defaults_DoesNotThrow()
    {
        var policy = new RetryPolicy();

        var act = () => policy.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public void Validate_MaxRetriesOutOfRange_Throws(int maxRetries)
    {
        var policy = new RetryPolicy { MaxRetries = maxRetries };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxRetries*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Validate_MaxRetriesInRange_DoesNotThrow(int maxRetries)
    {
        var policy = new RetryPolicy { MaxRetries = maxRetries };

        var act = () => policy.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_InitialBackoffTooSmall_Throws()
    {
        var policy = new RetryPolicy
        {
            InitialBackoff = TimeSpan.FromMilliseconds(10),
        };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*InitialBackoff*");
    }

    [Fact]
    public void Validate_InitialBackoffTooLarge_Throws()
    {
        var policy = new RetryPolicy
        {
            InitialBackoff = TimeSpan.FromSeconds(10),
        };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*InitialBackoff*");
    }

    [Fact]
    public void Validate_MaxBackoffTooSmall_Throws()
    {
        var policy = new RetryPolicy
        {
            MaxBackoff = TimeSpan.FromMilliseconds(500),
        };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxBackoff*");
    }

    [Fact]
    public void Validate_MaxBackoffTooLarge_Throws()
    {
        var policy = new RetryPolicy
        {
            MaxBackoff = TimeSpan.FromSeconds(200),
        };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxBackoff*");
    }

    [Fact]
    public void Validate_MaxBackoffLessThanInitialBackoff_Throws()
    {
        var policy = new RetryPolicy
        {
            InitialBackoff = TimeSpan.FromSeconds(2),
            MaxBackoff = TimeSpan.FromSeconds(1),
        };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxBackoff*InitialBackoff*");
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.4)]
    [InlineData(3.1)]
    [InlineData(5.0)]
    public void Validate_BackoffMultiplierOutOfRange_Throws(double multiplier)
    {
        var policy = new RetryPolicy { BackoffMultiplier = multiplier };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*BackoffMultiplier*");
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void Validate_BackoffMultiplierInRange_DoesNotThrow(double multiplier)
    {
        var policy = new RetryPolicy { BackoffMultiplier = multiplier };

        var act = () => policy.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_MaxConcurrentRetriesOutOfRange_Throws(int concurrent)
    {
        var policy = new RetryPolicy { MaxConcurrentRetries = concurrent };

        var act = () => policy.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxConcurrentRetries*");
    }

    [Fact]
    public void DefaultValues_MatchSpec()
    {
        var policy = new RetryPolicy();

        policy.Enabled.Should().BeTrue();
        policy.MaxRetries.Should().Be(3);
        policy.InitialBackoff.Should().Be(TimeSpan.FromMilliseconds(500));
        policy.MaxBackoff.Should().Be(TimeSpan.FromSeconds(30));
        policy.BackoffMultiplier.Should().Be(2.0);
        policy.JitterMode.Should().Be(JitterMode.Full);
        policy.MaxConcurrentRetries.Should().Be(10);
    }
}
