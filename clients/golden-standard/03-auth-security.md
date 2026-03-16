# Category 3: Auth & Security

**Tier:** 1 (Critical — gate blocker)
**Current Average Score:** 2.29 / 5.0
**Target Score:** 4.0+
**Weight:** 9%

## Purpose

SDKs must support enterprise authentication and encryption requirements. Token auth, TLS, and mTLS must work out of the box. Security configuration must be simple but not simplistic — insecure options must require explicit opt-in.

---

## Requirements

### REQ-AUTH-1: Token Authentication

The SDK must support JWT and static token authentication.

**Acceptance criteria:**
- [ ] Static token can be provided via client options/builder
- [ ] Token is sent as gRPC metadata on every request
- [ ] Token can be updated without recreating the client (rotation support)
- [ ] Missing token when server requires auth produces a clear `AuthenticationError`
- [ ] Token is never logged (even at DEBUG level)

### REQ-AUTH-2: TLS Encryption

The SDK must support TLS for encrypting all communication.

**Localhost vs. remote default:** "Localhost" means addresses matching `localhost`, `127.0.0.1`, `::1`, or `[::1]`. All other addresses are "remote". In-cluster Kubernetes DNS names (e.g., `kubemq.default.svc.cluster.local`) are treated as remote.

**Configuration options:**

| Option | Description |
|--------|-------------|
| Enable TLS | Enable/disable TLS (default: false for localhost, true for remote) |
| CA certificate | Path to CA certificate file or PEM bytes |
| Server name override | Override for TLS server name verification |
| InsecureSkipVerify | Disable certificate verification (dev only) — see below |
| Minimum TLS version | Minimum acceptable TLS version (default: TLS 1.2) |

**Hostname verification:** Hostname verification uses platform TLS defaults. When connecting by IP address, the server certificate must include the IP in its SAN list. The `ServerNameOverride` option overrides the expected hostname for verification. Document this behavior with a Kubernetes example (connecting to a pod IP vs. service DNS name).

**InsecureSkipVerify:** `InsecureSkipVerify` MUST be a separately named option (e.g., `WithInsecureSkipVerify()`), not a boolean flag on a generic TLS config. The SDK MUST log a WARNING containing "certificate verification is disabled" on every connection attempt where skip_verify is active.

**TLS handshake failure classification:**
- TLS handshake failure due to certificate validation (expired, untrusted, hostname mismatch) = `AuthenticationError`, non-retryable.
- TLS handshake failure due to network error = `TransientError`, retryable.
- TLS version/cipher negotiation failure = `ConfigurationError`, non-retryable.

**Acceptance criteria:**
- [ ] TLS can be enabled with a single option (e.g., `WithTLS(true)`)
- [ ] Custom CA certificates can be provided (file path or PEM bytes)
- [ ] Server name override is supported for environments with mismatched certs
- [ ] `InsecureSkipVerify` is exposed as a separately named option (e.g., `WithInsecureSkipVerify()`)
- [ ] The SDK logs a WARNING containing "certificate verification is disabled" on every connection attempt where skip_verify is active
- [ ] TLS 1.2 is the minimum enforced version (HTTP/2 requires it)
- [ ] System CA bundle is used by default when TLS is enabled without custom CA
- [ ] TLS handshake failures are classified per the failure classification above

### REQ-AUTH-3: Mutual TLS (mTLS)

The SDK must support mutual TLS where both client and server authenticate via certificates.

**Configuration options:**

| Option | Description |
|--------|-------------|
| Client certificate | Path to client certificate file or PEM bytes |
| Client private key | Path to client private key file or PEM bytes |
| CA certificate | Path to CA certificate for verifying the server |

**Acceptance criteria:**
- [ ] mTLS can be configured via client options/builder with 3 parameters (client cert, client key, CA cert)
- [ ] Both file paths and in-memory PEM bytes are accepted
- [ ] Invalid certificates produce clear error messages at connection time (fail-fast)
- [ ] Certificate errors are classified as `AuthenticationError` (non-retryable)
- [ ] mTLS configuration is documented with examples
- [ ] On reconnection, the SDK MUST reload TLS credentials (certificates and keys) from the originally configured source (file path or PEM provider callback)
- [ ] If certificate files have changed on disk since the last connection, the new certificates are used
- [ ] Documentation includes an example showing certificate loading from environment variables via the PEM bytes API

### REQ-AUTH-4: Credential Provider Interface

The SDK must define a pluggable credential provider interface for extensibility.

**Interface contract:**
```
CredentialProvider:
  GetToken() -> (token string, expiresAt time, error)
  // expiresAt is optional (zero value means no expiry hint)
  // When provided, the SDK uses it for proactive refresh scheduling
```

**Token refresh lifecycle:**
- **Reactive refresh:** On server `UNAUTHENTICATED` response, invalidate the cached token and re-invoke the provider.
- **Proactive refresh (RECOMMENDED):** When `expiresAt` is provided and the token is approaching expiry, refresh before expiry to prevent unnecessary request failures.

**Serialization:** The SDK MUST serialize calls to the credential provider (at most one outstanding `GetToken()` call at a time). The SDK caches the returned token and re-invokes the provider only when the cache is invalidated. Document this guarantee for provider implementers.

**Invocation triggers:** The provider is called when no cached token exists, when the cached token is invalidated by a server `UNAUTHENTICATED` response, or when proactive refresh determines the token is approaching expiry.

**Connection state interaction:** The credential provider is invoked during both initial connection (`CONNECTING`) and reconnection (`RECONNECTING`). Provider invocation MUST be subject to the connection/reconnection timeout (not a separate timeout). Provider failure during `CONNECTING` follows the same policy as connection failure. Provider failure during `RECONNECTING` counts as a failed reconnection attempt and follows the backoff policy from REQ-CONN-1.

**Error classification:** Provider errors indicating invalid/expired credentials are wrapped as `AuthenticationError` (non-retryable). Provider errors indicating transient infrastructure failures (e.g., credential store unavailable, network timeout) are wrapped as `TransientError` (retryable). The `CredentialProvider` interface SHOULD support typed/categorized errors to enable this distinction.

**OIDC and enterprise identity:** The `CredentialProvider` interface covers OIDC, Vault, cloud IAM, and other enterprise identity systems by design. SDK documentation MUST include a worked example of an OIDC credential provider using the interface. This satisfies OIDC integration requirements without coupling the SDK to any specific OIDC library.

**Acceptance criteria:**
- [ ] A `CredentialProvider` interface/type is defined with `GetToken()` returning `(token, expiresAt, error)`
- [ ] Static token is implemented as a built-in provider
- [ ] Users can implement custom providers (e.g., vault integration, OIDC)
- [ ] Reactive refresh: on `UNAUTHENTICATED` response, cached token is invalidated and provider is re-invoked
- [ ] Proactive refresh (RECOMMENDED): when `expiresAt` is provided, token is refreshed before expiry
- [ ] Calls to the credential provider are serialized (at most one outstanding call at a time)
- [ ] Provider is invoked during both `CONNECTING` and `RECONNECTING` states, subject to connection timeout
- [ ] Provider errors are classified: credential errors as `AuthenticationError`, infrastructure errors as `TransientError`
- [ ] SDK documentation includes a worked example of an OIDC credential provider

### REQ-AUTH-5: Security Best Practices

**Acceptance criteria:**
- [ ] Credentials MUST be excluded from: log messages at all levels, error message strings, OpenTelemetry span attributes, gRPC metadata captured by interceptors/middleware, and `String()`/`toString()` representations
- [ ] When auth state is needed for debugging, log only `token_present: true/false`, never the value
- [ ] TLS certificate files are validated at construction time (fail-fast)
- [ ] Documentation includes a security configuration guide with examples for each auth method
- [ ] Insecure options (`skip_verify`) emit a warning log on every connection

### REQ-AUTH-6: TLS Credentials During Reconnection

The SDK must reload TLS credentials during reconnection to support certificate rotation (e.g., cert-manager in Kubernetes).

**Acceptance criteria:**
- [ ] On reconnection, the SDK reloads certificates from the configured source (file path or PEM provider callback)
- [ ] If the certificate source returns an error (file missing, permission denied), the error is treated as transient and retried per reconnection backoff
- [ ] If TLS handshake fails after successful cert reload, the failure is classified per REQ-AUTH-2 rules
- [ ] Certificate reload is logged at DEBUG level
- [ ] Certificate reload errors are logged at ERROR level

**Cross-references:** REQ-CONN-1 (reconnection lifecycle), REQ-AUTH-2 (TLS handshake failure classification), REQ-AUTH-3 (mTLS credential reload).

---

## What 4.0+ Looks Like

- Token auth, TLS, and mTLS all work with clear, concise configuration
- Certificate/token errors produce actionable messages ("Certificate expired on 2026-01-15, renew it")
- mTLS requires exactly 3 files — no ceremony beyond providing cert paths
- Credential provider interface allows enterprise integrations (Vault, cloud IAM, OIDC) with a pluggable `GetToken()` returning token + optional expiry hint
- Proactive token refresh prevents unnecessary request failures when expiry hints are available
- TLS credentials are automatically reloaded on reconnection, supporting cert-manager rotation workflows
- TLS handshake failures are classified (auth vs. transient vs. config) so reconnection logic makes correct retry decisions
- Security-sensitive values never appear in logs, error messages, OTel spans, or interceptor metadata
- `InsecureSkipVerify` requires explicit opt-in via a dedicated API and warns on every connection
- Documentation includes working examples for each auth method, including OIDC provider, env-var cert loading, and Kubernetes hostname verification
