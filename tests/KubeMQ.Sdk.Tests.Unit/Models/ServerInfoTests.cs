using FluentAssertions;
using KubeMQ.Sdk.Common;

namespace KubeMQ.Sdk.Tests.Unit.Models;

public class ServerInfoTests
{
    [Fact]
    public void Construction_WithAllProperties_SetsCorrectly()
    {
        var info = new ServerInfo
        {
            Host = "kubemq-server",
            Version = "3.5.0",
            ServerStartTime = 1700000000,
            ServerUpTimeSeconds = 86400,
        };

        info.Host.Should().Be("kubemq-server");
        info.Version.Should().Be("3.5.0");
        info.ServerStartTime.Should().Be(1700000000);
        info.ServerUpTimeSeconds.Should().Be(86400);
    }

    [Fact]
    public void DefaultValues_ForOptionalProperties()
    {
        var info = new ServerInfo
        {
            Host = "h",
            Version = "1.0",
        };

        info.ServerStartTime.Should().Be(0);
        info.ServerUpTimeSeconds.Should().Be(0);
    }

    [Fact]
    public void ToString_ContainsHostVersionAndUptime()
    {
        var info = new ServerInfo
        {
            Host = "test-host",
            Version = "2.0.0",
            ServerUpTimeSeconds = 3600,
        };

        var result = info.ToString();

        result.Should().Contain("test-host");
        result.Should().Contain("2.0.0");
        result.Should().Contain("3600");
    }

    [Fact]
    public void ToString_MatchesExpectedFormat()
    {
        var info = new ServerInfo
        {
            Host = "myhost",
            Version = "1.0.0",
            ServerUpTimeSeconds = 100,
        };

        info.ToString().Should().Be("Host=myhost, Version=1.0.0, Uptime=100s");
    }

    [Fact]
    public void TwoInstances_WithSameValues_AreNotReferenceEqual()
    {
        var info1 = new ServerInfo { Host = "h", Version = "1.0" };
        var info2 = new ServerInfo { Host = "h", Version = "1.0" };

        info1.Should().NotBeSameAs(info2);
    }
}
