using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;
using KubeMQ.Sdk.Internal.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace KubeMQ.Sdk.Tests.Unit.Transport;

public sealed class TlsConfiguratorTests : IDisposable
{
    private readonly SocketsHttpHandler _handler = new();
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        _handler.Dispose();
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); } catch { }
        }
    }

    private (string CertPath, string KeyPath) CreateTempPemCertAndKey()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=TestCert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));

        var certPem = cert.ExportCertificatePem();
        var keyPem = rsa.ExportRSAPrivateKeyPem();

        var certPath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        File.WriteAllText(certPath, certPem);
        File.WriteAllText(keyPath, keyPem);
        _tempFiles.Add(certPath);
        _tempFiles.Add(keyPath);
        return (certPath, keyPath);
    }

    private string CreateTempCaCertFile()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=TestCA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var caCert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));

        var caPath = Path.GetTempFileName();
        File.WriteAllBytes(caPath, caCert.Export(X509ContentType.Cert));
        _tempFiles.Add(caPath);
        return caPath;
    }

    [Fact]
    public void ConfigureTls_Disabled_DoesNotModifySslOptions()
    {
        var tls = new TlsOptions { Enabled = false };
        var originalSsl = _handler.SslOptions;

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.Should().BeSameAs(originalSsl);
    }

    [Fact]
    public void ConfigureTls_Enabled_SetsSslOptions()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
            MinTlsVersion = SslProtocols.Tls12,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.Should().NotBeNull();
        _handler.SslOptions.EnabledSslProtocols.Should().HaveFlag(SslProtocols.Tls13);
        _handler.SslOptions.EnabledSslProtocols.Should().HaveFlag(SslProtocols.Tls12);
    }

    [Fact]
    public void ConfigureTls_InsecureSkipVerify_SetsCallbackThatAlwaysReturnsTrue()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
            InsecureSkipVerify = true,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance, "test-address");

        _handler.SslOptions.RemoteCertificateValidationCallback.Should().NotBeNull();
        _handler.SslOptions.RemoteCertificateValidationCallback!(null, null, null, default)
            .Should().BeTrue();
    }

    [Fact]
    public void ConfigureTls_ServerNameOverride_SetsTargetHost()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
            ServerNameOverride = "my-server.example.com",
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.TargetHost.Should().Be("my-server.example.com");
    }

    [Fact]
    public void ConfigureTls_InsecureSkipVerify_WithNullLogger_DoesNotThrow()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
            InsecureSkipVerify = true,
        };

        Action act = () => TlsConfigurator.ConfigureTls(_handler, tls, null, "test");

        act.Should().NotThrow();
        _handler.SslOptions.RemoteCertificateValidationCallback!(null, null, null, default)
            .Should().BeTrue();
    }

    [Fact]
    public void LoadCaCertificate_NullPaths_ReturnsNull()
    {
        var tls = new TlsOptions();

        var result = TlsConfigurator.LoadCaCertificate(tls);

        result.Should().BeNull();
    }

    [Fact]
    public void LoadCaCertificate_InvalidFilePath_ThrowsConfigurationException()
    {
        var tls = new TlsOptions { CaFile = "/nonexistent/path/ca.pem" };

        Action act = () => TlsConfigurator.LoadCaCertificate(tls);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Failed to load TLS CA certificate*");
    }

    [Fact]
    public void LoadClientCertificates_NullPaths_ReturnsEmptyCollection()
    {
        var tls = new TlsOptions();

        var result = TlsConfigurator.LoadClientCertificates(tls);

        result.Count.Should().Be(0);
    }

    [Fact]
    public void LoadClientCertificates_InvalidFilePath_ThrowsConfigurationException()
    {
        var tls = new TlsOptions
        {
            CertFile = "/nonexistent/cert.pem",
            KeyFile = "/nonexistent/key.pem",
        };

        Action act = () => TlsConfigurator.LoadClientCertificates(tls);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Failed to load TLS client certificate*");
    }

    [Fact]
    public void LoadClientCertificates_InvalidPemStrings_ThrowsConfigurationException()
    {
        var tls = new TlsOptions
        {
            ClientCertificatePem = "not-a-valid-pem",
            ClientKeyPem = "not-a-valid-key",
        };

        Action act = () => TlsConfigurator.LoadClientCertificates(tls);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Failed to load TLS client certificate*");
    }

    [Fact]
    public void LoadCaCertificate_InvalidPemString_ThrowsConfigurationException()
    {
        var tls = new TlsOptions
        {
            CaCertificatePem = "not-a-valid-ca-pem",
        };

        Action act = () => TlsConfigurator.LoadCaCertificate(tls);

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*Failed to load TLS CA certificate*");
    }

    [Fact]
    public void ConfigureTls_Enabled_NoClientCert_NoValidationCallback()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.ClientCertificates.Should().BeNull();
        _handler.SslOptions.RemoteCertificateValidationCallback.Should().BeNull();
    }

    [Fact]
    public void ConfigureTls_InsecureSkipVerify_OverridesCaValidation()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
            InsecureSkipVerify = true,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.RemoteCertificateValidationCallback!(null, null, null, default)
            .Should().BeTrue("InsecureSkipVerify overrides all validation");
    }

    [Fact]
    public void ConfigureTls_WithCaFile_SetsRemoteCertificateValidationCallback()
    {
        var caPath = CreateTempCaCertFile();
        var tls = new TlsOptions
        {
            Enabled = true,
            CaFile = caPath,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.RemoteCertificateValidationCallback.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureTls_WithCaFile_CallbackReturnsTrueForNoPolicyErrors()
    {
        var caPath = CreateTempCaCertFile();
        var tls = new TlsOptions
        {
            Enabled = true,
            CaFile = caPath,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        var result = _handler.SslOptions.RemoteCertificateValidationCallback!(
            null, null, null, SslPolicyErrors.None);

        result.Should().BeTrue();
    }

    [Fact]
    public void ConfigureTls_WithCaFile_CallbackReturnsFalseForNullChain()
    {
        var caPath = CreateTempCaCertFile();
        var tls = new TlsOptions
        {
            Enabled = true,
            CaFile = caPath,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        var result = _handler.SslOptions.RemoteCertificateValidationCallback!(
            null, null, null, SslPolicyErrors.RemoteCertificateChainErrors);

        result.Should().BeFalse();
    }

    [Fact]
    public void ConfigureTls_WithServerNameOverride_SetsTargetHostOnSslOptions()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
            ServerNameOverride = "custom.server.name",
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.TargetHost.Should().Be("custom.server.name");
    }

    [Fact]
    public void LoadClientCertificates_WithPemFiles_ReturnsOneCertificate()
    {
        var (certPath, keyPath) = CreateTempPemCertAndKey();
        var tls = new TlsOptions
        {
            CertFile = certPath,
            KeyFile = keyPath,
        };

        var certs = TlsConfigurator.LoadClientCertificates(tls);

        certs.Count.Should().Be(1);
    }

    [Fact]
    public void LoadClientCertificates_WithPemStrings_ReturnsOneCertificate()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=InlinePem", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));

        var tls = new TlsOptions
        {
            ClientCertificatePem = cert.ExportCertificatePem(),
            ClientKeyPem = rsa.ExportRSAPrivateKeyPem(),
        };

        var certs = TlsConfigurator.LoadClientCertificates(tls);

        certs.Count.Should().Be(1);
    }

    [Fact]
    public void LoadCaCertificate_WithValidFile_ReturnsCertificate()
    {
        var caPath = CreateTempCaCertFile();
        var tls = new TlsOptions { CaFile = caPath };

        var result = TlsConfigurator.LoadCaCertificate(tls);

        result.Should().NotBeNull();
        result!.Subject.Should().Contain("CN=TestCA");
    }

    [Fact]
    public void LoadCaCertificate_WithValidPemString_ReturnsCertificate()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=PemCA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var caCert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));

        var tls = new TlsOptions { CaCertificatePem = caCert.ExportCertificatePem() };

        var result = TlsConfigurator.LoadCaCertificate(tls);

        result.Should().NotBeNull();
        result!.Subject.Should().Contain("CN=PemCA");
    }

    [Fact]
    public void ConfigureTls_WithClientCertFiles_SetsClientCertificates()
    {
        var (certPath, keyPath) = CreateTempPemCertAndKey();
        var tls = new TlsOptions
        {
            Enabled = true,
            CertFile = certPath,
            KeyFile = keyPath,
        };

        TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance);

        _handler.SslOptions.ClientCertificates.Should().NotBeNull();
        _handler.SslOptions.ClientCertificates!.Count.Should().Be(1);
    }

    [Fact]
    public void ConfigureTls_InsecureSkipVerify_WithNullAddress_DoesNotThrow()
    {
        var tls = new TlsOptions
        {
            Enabled = true,
            InsecureSkipVerify = true,
        };

        Action act = () => TlsConfigurator.ConfigureTls(_handler, tls, NullLogger.Instance, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void LoadClientCertificates_WithCertFileOnly_NoKeyFile_ReturnsEmptyCollection()
    {
        var tls = new TlsOptions
        {
            CertFile = "/some/cert.pem",
            KeyFile = null,
        };

        var result = TlsConfigurator.LoadClientCertificates(tls);

        result.Count.Should().Be(0);
    }

    [Fact]
    public void LoadClientCertificates_WithCertPemOnly_NoKeyPem_ReturnsEmptyCollection()
    {
        var tls = new TlsOptions
        {
            ClientCertificatePem = "some-cert-pem",
            ClientKeyPem = null,
        };

        var result = TlsConfigurator.LoadClientCertificates(tls);

        result.Count.Should().Be(0);
    }
}
