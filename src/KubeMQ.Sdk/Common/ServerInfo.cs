namespace KubeMQ.Sdk.Common;

/// <summary>
/// Contains information about the connected KubeMQ server.
/// </summary>
public sealed class ServerInfo
{
    /// <summary>Gets the server host name.</summary>
    public required string Host { get; init; }

    /// <summary>Gets the server version.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the server start time as a Unix timestamp.</summary>
    public long ServerStartTime { get; init; }

    /// <summary>Gets the server uptime in seconds.</summary>
    public long ServerUpTimeSeconds { get; init; }

    /// <inheritdoc />
    public override string ToString() =>
        $"Host={Host}, Version={Version}, Uptime={ServerUpTimeSeconds}s";
}
