using FluentAssertions;
using KubeMQ.Sdk.Config;

namespace KubeMQ.Sdk.Tests.Unit.Config;

public sealed class KeepaliveOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new KeepaliveOptions();

        options.PingInterval.Should().Be(TimeSpan.FromSeconds(10));
        options.PingTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.PermitWithoutStream.Should().BeTrue();
    }

    [Fact]
    public void PingInterval_CanBeSet()
    {
        var options = new KeepaliveOptions { PingInterval = TimeSpan.FromSeconds(20) };

        options.PingInterval.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void PingTimeout_CanBeSet()
    {
        var options = new KeepaliveOptions { PingTimeout = TimeSpan.FromSeconds(10) };

        options.PingTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void PermitWithoutStream_CanBeSetToFalse()
    {
        var options = new KeepaliveOptions { PermitWithoutStream = false };

        options.PermitWithoutStream.Should().BeFalse();
    }
}
