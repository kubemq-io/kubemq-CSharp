namespace KubeMQ.Sdk.Config;

/// <summary>
/// Configures HTTP/2 keepalive pings on the underlying <see cref="System.Net.Http.SocketsHttpHandler"/>.
/// Dead connections are detected within <see cref="PingInterval"/> + <see cref="PingTimeout"/> (default: 15 s).
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is NOT thread-safe. Configure before passing to
/// the client constructor. Do not modify after the client has been created.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="false"/>
public sealed class KeepaliveOptions
{
    /// <summary>Gets or sets the interval between keepalive pings. Default: 10 s.</summary>
    public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the timeout waiting for a keepalive ping ACK. Default: 5 s.</summary>
    public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets a value indicating whether to send pings even without active streams. Default: true.</summary>
    public bool PermitWithoutStream { get; set; } = true;
}
