using KubeMQ.Sdk.Auth;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Client;

/// <summary>
/// Configuration for a <see cref="KubeMQClient"/> instance.
/// Validated at construction; immutable semantics enforced by the client constructor.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread Safety:</b> This type is NOT thread-safe. Configure all properties
/// before passing to the <see cref="KubeMQClient"/> constructor. Do not modify
/// after the client has been created.
/// </para>
/// <para>
/// Properties use <c>{ get; set; }</c> (not <c>{ get; init; }</c>) per the .NET Options pattern
/// convention, enabling the standard <c>Configure&lt;T&gt;</c> delegate pattern in DI scenarios.
/// </para>
/// </remarks>
/// <threadsafety static="true" instance="false"/>
public class KubeMQClientOptions
{
    /// <summary>Gets or sets the KubeMQ server address. Default: "localhost:50000".</summary>
    public string Address { get; set; } = "localhost:50000";

    /// <summary>Gets or sets the client identifier. Auto-generated if null or empty.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the static authentication token. Redacted in <see cref="ToString"/>.</summary>
    public string? AuthToken { get; set; }

    /// <summary>Gets or sets the default timeout for SDK-level operations. Default: 5 s.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the timeout for the initial connection attempt. Default: 10 s.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the maximum gRPC send message size in bytes. Default: 104,857,600 (100 MB).</summary>
    public int MaxSendSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>Gets or sets the maximum gRPC receive message size in bytes. Default: 4,194,304 (4 MB).
    /// Matches the server's default TOML config. Override to 100 MB (104857600) if needed for large messages.</summary>
    public int MaxReceiveSize { get; set; } = 4 * 1024 * 1024;

    /// <summary>
    /// Gets or sets a value indicating whether operations block until the connection is ready.
    /// When true, operations during <c>Connecting</c>/<c>Reconnecting</c> states will wait.
    /// When false, they fail immediately with <see cref="KubeMQConnectionException"/>.
    /// Default: true.
    /// </summary>
    public bool WaitForReady { get; set; } = true;

    /// <summary>Gets or sets the retry policy for transient operation failures.</summary>
    public RetryPolicy Retry { get; set; } = new();

    /// <summary>Gets or sets the keepalive configuration for dead connection detection.</summary>
    public KeepaliveOptions Keepalive { get; set; } = new();

    /// <summary>Gets or sets the reconnection behavior configuration.</summary>
    public ReconnectOptions Reconnect { get; set; } = new();

    /// <summary>Gets or sets the logger factory for SDK diagnostics. Null uses NullLoggerFactory.</summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>Gets or sets the TLS/mTLS configuration. Null disables TLS.</summary>
    public TlsOptions? Tls { get; set; }

    /// <summary>Gets or sets the subscription callback behavior configuration.</summary>
    public SubscriptionOptions Subscription { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum time to wait for in-flight subscription callbacks to complete
    /// during shutdown. Default is 30 seconds.
    /// </summary>
    /// <remarks>
    /// This timeout is separate from the drain timeout (5s) which covers
    /// flushing buffered messages and pending gRPC responses. The callback
    /// completion timeout is typically longer because user-supplied handlers
    /// may perform I/O-bound work.
    /// </remarks>
    public TimeSpan CallbackDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the pluggable credential provider for dynamic token refresh.
    /// Takes precedence over <see cref="AuthToken"/> when both are set.
    /// </summary>
    public ICredentialProvider? CredentialProvider { get; set; }

    /// <summary>
    /// Validates all property values. Throws <see cref="KubeMQConfigurationException"/> on invalid values.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">One or more property values are invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Address))
        {
            throw new KubeMQConfigurationException("Address must not be empty.");
        }

        if (DefaultTimeout <= TimeSpan.Zero)
        {
            throw new KubeMQConfigurationException(
                $"DefaultTimeout must be positive, got {DefaultTimeout}.");
        }

        if (ConnectionTimeout <= TimeSpan.Zero)
        {
            throw new KubeMQConfigurationException(
                $"ConnectionTimeout must be positive, got {ConnectionTimeout}.");
        }

        if (MaxSendSize <= 0)
        {
            throw new KubeMQConfigurationException(
                $"MaxSendSize must be positive, got {MaxSendSize}.");
        }

        if (MaxReceiveSize <= 0)
        {
            throw new KubeMQConfigurationException(
                $"MaxReceiveSize must be positive, got {MaxReceiveSize}.");
        }

        if (Reconnect.BackoffMultiplier < 1.0)
        {
            throw new KubeMQConfigurationException(
                $"BackoffMultiplier must be >= 1.0, got {Reconnect.BackoffMultiplier}.");
        }

        if (Reconnect.MaxDelay < Reconnect.InitialDelay)
        {
            throw new KubeMQConfigurationException("MaxDelay must be >= InitialDelay.");
        }

        if (CallbackDrainTimeout <= TimeSpan.Zero)
        {
            throw new KubeMQConfigurationException(
                $"CallbackDrainTimeout must be positive, got {CallbackDrainTimeout}.");
        }

        Retry.Validate();
        Tls?.Validate();
        Subscription.Validate();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        string credentialInfo = CredentialProvider is not null
            ? $"<{CredentialProvider.GetType().Name}>"
            : AuthToken is not null ? "<redacted>" : "<not set>";

        return $"KubeMQClientOptions {{ " +
               $"Address = {Address}, " +
               $"ClientId = {ClientId ?? "<auto>"}, " +
               $"AuthToken = {(AuthToken is null ? "<not set>" : "<redacted>")}, " +
               $"CredentialProvider = {credentialInfo}, " +
               $"Tls.Enabled = {Tls?.Enabled ?? false}, " +
               $"DefaultTimeout = {DefaultTimeout}, " +
               $"ConnectionTimeout = {ConnectionTimeout}, " +
               $"WaitForReady = {WaitForReady}, " +
               $"Reconnect.Enabled = {Reconnect.Enabled} }}";
    }
}
