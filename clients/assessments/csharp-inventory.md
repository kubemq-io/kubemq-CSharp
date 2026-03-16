# KubeMQ C# / .NET SDK — Codebase Inventory

**Date:** 2026-03-11
**Path:** /Users/liornabat/development/projects/kubemq/clients/kubemq-csharp
**SDK Version:** 3.0.0
**Target Framework:** net8.0, C# 12.0
**Package ID:** KubeMQ.SDK.CSharp
**License:** Apache-2.0

## Directory Structure (Top 2 Levels)

```
.
├── .claude/skills/          — Agent skills
├── .github/workflows/       — CI (ci.yml, release.yml)
├── .vscode/                 — VS Code settings
├── Archive/                 — Legacy v1/v2 code (20 subdirs)
├── Examples/                — v3 examples
│   ├── CQ/                  — Legacy CQ examples
│   ├── Commands/            — Command send/handle
│   ├── Config/              — TLS, mTLS, token auth, custom timeouts
│   ├── Events/              — Basic pub/sub, wildcards, multiple subscribers
│   ├── EventsStore/         — Persistent pub/sub, replay
│   ├── Observability/       — OpenTelemetry example
│   ├── PubSub/              — Legacy pub/sub examples
│   ├── Queries/             — Send query, handle query, cached response
│   └── Queues/              — Send/receive, batch, DLQ, delayed, visibility timeout, ack/reject
├── KubeMQ.SDK.csharp/       — Legacy v2 SDK source
├── benchmarks/              — BenchmarkDotNet benchmarks
├── clients/                 — Assessment framework, golden standard
├── docfx/                   — DocFX API documentation config
├── docs/                    — Generated docs
├── src/KubeMQ.Sdk/          — v3 SDK source (main)
└── tests/KubeMQ.Sdk.Tests.Unit/ — Unit tests
```

## File Counts

| Type | Count |
|------|-------|
| Source files (.cs in src/) | 90 (incl. obj/ generated) |
| Core source files (excl. obj/) | ~70 |
| Test files (.cs in tests/) | 21 (incl. obj/ generated) |
| Core test files (excl. obj/) | ~15 |
| Example files (.cs) | 93 |
| Documentation files (.md) | 7 (README, CHANGELOG, CONTRIBUTING, SECURITY, TROUBLESHOOTING, MIGRATION-v3, COMPATIBILITY) |
| Total .cs files (all) | 362 |

## Source Code Structure (src/KubeMQ.Sdk/)

### Public API
- `Client/` — IKubeMQClient, KubeMQClient, KubeMQClientOptions
- `Commands/` — CommandMessage, CommandReceived, CommandResponse, CommandsSubscription
- `Queries/` — QueryMessage, QueryReceived, QueryResponse, QueriesSubscription
- `Events/` — EventMessage, EventReceived, EventsSubscription, SubscribeType
- `EventsStore/` — EventStoreMessage, EventStoreReceived, EventStoreStartPosition, EventStoreSubscription
- `Queues/` — QueueMessage, QueueMessageReceived, QueuePollRequest, QueuePollResponse, QueueSendResult
- `Auth/` — ICredentialProvider, StaticTokenProvider, CredentialResult
- `Common/` — ChannelInfo, ChannelStats, ConnectionState, ServerInfo
- `Config/` — TlsOptions, RetryPolicy, ReconnectOptions, KeepaliveOptions, SubscriptionOptions, BufferFullMode, JitterMode
- `Exceptions/` — Rich exception hierarchy (10 exception classes with error codes and categories)
- `DependencyInjection/` — ASP.NET Core DI extensions, hosted service

### Internal
- `Internal/Transport/` — ConnectionManager, GrpcTransport, StreamManager, StateMachine, ReconnectBuffer, TlsConfigurator, etc.
- `Internal/Protocol/` — AuthInterceptor, GrpcErrorMapper, MessageValidator, RetryHandler, TelemetryInterceptor
- `Internal/Telemetry/` — KubeMQActivitySource, KubeMQMetrics, SemanticConventions, TextMapCarrier
- `Internal/Logging/` — Structured logging via Log.cs

### Proto
- `Proto/kubemq.proto` — gRPC service definition

## Test Structure (tests/KubeMQ.Sdk.Tests.Unit/)

- `Client/` — KubeMQClientLifecycleTests, PublishTests, CommandQueryTests, QueueTests, ChannelTests
- `Config/` — KubeMQClientOptionsTests, RetryPolicyValidationTests
- `ErrorClassification/` — GrpcErrorMapperTests
- `Exceptions/` — ExceptionHierarchyTests
- `Protocol/` — AuthInterceptorTests, RetryHandlerTests
- `Telemetry/` — TelemetryTests
- `Transport/` — ConnectionManagerTests
- `Validation/` — MessageValidatorTests
- `Helpers/` — TestClientFactory

## Package Dependencies

| Package | Version |
|---------|---------|
| Google.Protobuf | 3.* |
| Grpc.Net.Client | 2.* |
| Grpc.Tools | 2.* (build-time) |
| Microsoft.Extensions.Logging.Abstractions | 8.* |
| Microsoft.Extensions.Configuration.Abstractions | 8.* |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.* |
| Microsoft.Extensions.Hosting.Abstractions | 8.* |
| Microsoft.Extensions.Options | 8.* |
| Microsoft.Extensions.Options.ConfigurationExtensions | 8.* |
| Microsoft.CodeAnalysis.NetAnalyzers | 9.* (build-time) |
| StyleCop.Analyzers | 1.2.0-beta.556 (build-time) |
| Microsoft.SourceLink.GitHub | 8.* (build-time) |

## CI/CD

- `.github/workflows/ci.yml` — CI pipeline
- `.github/workflows/release.yml` — Release pipeline
- `.codecov.yml` — Code coverage config

## Documentation Files

| File | Lines | Content |
|------|-------|---------|
| README.md | 326 | Comprehensive: install, quick start, patterns, config, error handling, compatibility |
| CHANGELOG.md | 91 | Version history |
| CONTRIBUTING.md | 65 | Build instructions, code style, PR requirements |
| SECURITY.md | 31 | Security policy |
| TROUBLESHOOTING.md | 332 | 11+ common issues with solutions |
| MIGRATION-v3.md | 256 | v2 → v3 migration guide |
| COMPATIBILITY.md | 83 | Server/platform compatibility matrix |

## Cookbook Repository

- Path: /tmp/kubemq-csharp-cookbook
- Structure: client/, pubsub/, queue/, rpc/
- C# files: 32
- README: 1

## Key Design Decisions

- **Single client class** (KubeMQClient) with methods for all messaging patterns
- **IAsyncEnumerable** for subscription streams
- **IAsyncDisposable** for resource cleanup
- **Rich exception hierarchy** with error codes and retryable classification
- **ASP.NET Core DI integration** via AddKubeMQ extensions
- **OpenTelemetry** integration for distributed tracing and metrics
- **Reconnection with buffering** — messages buffered during reconnection
- **.NET 8.0 only** (LTS target)
