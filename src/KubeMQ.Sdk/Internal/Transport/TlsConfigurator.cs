using System;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Logging;
using Microsoft.Extensions.Logging;

namespace KubeMQ.Sdk.Internal.Transport;

/// <summary>
/// Configures TLS/SSL options on an existing <see cref="SocketsHttpHandler"/>.
/// <para>
/// <b>Handler ownership:</b> <see cref="GrpcTransport"/> creates and owns
/// the <see cref="SocketsHttpHandler"/> with all settings (keepalive, connection pooling,
/// <c>EnableMultipleHttp2Connections</c>). This class ONLY configures
/// <see cref="SocketsHttpHandler.SslOptions"/> on the handler that GrpcTransport provides.
/// </para>
/// </summary>
internal static class TlsConfigurator
{
    /// <summary>
    /// Configures TLS on an existing handler. Does nothing when TLS is disabled.
    /// </summary>
    /// <param name="handler">The handler created by GrpcTransport.</param>
    /// <param name="tls">TLS options.</param>
    /// <param name="logger">Optional logger for InsecureSkipVerify warnings.</param>
    /// <param name="address">Server address for log messages.</param>
    internal static void ConfigureTls(
        SocketsHttpHandler handler, TlsOptions tls, ILogger? logger, string? address = null)
    {
        if (!tls.Enabled)
        {
            return;
        }

        var sslOptions = new SslClientAuthenticationOptions
        {
            EnabledSslProtocols = tls.MinTlsVersion | SslProtocols.Tls13,
            TargetHost = tls.ServerNameOverride,
        };

        if (tls.HasClientCertificate)
        {
            sslOptions.ClientCertificates = LoadClientCertificates(tls);
        }

        if (tls.HasCaCertificate)
        {
#pragma warning disable CA2000 // CA cert is captured by the validation callback and lives as long as the handler
            X509Certificate2? caCert = LoadCaCertificate(tls);
#pragma warning restore CA2000
            sslOptions.RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
            {
                if (errors == SslPolicyErrors.None)
                {
                    return true;
                }

                if (chain is not null && caCert is not null)
                {
                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(caCert);
                    return chain.Build(new X509Certificate2(cert!));
                }

                return false;
            };
        }

        if (tls.InsecureSkipVerify)
        {
            if (tls.HasCaCertificate && logger is not null)
            {
                Log.InsecureOverridesCa(logger, address ?? "unknown");
            }

            if (logger is not null)
            {
                Log.InsecureConnection(logger, address ?? "unknown");
            }

#pragma warning disable CA5359 // Intentional: InsecureSkipVerify is an explicit opt-in by the user
            sslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
        }

        handler.SslOptions = sslOptions;
    }

    internal static X509CertificateCollection LoadClientCertificates(TlsOptions tls)
    {
        try
        {
            X509Certificate2 cert;
            if (tls.CertFile is not null && tls.KeyFile is not null)
            {
                cert = X509Certificate2.CreateFromPemFile(tls.CertFile, tls.KeyFile);
            }
            else if (tls.ClientCertificatePem is not null && tls.ClientKeyPem is not null)
            {
                cert = X509Certificate2.CreateFromPem(
                    tls.ClientCertificatePem,
                    tls.ClientKeyPem);
            }
            else
            {
                return new X509CertificateCollection();
            }

            return new X509CertificateCollection { cert };
        }
        catch (Exception ex) when (ex is not KubeMQException)
        {
            throw new KubeMQConfigurationException(
                $"Failed to load TLS client certificate: {ex.Message}", ex);
        }
    }

    internal static X509Certificate2? LoadCaCertificate(TlsOptions tls)
    {
        try
        {
            if (tls.CaFile is not null)
            {
                return new X509Certificate2(tls.CaFile);
            }

            if (tls.CaCertificatePem is not null)
            {
                return X509Certificate2.CreateFromPem(tls.CaCertificatePem);
            }

            return null;
        }
        catch (Exception ex) when (ex is not KubeMQException)
        {
            throw new KubeMQConfigurationException(
                $"Failed to load TLS CA certificate: {ex.Message}", ex);
        }
    }
}
