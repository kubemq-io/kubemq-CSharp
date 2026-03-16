# KubeMQ C# SDK — Server Compatibility Matrix

| SDK Version | Server ≥4.0 | Server ≥3.0 | Server <3.0 | Notes |
|-------------|-------------|-------------|-------------|-------|
| v3.x        | ✅          | ✅          | ❌ — untested | v3 requires net8.0+, new queue stream API |
| v2.x (EOL)  | ✅          | ✅          | ✅          | Uses deprecated Grpc.Core; EOL 12 months after v3.0.0 GA |

## How We Test

The SDK CI pipeline runs integration tests against the **lowest supported** and **latest stable** server versions on every PR merge.

## Version Validation

On `ConnectAsync`, the SDK calls `PingAsync` and logs a warning if the server version falls outside the tested compatibility range. The connection proceeds normally — no error is thrown.

## Reporting Issues

If you encounter a compatibility issue not covered by this matrix, please open an issue at <https://github.com/kubemq-io/kubemq-CSharp/issues>.

---

## Platform Support

| Platform | net8.0 | Notes |
|----------|--------|-------|
| Linux (x64) | ✅ | Primary CI target |
| Linux (arm64) | ✅ | Tested in CI |
| Windows (x64) | ✅ | CI target |
| macOS (x64) | ✅ | CI target |
| macOS (arm64/Apple Silicon) | ✅ | CI target |
| Alpine Linux (musl) | ✅ | Works with `Grpc.Net.Client` natively |

### Container Support

The SDK is tested in the following container images:
- `mcr.microsoft.com/dotnet/runtime:8.0` (Debian-based)
- `mcr.microsoft.com/dotnet/runtime:8.0-alpine` (Alpine/musl)

### Kubernetes Deployment

- **Sidecar pattern:** KubeMQ server runs as a sidecar container; SDK connects to `localhost:50000`
- **Standalone pattern:** KubeMQ server runs as a separate deployment; SDK connects via Kubernetes DNS (e.g., `kubemq-cluster.default.svc.cluster.local:50000`)

Both patterns are supported. See the [README](./README.md) for connection examples.

---

## Dependency Audit

| Dependency | Version | Justification | Transitive? |
|-----------|---------|---------------|-------------|
| `Google.Protobuf` | 3.* | Protobuf serialization for gRPC messages | No — direct |
| `Grpc.Net.Client` | 2.* | gRPC transport layer (replaces deprecated `Grpc.Core`) | No — direct |
| `Grpc.Tools` | 2.* | Proto compilation at build time; PrivateAssets=All | Build-only |
| `Microsoft.Extensions.Logging.Abstractions` | 8.* | `ILogger` / `ILoggerFactory` interfaces | No — direct |
| `Microsoft.Extensions.Options` | 8.* | `IOptions<T>` pattern for DI | No — direct |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 8.* | `IServiceCollection` extensions | No — direct |
| `Microsoft.Extensions.Configuration.Abstractions` | 8.* | `IConfiguration` binding for DI | No — direct |
| `Microsoft.Extensions.Hosting.Abstractions` | 8.* | `IHostedService` for background connection management | No — direct |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 8.* | Configuration binding for `IOptions<T>` | No — direct |
| `Microsoft.SourceLink.GitHub` | 8.* | SourceLink for NuGet debugging; PrivateAssets=All | Build-only |

---

## gRPC Version Compatibility

| SDK Component | v2 Version | v3 Version | Compatibility Notes |
|--------------|-----------|-----------|---------------------|
| `Google.Protobuf` | 3.26.1 | 3.* | Wire-format compatible; proto3 syntax unchanged |
| `Grpc.Core` | 2.46.6 (deprecated) | (removed) | Replaced by `Grpc.Net.Client` |
| `Grpc.Net.Client` | (absent) | 2.* | HTTP/2-based, no native binaries |
| `Grpc.Tools` | (absent) | 2.* | Build-time only proto compilation |

**Wire compatibility:** The gRPC wire format (HTTP/2 + protobuf) is stable across all `Grpc.Net.Client` versions. A v3 SDK client can communicate with any KubeMQ server that speaks standard gRPC, regardless of whether the server uses `Grpc.Core` or `Grpc.Net.Client` internally.

---

## Protobuf Backward/Forward Compatibility

1. **Proto file versioning:** The proto file includes a comment header with the server version it was generated from
2. **Backward compatibility:** New fields added to proto messages use new field numbers; existing field numbers are never reused
3. **Forward compatibility:** The SDK gracefully handles unknown fields from newer server versions (protobuf default behavior — unknown fields are preserved in the wire format)
4. **Proto update process:** When the KubeMQ server adds new proto fields, the SDK proto file is updated and a new minor version is released
