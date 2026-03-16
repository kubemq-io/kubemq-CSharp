using FluentAssertions;
using KubeMQ.Sdk.Config;

namespace KubeMQ.Sdk.Tests.Unit.Config;

public sealed class ReconnectOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new ReconnectOptions();

        options.Enabled.Should().BeTrue();
        options.MaxAttempts.Should().Be(0);
        options.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxDelay.Should().Be(TimeSpan.FromSeconds(30));
        options.BackoffMultiplier.Should().Be(2.0);
        options.BufferSize.Should().Be(8 * 1024 * 1024);
        options.BufferFullMode.Should().Be(BufferFullMode.Block);
    }

    [Fact]
    public void Enabled_CanBeSetToFalse()
    {
        var options = new ReconnectOptions { Enabled = false };

        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void MaxAttempts_CanBeSet()
    {
        var options = new ReconnectOptions { MaxAttempts = 5 };

        options.MaxAttempts.Should().Be(5);
    }

    [Fact]
    public void InitialDelay_CanBeSet()
    {
        var options = new ReconnectOptions { InitialDelay = TimeSpan.FromMilliseconds(500) };

        options.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void MaxDelay_CanBeSet()
    {
        var options = new ReconnectOptions { MaxDelay = TimeSpan.FromMinutes(1) };

        options.MaxDelay.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void BackoffMultiplier_CanBeSet()
    {
        var options = new ReconnectOptions { BackoffMultiplier = 1.5 };

        options.BackoffMultiplier.Should().Be(1.5);
    }

    [Fact]
    public void BufferSize_CanBeSet()
    {
        var options = new ReconnectOptions { BufferSize = 1024 };

        options.BufferSize.Should().Be(1024);
    }

    [Fact]
    public void BufferFullMode_CanBeSetToError()
    {
        var options = new ReconnectOptions { BufferFullMode = BufferFullMode.Error };

        options.BufferFullMode.Should().Be(BufferFullMode.Error);
    }
}
