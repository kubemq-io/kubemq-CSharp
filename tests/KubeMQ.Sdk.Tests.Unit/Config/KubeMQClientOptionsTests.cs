using FluentAssertions;
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Tests.Unit.Config;

public class KubeMQClientOptionsTests
{
    [Fact]
    public void Validate_DefaultOptions_DoesNotThrow()
    {
        var options = new KubeMQClientOptions();

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ValidOptions_DoesNotThrow()
    {
        var options = new KubeMQClientOptions
        {
            Address = "kubemq-server:50000",
            ClientId = "test-client",
            DefaultTimeout = TimeSpan.FromSeconds(10),
            MaxSendSize = 1024 * 1024,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmptyAddress_ThrowsConfigurationException(string? address)
    {
        var options = new KubeMQClientOptions { Address = address! };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Address*");
    }

    [Fact]
    public void Validate_ZeroDefaultTimeout_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            DefaultTimeout = TimeSpan.Zero,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*DefaultTimeout*");
    }

    [Fact]
    public void Validate_NegativeDefaultTimeout_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            DefaultTimeout = TimeSpan.FromSeconds(-1),
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*DefaultTimeout*");
    }

    [Fact]
    public void Validate_ZeroConnectionTimeout_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            ConnectionTimeout = TimeSpan.Zero,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*ConnectionTimeout*");
    }

    [Fact]
    public void Validate_NegativeMaxSendSize_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            MaxSendSize = -1,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxSendSize*");
    }

    [Fact]
    public void Validate_ZeroMaxSendSize_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            MaxSendSize = 0,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxSendSize*");
    }

    [Fact]
    public void Validate_NegativeMaxReceiveSize_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            MaxReceiveSize = -1,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxReceiveSize*");
    }

    [Fact]
    public void Validate_BackoffMultiplierLessThanOne_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions { BackoffMultiplier = 0.5 },
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*BackoffMultiplier*");
    }

    [Fact]
    public void Validate_MaxDelayLessThanInitialDelay_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            Reconnect = new ReconnectOptions
            {
                InitialDelay = TimeSpan.FromSeconds(10),
                MaxDelay = TimeSpan.FromSeconds(1),
            },
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxDelay*InitialDelay*");
    }

    [Fact]
    public void Validate_ZeroCallbackDrainTimeout_ThrowsConfigurationException()
    {
        var options = new KubeMQClientOptions
        {
            CallbackDrainTimeout = TimeSpan.Zero,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*CallbackDrainTimeout*");
    }

    [Fact]
    public void DefaultValues_MatchGoldenStandard()
    {
        var options = new KubeMQClientOptions();

        options.Address.Should().Be("localhost:50000");
        options.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(10));
        options.MaxSendSize.Should().Be(100 * 1024 * 1024);
        options.MaxReceiveSize.Should().Be(100 * 1024 * 1024);
        options.GrpcChannelCount.Should().Be(5);
        options.WaitForReady.Should().BeTrue();
        options.AuthToken.Should().BeNull();
        options.ClientId.Should().BeNull();
        options.Tls.Should().BeNull();
    }

    [Fact]
    public void DefaultValues_RetryPolicyDefaults()
    {
        var options = new KubeMQClientOptions();

        options.Retry.Should().NotBeNull();
        options.Retry.Enabled.Should().BeTrue();
        options.Retry.MaxRetries.Should().Be(3);
        options.Retry.InitialBackoff.Should().Be(TimeSpan.FromMilliseconds(500));
        options.Retry.MaxBackoff.Should().Be(TimeSpan.FromSeconds(30));
        options.Retry.BackoffMultiplier.Should().Be(2.0);
    }

    [Fact]
    public void ToString_RedactsAuthToken()
    {
        var options = new KubeMQClientOptions
        {
            AuthToken = "super-secret-token",
        };

        var result = options.ToString();

        result.Should().NotContain("super-secret-token");
        result.Should().Contain("<redacted>");
    }

    [Fact]
    public void ToString_ShowsNotSetWhenNoAuthToken()
    {
        var options = new KubeMQClientOptions
        {
            AuthToken = null,
        };

        var result = options.ToString();

        result.Should().Contain("<not set>");
    }

    [Fact]
    public void ToString_ContainsAddress()
    {
        var options = new KubeMQClientOptions
        {
            Address = "my-server:50000",
        };

        var result = options.ToString();

        result.Should().Contain("my-server:50000");
    }

    [Fact]
    public void ToString_ContainsClientIdOrAuto()
    {
        var optionsWithId = new KubeMQClientOptions { ClientId = "my-client" };
        var optionsNoId = new KubeMQClientOptions { ClientId = null };

        optionsWithId.ToString().Should().Contain("my-client");
        optionsNoId.ToString().Should().Contain("<auto>");
    }

    [Fact]
    public void Validate_ReconnectTimeoutZero_Throws()
    {
        var options = new KubeMQClientOptions
        {
            ReconnectTimeout = TimeSpan.Zero,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*ReconnectTimeout*positive*");
    }

    [Fact]
    public void Validate_MaxMessageBodySizeNegative_Throws()
    {
        var options = new KubeMQClientOptions
        {
            MaxMessageBodySize = -1,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MaxMessageBodySize*non-negative*");
    }
}
