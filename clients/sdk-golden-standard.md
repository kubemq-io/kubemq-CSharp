# KubeMQ SDK Golden Standard

## Purpose

This document defines the target quality requirements for all KubeMQ client SDKs. Every SDK must meet these requirements to achieve production-grade status. The requirements define **what** each SDK must do (behavior, contracts, capabilities) — not **how** (each language uses idiomatic patterns).

## Guiding Principles

Adapted from the [Azure SDK Design Guidelines](https://azure.github.io/azure-sdk/general_introduction.html):

1. **Idiomatic** — Each SDK feels natural in its language. Go uses functional options, Java uses builders, Python uses kwargs, C# uses async/await, JS/TS uses options objects.
2. **Consistent** — Same behavior across all SDKs. Retry counts, error types, feature sets, timeout defaults are identical. Only the API surface adapts to the language.
3. **Approachable** — A developer can send their first message in under 5 minutes with 3 lines of code. Complexity is opt-in via progressive disclosure.
4. **Diagnosable** — When something goes wrong, errors tell you what failed, why, and how to fix it. OpenTelemetry integration provides full observability.
5. **Compatible** — Backward compatibility within major versions. Breaking changes only in major releases with migration guides. Deprecated APIs have minimum 2 minor versions notice.

## Reference SDK Strategy

**Java** (currently scored 3.10/5.0, highest across all SDKs) serves as the reference implementation. The workflow:

1. Define requirements (this document set)
2. Implement in Java first — validate that requirements are achievable and well-specified
3. Port patterns to Go, Python, C#, JS/TS — using Java as the concrete reference

## Target Score

**4.0+ across all 13 categories** for every SDK. This represents enterprise-grade quality matching Azure/AWS SDK standards.

## Consistency Model

**Behavioral parity** — All SDKs exhibit identical behavior (retry policies, error classification, feature coverage, timeout defaults) but use language-idiomatic APIs and patterns. This is the Azure SDK model.

## Transport

**gRPC only** — gRPC is the sole required transport for all SDKs. REST/WebSocket support is not required and should be removed from SDKs where it exists to reduce maintenance burden.

## Tier Definitions

### Tier 1 — Must-Have (thorough requirements, concrete acceptance criteria)

These categories are gate blockers. A score below 3.0 in any Tier 1 category caps the overall SDK score.

| # | Category | Current Avg | Target | Doc |
|---|----------|-------------|--------|-----|
| 1 | Error Handling & Resilience | 2.10 | 4.0+ | [01-error-handling.md](golden-standard/01-error-handling.md) |
| 2 | Connection & Transport | 2.70 | 4.0+ | [02-connection-transport.md](golden-standard/02-connection-transport.md) |
| 3 | Auth & Security | 2.29 | 4.0+ | [03-auth-security.md](golden-standard/03-auth-security.md) |
| 4 | Testing | 1.93 | 4.0+ | [04-testing.md](golden-standard/04-testing.md) |
| 5 | Observability | 1.46 | 4.0+ | [05-observability.md](golden-standard/05-observability.md) |
| 6 | Documentation | 2.68 | 4.0+ | [06-documentation.md](golden-standard/06-documentation.md) |
| 7 | Code Quality & Architecture | 2.85 | 4.0+ | [07-code-quality.md](golden-standard/07-code-quality.md) |

### Tier 2 — Should-Have (lighter requirements, "good enough" bar)

Important for overall quality but not gate blockers on their own.

| # | Category | Current Avg | Target | Doc |
|---|----------|-------------|--------|-----|
| 8 | API Completeness & Feature Parity | 4.10 | 4.0+ | [08-api-completeness.md](golden-standard/08-api-completeness.md) |
| 9 | API Design & DX | 3.28 | 4.0+ | [09-api-design-dx.md](golden-standard/09-api-design-dx.md) |
| 10 | Concurrency & Thread Safety | 3.09 | 4.0+ | [10-concurrency.md](golden-standard/10-concurrency.md) |
| 11 | Packaging & Distribution | 2.94 | 4.0+ | [11-packaging.md](golden-standard/11-packaging.md) |
| 12 | Compatibility, Lifecycle & Supply Chain | 1.62 | 4.0+ | [12-compatibility-lifecycle.md](golden-standard/12-compatibility-lifecycle.md) |
| 13 | Performance | 1.84 | 4.0+ | [13-performance.md](golden-standard/13-performance.md) |

## Cross-SDK Parity Matrix

Maintained as a living document. Each feature row tracks support status across all 5 SDKs.

| Feature | Go | Java | C# | Python | JS/TS | Required Tier |
|---------|-----|------|-----|--------|-------|---------------|
| **Error Handling** | | | | | | |
| Typed error hierarchy | | | | | | Core |
| Error classification (retryable/non-retryable) | | | | | | Core |
| Auto-retry with exponential backoff + jitter | | | | | | Core |
| Configurable retry policy | | | | | | Core |
| Per-operation timeouts | | | | | | Core |
| **Connection** | | | | | | |
| Auto-reconnect with buffering | | | | | | Core |
| Connection state callbacks | | | | | | Core |
| Keepalive configuration | | | | | | Core |
| Graceful shutdown / drain | | | | | | Core |
| **Auth & Security** | | | | | | |
| Token authentication | | | | | | Core |
| TLS encryption | | | | | | Core |
| mTLS (mutual TLS) | | | | | | Core |
| Certificate validation options | | | | | | Core |
| **Observability** | | | | | | |
| OpenTelemetry traces (optional dep) | | | | | | Core |
| OpenTelemetry metrics (optional dep) | | | | | | Core |
| W3C Trace Context propagation | | | | | | Core |
| Structured logging hooks | | | | | | Core |
| **Testing** | | | | | | |
| Unit tests (≥80% coverage) | | | | | | Core |
| Integration tests (real server) | | | | | | Core |
| CI pipeline (GitHub Actions) | | | | | | Core |
| **Documentation** | | | | | | |
| Auto-generated API reference | | | | | | Core |
| README with quickstart | | | | | | Core |
| Per-pattern code examples | | | | | | Core |
| Troubleshooting guide | | | | | | Core |
| **Messaging Patterns** | | | | | | |
| Events (pub/sub) | | | | | | Core |
| Events Store (persistent pub/sub) | | | | | | Core |
| Queue stream upstream (send via stream) | | | | | | Core |
| Queue stream downstream (receive/ack/reject) | | | | | | Core |
| Queue DLQ, delay, expiry | | | | | | Core |
| RPC Commands (fire-and-forget) | | | | | | Core |
| RPC Queries (request-reply) | | | | | | Core |
| Queue simple send/receive (non-stream) | | | | | | Extended |
| Queue peek, batch | | | | | | Extended |

**Legend:** Empty = not assessed yet. Fill with: ✅ Compliant | ⚠️ Partial | ❌ Missing | N/A

## Current Status vs Target

| SDK | Current Score | Target | Gap |
|-----|--------------|--------|-----|
| Java (reference) | 3.10 | 4.0+ | +0.90 |
| Python | 2.96 | 4.0+ | +1.04 |
| Go | 2.47 | 4.0+ | +1.53 |
| C# / .NET | 2.27 | 4.0+ | +1.73 |
| Node.js / TS | 2.08 | 4.0+ | +1.92 |
