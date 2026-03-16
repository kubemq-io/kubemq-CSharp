using FluentAssertions;

namespace KubeMQ.Sdk.Tests.Unit;

public class KubeMQSdkInfoTests
{
    [Fact]
    public void Version_ReturnsNonEmptyString()
    {
        var version = KubeMQSdkInfo.Version;

        version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Version_FollowsSemVerFormat()
    {
        var version = KubeMQSdkInfo.Version;

        var versionWithoutBuildMeta = version.Split('+')[0];
        versionWithoutBuildMeta.Should().MatchRegex(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)*)?$");
    }

    [Fact]
    public void Version_IsCached_ReturnsSameInstance()
    {
        var v1 = KubeMQSdkInfo.Version;
        var v2 = KubeMQSdkInfo.Version;

        v1.Should().BeSameAs(v2);
    }

    [Fact]
    public void AssemblyVersion_ReturnsValidVersion()
    {
        var version = KubeMQSdkInfo.AssemblyVersion;

        version.Should().NotBeNull();
        version.Major.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void AssemblyVersion_MajorMatchesInformationalVersion()
    {
        var infoVersionMajor = KubeMQSdkInfo.Version.Split('.')[0];
        var assemblyMajor = KubeMQSdkInfo.AssemblyVersion.Major.ToString();

        assemblyMajor.Should().Be(infoVersionMajor);
    }
}
