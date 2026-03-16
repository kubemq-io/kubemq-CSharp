# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [3.0.0] - YYYY-MM-DD

### Added

- **Typed exception hierarchy:** `KubeMQException` base with `KubeMQConnectionException`,
  `KubeMQAuthenticationException`, `KubeMQTimeoutException`, `KubeMQOperationException`,
  `KubeMQConfigurationException`, `KubeMQStreamBrokenException`, `KubeMQBufferFullException`,
  `KubeMQRetryExhaustedException`, `KubeMQPartialFailureException`
- **Automatic retry with exponential backoff:** Configurable via `RetryPolicy` with
  jitter, max retries, and per-operation safety rules
- **Auto-reconnection with buffering:** Configurable via `ReconnectOptions` with
  backoff, buffer size, and connection state events
- **OpenTelemetry integration:** Distributed tracing via `ActivitySource`, metrics via
  `Meter` — no OpenTelemetry NuGet dependency required
- **Structured logging:** `ILoggerFactory` integration with `LoggerMessage` source generators
- **`IDisposable`/`IAsyncDisposable`:** Full resource cleanup via `using`/`await using`
- **`IAsyncEnumerable<T>` subscriptions:** `await foreach` pattern for all message streams
- **`CancellationToken` on all async methods**
- **Dependency Injection:** `services.AddKubeMQ()` extension methods with hosted service
- **Events Store `FromTimeDelta`:** Subscribe from a relative time offset
- **Connection state events:** `StateChanged` event with `ConnectionState` enum
- **Keepalive configuration:** `KeepaliveOptions` for gRPC ping settings
- **Channel management:** `ListChannelsAsync`, `CreateChannelAsync`, `DeleteChannelAsync`
- **Queue message operations:** `AckAsync`, `RejectAsync`, `RequeueAsync`, `ExtendVisibilityAsync`
- **SourceLink and symbol package (`.snupkg`) support** for debugger source stepping
- **Runtime version query** via `KubeMQSdkInfo.Version`
- **GitHub Actions release pipeline** triggered by `v*` git tags
- **TROUBLESHOOTING.md:** Solutions for 11+ common issues
- **CONTRIBUTING.md:** Build, test, and PR requirements
- **MIGRATION-v3.md:** Complete v2 → v3 migration guide with before/after code
- **DocFX API reference:** Auto-generated and published on every release

### Changed

- **BREAKING:** Namespace changed from `KubeMQ.SDK.csharp` to `KubeMQ.Sdk`
- **BREAKING:** `Result` pattern replaced with typed exceptions — all methods that returned
  `Task<Result>` now return `Task` (or `Task<T>`) and throw on failure
- **BREAKING:** Configuration changed from fluent `SetX()` builders to `KubeMQClientOptions`
  with property initializers
- **BREAKING:** Client classes replaced — `EventsClient`, `QueuesClient`, `CommandsClient`,
  `QueriesClient` merged into single `KubeMQClient`
- **BREAKING:** Default send timeout is now 5s (previously no default for Events)
- **BREAKING:** Migrated from deprecated `Grpc.Core` to `Grpc.Net.Client`
- **BREAKING:** Target framework changed from `netstandard2.0`/`net5.0` to `net8.0`
- **BREAKING:** Message body type changed from `byte[]` to `ReadOnlyMemory<byte>`
- **BREAKING:** `Subscribe()` now returns `IAsyncEnumerable<T>` instead of `Result` with
  background `Task.Run`
- **BREAKING:** `Newtonsoft.Json` dependency removed — use `System.Text.Json` or bring your own

### Removed

- **BREAKING:** Old API surface (`KubeMQ.SDK.csharp.Events.*`, `KubeMQ.SDK.csharp.CommandQuery.*`,
  `KubeMQ.SDK.csharp.Queue.*`, `KubeMQ.SDK.csharp.QueueStream.*`) — all removed
- **BREAKING:** `Result`, `PingResult`, `ListAsyncResult` result wrapper classes
- **BREAKING:** Fluent builder methods (`SetChannel()`, `SetBody()`, etc.)
- **BREAKING:** `Console.WriteLine` debug output in production code
- `Grpc.Core` dependency (replaced by `Grpc.Net.Client`)
- `Newtonsoft.Json` dependency
- `Microsoft.CSharp` dependency
- `System.Configuration.ConfigurationManager` dependency

### Fixed

- Events Store `StartFromTime` subscription now correctly converts DateTime to epoch timestamp
- Auth token now injected on streaming calls (not just unary calls)
- `throw e;` replaced with `throw;` — stack traces preserved
- `Event.Encode()` no longer mutates the Tags dictionary on repeated sends
- Queue `Close()` no longer spin-waits infinitely

### Security

- Migrated from deprecated `Grpc.Core` (no longer receiving security patches) to supported
  `Grpc.Net.Client`
- Upgraded `Microsoft.Extensions.*` dependencies from EOL 3.1.x to current 8.x

## [2.0.0] - 2024-XX-XX

_No changelog maintained for prior versions. See git history for changes._

[Unreleased]: https://github.com/kubemq-io/kubemq-CSharp/compare/v3.0.0...HEAD
[3.0.0]: https://github.com/kubemq-io/kubemq-CSharp/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/kubemq-io/kubemq-CSharp/releases/tag/v2.0.0
