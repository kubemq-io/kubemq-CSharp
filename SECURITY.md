# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 3.x     | Active             |
| 2.x     | End of life        |
| 1.x     | End of life        |

## Reporting a Vulnerability

If you discover a security vulnerability, please report it responsibly:

1. **Do NOT** open a public GitHub issue for security vulnerabilities
2. Email [security@kubemq.io](mailto:security@kubemq.io) with:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
3. You will receive an acknowledgment within 48 hours
4. A fix will be developed and released as a patch version

## Security Best Practices

When using the KubeMQ .NET SDK:

- Always use TLS in production (`Tls = new TlsOptions { Enabled = true }`)
- Never log or expose authentication tokens
- Rotate JWT tokens regularly
- Use the latest SDK version to receive security patches
- See the [TLS Setup Example](examples/Config/Config.TlsSetup/) for secure configuration
