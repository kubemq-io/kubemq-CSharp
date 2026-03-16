using System.Security.Authentication;
using FluentAssertions;
using KubeMQ.Sdk.Config;
using KubeMQ.Sdk.Exceptions;

namespace KubeMQ.Sdk.Tests.Unit.Config;

public sealed class TlsOptionsTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); }
            catch { /* best effort cleanup */ }
        }
    }

    private string CreateTempFile(string content = "dummy")
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new TlsOptions();

        options.Enabled.Should().BeFalse();
        options.CertFile.Should().BeNull();
        options.KeyFile.Should().BeNull();
        options.CaFile.Should().BeNull();
        options.ClientCertificatePem.Should().BeNull();
        options.ClientKeyPem.Should().BeNull();
        options.CaCertificatePem.Should().BeNull();
        options.InsecureSkipVerify.Should().BeFalse();
        options.ServerNameOverride.Should().BeNull();
        options.MinTlsVersion.Should().Be(SslProtocols.Tls12);
    }

    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        var options = new TlsOptions
        {
            Enabled = false,
            CertFile = "/nonexistent/cert.pem",
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenEnabled_WithValidCertAndKey_DoesNotThrow()
    {
        var certFile = CreateTempFile("cert");
        var keyFile = CreateTempFile("key");

        var options = new TlsOptions
        {
            Enabled = true,
            CertFile = certFile,
            KeyFile = keyFile,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenEnabled_CertFileNotFound_ThrowsConfigurationException()
    {
        var options = new TlsOptions
        {
            Enabled = true,
            CertFile = "/nonexistent/cert.pem",
            KeyFile = "/some/key.pem",
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*client certificate file not found*");
    }

    [Fact]
    public void Validate_WhenEnabled_KeyFileNotFound_ThrowsConfigurationException()
    {
        var certFile = CreateTempFile("cert");

        var options = new TlsOptions
        {
            Enabled = true,
            CertFile = certFile,
            KeyFile = "/nonexistent/key.pem",
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*client key file not found*");
    }

    [Fact]
    public void Validate_WhenEnabled_CaFileNotFound_ThrowsConfigurationException()
    {
        var options = new TlsOptions
        {
            Enabled = true,
            CaFile = "/nonexistent/ca.pem",
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*CA certificate file not found*");
    }

    [Fact]
    public void Validate_WhenEnabled_CertWithoutKey_ThrowsConfigurationException()
    {
        var certFile = CreateTempFile("cert");

        var options = new TlsOptions
        {
            Enabled = true,
            CertFile = certFile,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*certificate file provided without key file*");
    }

    [Fact]
    public void Validate_WhenEnabled_KeyWithoutCert_ThrowsConfigurationException()
    {
        var keyFile = CreateTempFile("key");

        var options = new TlsOptions
        {
            Enabled = true,
            KeyFile = keyFile,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*key file provided without certificate file*");
    }

    [Fact]
    public void Validate_WhenEnabled_ClientPemWithoutKeyPem_ThrowsConfigurationException()
    {
        var options = new TlsOptions
        {
            Enabled = true,
            ClientCertificatePem = "-----BEGIN CERTIFICATE-----",
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*certificate PEM provided without key PEM*");
    }

    [Fact]
    public void Validate_WhenEnabled_KeyPemWithoutClientPem_ThrowsConfigurationException()
    {
        var options = new TlsOptions
        {
            Enabled = true,
            ClientKeyPem = "-----BEGIN PRIVATE KEY-----",
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*key PEM provided without certificate PEM*");
    }

    [Fact]
    public void Validate_WhenEnabled_PemPairSet_DoesNotThrow()
    {
        var options = new TlsOptions
        {
            Enabled = true,
            ClientCertificatePem = "-----BEGIN CERTIFICATE-----",
            ClientKeyPem = "-----BEGIN PRIVATE KEY-----",
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(SslProtocols.Tls12)]
    [InlineData(SslProtocols.Tls13)]
    [InlineData(SslProtocols.Tls12 | SslProtocols.Tls13)]
    public void Validate_WhenEnabled_ValidMinTlsVersion_DoesNotThrow(SslProtocols protocol)
    {
        var options = new TlsOptions
        {
            Enabled = true,
            MinTlsVersion = protocol,
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

#pragma warning disable SYSLIB0039 // SslProtocols.Tls11 is obsolete
    [Fact]
    public void Validate_WhenEnabled_MinTlsVersionBelowTls12_ThrowsConfigurationException()
    {
        var options = new TlsOptions
        {
            Enabled = true,
            MinTlsVersion = SslProtocols.Tls11,
        };

        var act = () => options.Validate();

        act.Should().Throw<KubeMQConfigurationException>()
            .WithMessage("*MinTlsVersion must include at least TLS 1.2*");
    }
#pragma warning restore SYSLIB0039

    [Fact]
    public void InsecureSkipVerify_CanBeSet()
    {
        var options = new TlsOptions { InsecureSkipVerify = true };

        options.InsecureSkipVerify.Should().BeTrue();
    }

    [Fact]
    public void HasClientCertificate_WithFilePair_ReturnsTrue()
    {
        var options = new TlsOptions
        {
            CertFile = "/some/cert.pem",
            KeyFile = "/some/key.pem",
        };

        options.HasClientCertificate.Should().BeTrue();
    }

    [Fact]
    public void HasClientCertificate_WithPemPair_ReturnsTrue()
    {
        var options = new TlsOptions
        {
            ClientCertificatePem = "cert-pem",
            ClientKeyPem = "key-pem",
        };

        options.HasClientCertificate.Should().BeTrue();
    }

    [Fact]
    public void HasClientCertificate_WithNone_ReturnsFalse()
    {
        var options = new TlsOptions();

        options.HasClientCertificate.Should().BeFalse();
    }

    [Fact]
    public void HasCaCertificate_WithCaFile_ReturnsTrue()
    {
        var options = new TlsOptions { CaFile = "/some/ca.pem" };

        options.HasCaCertificate.Should().BeTrue();
    }

    [Fact]
    public void HasCaCertificate_WithCaPem_ReturnsTrue()
    {
        var options = new TlsOptions { CaCertificatePem = "ca-pem" };

        options.HasCaCertificate.Should().BeTrue();
    }

    [Fact]
    public void HasCaCertificate_WithNone_ReturnsFalse()
    {
        var options = new TlsOptions();

        options.HasCaCertificate.Should().BeFalse();
    }
}
