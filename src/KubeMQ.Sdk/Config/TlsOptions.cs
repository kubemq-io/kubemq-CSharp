using System.IO;
using System.Security.Authentication;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Config;

/// <summary>
/// TLS/mTLS configuration for connections to the KubeMQ server.
/// Supports file paths and PEM-string-based certificate loading.
/// </summary>
public sealed class TlsOptions
{
    /// <summary>Gets or sets a value indicating whether TLS encryption is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the path to the client certificate file (PEM).</summary>
    public string? CertFile { get; set; }

    /// <summary>Gets or sets the path to the client private key file (PEM).</summary>
    public string? KeyFile { get; set; }

    /// <summary>Gets or sets the path to the CA certificate file (PEM).</summary>
    public string? CaFile { get; set; }

    /// <summary>Gets or sets the client certificate PEM string (alternative to file).</summary>
    public string? ClientCertificatePem { get; set; }

    /// <summary>Gets or sets the client private key PEM string (alternative to file).</summary>
    public string? ClientKeyPem { get; set; }

    /// <summary>Gets or sets the CA certificate PEM string (alternative to file).</summary>
    public string? CaCertificatePem { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to skip TLS certificate verification.
    /// Only for development — the SDK logs a WARNING on every connection when active.
    /// </summary>
    public bool InsecureSkipVerify { get; set; }

    /// <summary>Gets or sets the expected server hostname for TLS verification override.</summary>
    public string? ServerNameOverride { get; set; }

    /// <summary>Gets or sets the minimum TLS version. Default: TLS 1.2 (HTTP/2 requirement).</summary>
    public SslProtocols MinTlsVersion { get; set; } = SslProtocols.Tls12;

    /// <summary>Gets a value indicating whether client certificate material is configured.</summary>
    internal bool HasClientCertificate =>
        (CertFile is not null && KeyFile is not null) ||
        (ClientCertificatePem is not null && ClientKeyPem is not null);

    /// <summary>Gets a value indicating whether a custom CA certificate is configured.</summary>
    internal bool HasCaCertificate =>
        CaFile is not null || CaCertificatePem is not null;

    /// <summary>
    /// Validates TLS configuration. Throws <see cref="KubeMQConfigurationException"/> on invalid values.
    /// </summary>
    /// <exception cref="KubeMQConfigurationException">TLS configuration is invalid.</exception>
    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (CertFile is not null && !File.Exists(CertFile))
        {
            throw new KubeMQConfigurationException(
                $"TLS client certificate file not found: {CertFile}");
        }

        if (KeyFile is not null && !File.Exists(KeyFile))
        {
            throw new KubeMQConfigurationException(
                $"TLS client key file not found: {KeyFile}");
        }

        if (CaFile is not null && !File.Exists(CaFile))
        {
            throw new KubeMQConfigurationException(
                $"TLS CA certificate file not found: {CaFile}");
        }

        if (CertFile is not null && KeyFile is null)
        {
            throw new KubeMQConfigurationException(
                "TLS client certificate file provided without key file");
        }

        if (KeyFile is not null && CertFile is null)
        {
            throw new KubeMQConfigurationException(
                "TLS client key file provided without certificate file");
        }

        if (ClientCertificatePem is not null && ClientKeyPem is null)
        {
            throw new KubeMQConfigurationException(
                "TLS client certificate PEM provided without key PEM");
        }

        if (ClientKeyPem is not null && ClientCertificatePem is null)
        {
            throw new KubeMQConfigurationException(
                "TLS client key PEM provided without certificate PEM");
        }

        if ((MinTlsVersion & SslProtocols.Tls12) == 0 &&
            (MinTlsVersion & SslProtocols.Tls13) == 0)
        {
            throw new KubeMQConfigurationException(
                "MinTlsVersion must include at least TLS 1.2 (HTTP/2 requirement)");
        }
    }
}
