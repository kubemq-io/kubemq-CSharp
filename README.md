# KubeMQ .NET SDK

[![NuGet](https://img.shields.io/nuget/v/KubeMQ.SDK.CSharp.svg)](https://www.nuget.org/packages/KubeMQ.SDK.CSharp/)
[![CI](https://github.com/kubemq-io/kubemq-CSharp/actions/workflows/ci.yml/badge.svg)](https://github.com/kubemq-io/kubemq-CSharp/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/kubemq-io/kubemq-CSharp/branch/main/graph/badge.svg)](https://codecov.io/gh/kubemq-io/kubemq-CSharp)
[![License](https://img.shields.io/github/license/kubemq-io/kubemq-CSharp)](LICENSE)

## Description

[KubeMQ](https://kubemq.io/) is a Kubernetes-native messaging platform. This SDK provides
a .NET client for publishing and consuming messages across all KubeMQ messaging patterns:
Events (pub/sub), Events Store (persistent pub/sub), Queues (pull-based with acknowledgment),
and RPC (Commands and Queries).

For the full API reference, see the [API Documentation](https://kubemq-io.github.io/kubemq-CSharp/).

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
  - [Prerequisites](#prerequisites)
  - [Send an Event](#send-an-event)
  - [Receive Events](#receive-events)
  - [Expected Output](#expected-output)
  - [More Quick Starts](#more-quick-starts)
- [Messaging Patterns](#messaging-patterns)
  - [Events (Pub/Sub)](#events-pubsub)
  - [Events Store (Persistent Pub/Sub)](#events-store-persistent-pubsub)
  - [Queues](#queues)
  - [Commands (RPC)](#commands-rpc)
  - [Queries (RPC)](#queries-rpc)
- [Configuration](#configuration)
  - [ASP.NET Core / Dependency Injection](#aspnet-core--dependency-injection)
- [Error Handling](#error-handling)
  - [Retry Policy](#retry-policy)
- [Troubleshooting](#troubleshooting)
  - [Connection refused](#connection-refused)
  - [Authentication failed](#authentication-failed)
  - [More issues](#more-issues)
- [Server Compatibility](#server-compatibility)
- [Deprecation Policy](#deprecation-policy)
- [Version Lifecycle](#version-lifecycle)
  - [Support Policy](#support-policy)
  - [Upgrading](#upgrading)
- [Security](#security)
- [Additional Resources](#additional-resources)
- [Contributing](#contributing)
- [License](#license)

## Installation

```bash
dotnet add package KubeMQ.SDK.CSharp
```

Or via PackageReference:

```xml
<PackageReference Include="KubeMQ.SDK.CSharp" Version="3.*" />
```

**Prerequisites:**
- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (LTS)
- A running KubeMQ server (≥3.0) ([Docker quick start](https://docs.kubemq.io/getting-started/quick-start))

> **Note:** SDK v3.x requires .NET 8.0 (LTS). For .NET Framework or older runtimes, use [SDK v2.x](https://www.nuget.org/packages/KubeMQ.SDK.csharp/2.0.0) (EOL — security patches only).

## Quick Start

### Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (LTS)
- KubeMQ server (≥3.0) running on `localhost:50000`
  ```bash
  docker run -d -p 50000:50000 kubemq/kubemq-community:latest
  ```
- Install the SDK: `dotnet add package KubeMQ.SDK.CSharp`

### Send an Event

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions());
await client.ConnectAsync();

await client.PublishEventAsync(new EventMessage
{
    Channel = "my-channel",
    Body = Encoding.UTF8.GetBytes("Hello, KubeMQ!")
});
```

### Receive Events

> **Note:** Start the receiver before the sender. Events are not persisted — only
> subscribers connected at publish time receive the message. For persistent delivery,
> use Events Store with `EventStoreStartPosition.FromFirst`.

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions());
await client.ConnectAsync();

await foreach (var msg in client.SubscribeToEventsAsync(
    new EventsSubscription { Channel = "my-channel" }))
{
    Console.WriteLine($"Received: {Encoding.UTF8.GetString(msg.Body.Span)}");
}
```

### Expected Output

```
Received: Hello, KubeMQ!
```

### More Quick Starts

- [Queue Quick Start](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Queues/Queues.SendReceive)
- [Command Quick Start](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Commands/Commands.SendCommand)
- [Query Quick Start](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Queries/Queries.SendQuery)

## Messaging Patterns

KubeMQ supports five messaging patterns. Choose based on your delivery and interaction requirements:

| Pattern | Delivery Guarantee | Use When | Example Use Case |
|---------|--------------------|----------|------------------|
| Events | At-most-once | You need fire-and-forget broadcasting to multiple subscribers | Real-time notifications, log streaming |
| Events Store | At-least-once (persistent) | Subscribers must not miss messages, even if offline | Audit trails, event sourcing, replay |
| Queues | At-least-once (with ack) | Work must be processed exactly by one consumer with acknowledgment | Job processing, task distribution |
| Commands | At-most-once (request/reply) | You need a response confirming the action was executed | Device control, configuration changes |
| Queries | At-most-once (request/reply) | You need to retrieve data from a responder | Data lookups, service-to-service reads |

See the [examples/](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples) directory
for runnable examples of each pattern.

### Events (Pub/Sub)

Fire-and-forget messages broadcast to all subscribers on a channel. No persistence or
delivery guarantee. Use for real-time notifications where occasional message loss is acceptable.

→ [Events Examples](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Events)

### Events Store (Persistent Pub/Sub)

Persistent events stored by the server. Subscribers can replay from a sequence number,
timestamp, or time delta. Use for audit trails and event sourcing.

→ [Events Store Examples](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/EventsStore)

### Queues

Pull-based message delivery with explicit acknowledgment. Each message is processed by
exactly one consumer. Supports delayed delivery, expiration, dead letter queues, and
visibility timeout.

→ [Queue Examples](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Queues)

### Commands (RPC)

Request/reply pattern where the sender expects confirmation that the action was executed.
The responder returns a success/failure indicator with no response payload.

→ [Command Examples](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Commands)

### Queries (RPC)

Request/reply pattern where the sender expects a data response. Supports server-side
caching with configurable TTL.

→ [Query Examples](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Queries)

## Configuration

```csharp
var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "kubemq-server:50000",
    ClientId = "my-service",
    DefaultTimeout = TimeSpan.FromSeconds(10),
    Tls = new TlsOptions { Enabled = true, CaFile = "/certs/ca.pem" },
    Retry = new RetryPolicy { MaxRetries = 5 },
});
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Address` | `string` | `"localhost:50000"` | KubeMQ server address (host:port) |
| `ClientId` | `string?` | auto-generated | Unique identifier for this client |
| `AuthToken` | `string?` | `null` | JWT authentication token |
| `DefaultTimeout` | `TimeSpan` | `5s` | Default timeout for all operations |
| `ConnectionTimeout` | `TimeSpan` | `10s` | Timeout for initial connection |
| `WaitForReady` | `bool` | `true` | Block operations during reconnection |
| `Tls` | `TlsOptions?` | `null` | TLS/mTLS configuration |
| `Retry` | `RetryPolicy` | enabled (3 retries) | Retry with exponential backoff |
| `Keepalive` | `KeepaliveOptions` | 10s ping | gRPC keepalive pings |
| `Reconnect` | `ReconnectOptions` | enabled (unlimited) | Auto-reconnection behavior |
| `LoggerFactory` | `ILoggerFactory?` | `null` | Structured logging provider |

### ASP.NET Core / Dependency Injection

```csharp
builder.Services.AddKubeMQ(opts =>
{
    opts.Address = "kubemq-server:50000";
});
```

Or bind from configuration:

```csharp
builder.Services.AddKubeMQ(builder.Configuration);
```

```json
{
  "KubeMQ": {
    "Address": "kubemq-server:50000",
    "DefaultTimeout": "00:00:10"
  }
}
```

## Error Handling

All SDK methods throw typed exceptions derived from `KubeMQException`. The exception
hierarchy enables precise error handling:

```csharp
try
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "events",
        Body = Encoding.UTF8.GetBytes("hello")
    });
}
catch (KubeMQTimeoutException ex)
{
    // Operation exceeded deadline — may succeed on retry
    Console.WriteLine($"Timeout: {ex.Message}");
}
catch (KubeMQAuthenticationException ex)
{
    // Invalid or expired credentials — do not retry
    Console.WriteLine($"Auth failed: {ex.Message}");
}
catch (KubeMQConnectionException ex)
{
    // Not connected — auto-reconnection in progress
    Console.WriteLine($"Connection lost: {ex.Message}");
}
catch (KubeMQException ex)
{
    // Catch-all for any SDK error
    Console.WriteLine($"Error [{ex.ErrorCode}]: {ex.Message}");
    Console.WriteLine($"Retryable: {ex.IsRetryable}");
}
```

### Retry Policy

Operations are retried automatically for transient errors:

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxRetries` | `3` | Maximum retry attempts |
| `InitialBackoff` | `500ms` | Delay before first retry |
| `MaxBackoff` | `30s` | Maximum delay between retries |
| `BackoffMultiplier` | `2.0` | Exponential multiplier |

Disable retry:

```csharp
var client = new KubeMQClient(new KubeMQClientOptions
{
    Retry = new RetryPolicy { Enabled = false }
});
```

## Troubleshooting

### Connection refused

**Error:** `KubeMQConnectionException: Failed to connect to localhost:50000`

Verify the KubeMQ server is running and accessible:

```bash
docker run -d --name kubemq -p 50000:50000 kubemq/kubemq-community:latest
```

### Authentication failed

**Error:** `KubeMQAuthenticationException: Authentication failed`

Verify your token is valid and not expired. See the
[Authentication Guide](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Config).

### More issues

See the full [Troubleshooting Guide](https://github.com/kubemq-io/kubemq-CSharp/blob/main/TROUBLESHOOTING.md)
for solutions to 11+ common issues.

## Server Compatibility

| SDK Version | Server ≥4.0 | Server ≥3.0 | Server <3.0 |
|-------------|-------------|-------------|-------------|
| v3.x        | ✅          | ✅          | ❌ — untested |
| v2.x (EOL)  | ✅          | ✅          | ✅          |

On `ConnectAsync`, the SDK pings the server and logs a warning if the server version is outside
the tested range. The connection proceeds normally — no error is thrown.

For the full compatibility matrix including platform support, see [COMPATIBILITY.md](./COMPATIBILITY.md).

## Deprecation Policy

When an API is deprecated in the KubeMQ C# SDK:

1. The API is annotated with `[Obsolete("Use {replacement} instead. Will be removed in v{version}.")]`
2. The deprecation is documented in the [CHANGELOG](./CHANGELOG.md)
3. The deprecated API continues to function for at least **2 minor versions or 6 months** (whichever is longer)
4. After the notice period, the API may be removed in a subsequent minor or major release

## Version Lifecycle

| SDK Version | Status | .NET Support | Security Patches Until |
|-------------|--------|-------------|----------------------|
| v3.x        | **Active** | .NET 8.0 (LTS) | Current |
| v2.x        | **End-of-Life** | .NET 5.0–8.0, .NET Framework 4.6.1+ | v3.0.0 GA date + 12 months |
| v1.x        | **End-of-Life** | .NET Framework 4.6.1+ | No longer supported |

### Support Policy

- When a new **major** SDK version reaches GA, the previous major version receives **security patches only** for **12 months**
- After the 12-month window, the previous version is marked End-of-Life and receives no further updates
- Bug fixes and new features are only added to the active major version
- Security patches for EOL versions are applied on a best-effort basis and limited to critical/high CVEs

### Upgrading

See [MIGRATION-v3.md](./MIGRATION-v3.md) for the v2 → v3 migration guide.

## Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting. The SDK supports TLS and mTLS connections — for configuration details, see [How to Connect with TLS](docs/how-to/connect-with-tls.md).

## Additional Resources

- [KubeMQ Documentation](https://docs.kubemq.io/) — Official KubeMQ documentation and guides
- [Full Documentation Index](docs/INDEX.md) — Complete SDK documentation index
- [KubeMQ Concepts](docs/CONCEPTS.md) — Core KubeMQ messaging concepts
- [SDK Feature Parity Matrix](../sdk-feature-parity-matrix.md) — Cross-SDK feature comparison
- [CHANGELOG.md](./CHANGELOG.md) — Release history
- [TROUBLESHOOTING.md](https://github.com/kubemq-io/kubemq-CSharp/blob/main/TROUBLESHOOTING.md) — Common issues and solutions
- [Examples](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples) — Runnable code examples for all patterns

## Contributing

See [CONTRIBUTING.md](https://github.com/kubemq-io/kubemq-CSharp/blob/main/CONTRIBUTING.md)
for build instructions, code style, and PR requirements.

## License

Apache License 2.0 — see [LICENSE](https://github.com/kubemq-io/kubemq-CSharp/blob/main/LICENSE).
