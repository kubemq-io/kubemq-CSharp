using System.Reflection;

namespace KubeMQ.Sdk;

/// <summary>
/// Provides runtime information about the KubeMQ SDK assembly.
/// </summary>
public static class KubeMQSdkInfo
{
    private static readonly Lazy<string> _versionLazy = new(
        () => typeof(KubeMQSdkInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0-unknown");

    /// <summary>
    /// Gets the SDK version string including pre-release label and build metadata.
    /// Example: "3.0.0", "3.1.0-beta.1+abc1234".
    /// </summary>
    /// <remarks>
    /// The version is read from <see cref="AssemblyInformationalVersionAttribute"/>
    /// which is automatically set by MSBuild from the <c>&lt;Version&gt;</c> project property.
    /// When SourceLink is enabled, the build metadata includes the git commit SHA.
    /// </remarks>
    public static string Version => _versionLazy.Value;

    /// <summary>
    /// Gets the SDK version as a <see cref="System.Version"/> object (MAJOR.MINOR.PATCH only,
    /// no pre-release or build metadata).
    /// </summary>
    /// <remarks>
    /// Returns the assembly file version, which always strips pre-release labels.
    /// Use <see cref="Version"/> for the full version string including pre-release labels.
    /// </remarks>
    public static System.Version AssemblyVersion =>
        typeof(KubeMQSdkInfo).Assembly.GetName().Version!;
}
