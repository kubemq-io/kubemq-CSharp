# C# / .NET Language Constraints for Spec Agents

**Purpose:** Language-specific pitfalls that spec agents MUST follow to prevent common errors in C# SDK specs.

**Methodology:** Rules are cross-referenced against patterns used by Azure SDK for .NET, NATS.Net, StackExchange.Redis, Confluent.Kafka, Grpc.Net.Client, kubernetes-client/csharp, and AWS SDK for .NET.

---

## Syntax Rules

### CS-1: async/await must propagate correctly
Every async method MUST return `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>` — never `void` (except framework event handlers).
- **WRONG:** `public async void SendAsync(...)` — fire-and-forget, exceptions are lost and crash the process
- **RIGHT:** `public async Task SendAsync(...)` or `public async Task<Result> SendAsync(...)`
- All async methods should accept `CancellationToken` as the last parameter.
- `async void` is ONLY acceptable for framework-required event handlers, and the body MUST be wrapped in `try/catch`. See CS-23.
- Never call `.Result` or `.Wait()` on a `Task` — causes deadlocks in synchronization contexts (ASP.NET, WPF, WinForms). See CS-24.
- Use `ValueTask` / `ValueTask<T>` for methods that frequently complete synchronously (cache hits, status checks). See CS-21.

### CS-2: IDisposable and IAsyncDisposable
Any class that holds unmanaged resources (gRPC channels, timers, semaphores, `CancellationTokenSource`) MUST implement `IDisposable` AND `IAsyncDisposable`.
- Always include `using`/`await using` examples in specs.
- Implement the dispose pattern: `Dispose(bool disposing)` with finalizer safety.
- Implement `DisposeAsync()` and `DisposeAsyncCore()` following the dual-implementation pattern. See CS-22 for the full pattern.
- All public methods on disposable types MUST check `_disposed` and throw `ObjectDisposedException`. See CS-51.
- **WRONG:** Client class with gRPC channel but no `IDisposable`/`IAsyncDisposable` implementation.

**Specs MUST show both usage patterns:**
```csharp
// Preferred async pattern
await using var client = new KubeMQClient(options);

// Sync pattern still works
using var client = new KubeMQClient(options);
```

**Ecosystem references:**
- NATS.Net: `NatsConnection` implements both `IDisposable` and `IAsyncDisposable`.
- Azure SDK: Client classes implement `IDisposable`.

### CS-3: Nullable reference types (NRT)
Modern C# (8.0+) supports nullable reference types. Specs should:
- Enable `<Nullable>enable</Nullable>` in the project.
- Mark nullable parameters as `string?`, not just `string`.
- Use `[NotNull]`, `[MaybeNull]` attributes where needed.
- Never ignore nullable warnings in code snippets.
- With NRT enabled, still validate public API parameters at runtime using `ArgumentNullException.ThrowIfNull` (.NET 6+) or `nameof`-based throws for netstandard2.0. See CS-42.

### CS-4: Properties vs fields
C# uses properties, not public fields.
- **WRONG:** `public string Address;`
- **RIGHT:** `public string Address { get; set; }` or `public string Address { get; init; }` (C# 9+)
- Use `init` setters for immutable configuration objects.

### CS-5: Record types for DTOs (C# 9+)
Use `record` types for immutable data transfer objects:
```csharp
public record KubeMQError(string Code, string Message, string? RequestId = null);
```
Check the target framework version before using records.
- Use `required` (C# 11+) for mandatory properties — enforces initialization at compile time.
- Use `init` for optional properties — prevents post-construction mutation.
- Records provide value equality, `ToString()`, and `with` expressions automatically.
- For `netstandard2.0`, fall back to sealed classes with `{ get; }` properties.
- See CS-40 for detailed record type and DTO patterns.

---

## Naming Rules

### CS-6: Standard library name collisions
These names exist in .NET BCL and should be avoided:

| Name | Namespace | Risk |
|------|-----------|------|
| `Exception` | `System` | High — base exception type |
| `TimeoutException` | `System` | High — commonly caught |
| `OperationCanceledException` | `System` | High — CancellationToken |
| `Channel` | `System.Threading.Channels` | Medium |
| `Task` | `System.Threading.Tasks` | High — never reuse |
| `Logger` | `Microsoft.Extensions.Logging` | Medium |
| `Timer` | `System.Threading` / `System.Timers` | Medium |
| `HttpClient` | `System.Net.Http` | Medium |

**Resolution:** Prefix with `KubeMQ` or use descriptive names (e.g., `KubeMQTimeoutException`).

### CS-7: Namespace convention
Follow .NET namespace conventions:
- `KubeMQ.Sdk` — root namespace
- `KubeMQ.Sdk.Client` — client classes
- `KubeMQ.Sdk.Exceptions` — exception types (NOT `Errors`)
- `KubeMQ.Sdk.Config` — configuration
- `KubeMQ.Sdk.Grpc` — gRPC internals (internal)
- Use PascalCase for all namespace segments.

### CS-8: Async method naming
All async methods MUST end with `Async` suffix:
- `SendAsync`, `SubscribeAsync`, `ConnectAsync`
- **WRONG:** `public Task<Result> Send(...)` — missing `Async` suffix
- Synchronous wrappers (if any) use the bare name: `Send(...)`

---

## Concurrency Rules

### CS-9: ConfigureAwait(false) in library code
SDK/library code should use `ConfigureAwait(false)` on all awaits to avoid deadlocks:
```csharp
var result = await client.SendAsync(msg).ConfigureAwait(false);
```
- This is critical for library code used in ASP.NET or WPF contexts.
- Application code does NOT need this — only library code.

### CS-10: CancellationToken propagation
Every async method MUST accept `CancellationToken cancellationToken = default` as its last parameter.
- Pass the token to all downstream async calls.
- Check `cancellationToken.ThrowIfCancellationRequested()` at entry points.
- **WRONG:** `public Task<Result> SendAsync(Message msg)` — missing CancellationToken
- **RIGHT:** `public Task<Result> SendAsync(Message msg, CancellationToken cancellationToken = default)`

**CancellationTokenSource MUST be disposed:** `CancellationTokenSource` implements `IDisposable` and holds a kernel timer. It MUST be disposed, especially with timeouts or linked tokens. See CS-25.

**WRONG:**
```csharp
var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
cts.CancelAfter(timeout);
return await SendAsync(msg, cts.Token).ConfigureAwait(false);
// cts never disposed — timer leak
```

**RIGHT:**
```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
cts.CancelAfter(timeout);
return await SendAsync(msg, cts.Token).ConfigureAwait(false);
```

### CS-11: SemaphoreSlim for async locking
`lock` statements cannot be used with `await`. Use `SemaphoreSlim` for async-compatible locking:
```csharp
private readonly SemaphoreSlim _semaphore = new(1, 1);

await _semaphore.WaitAsync(cancellationToken);
try {
    // critical section with await
} finally {
    _semaphore.Release();
}
```
- **WRONG:** `lock (_obj) { await DoSomethingAsync(); }` — deadlock risk

**Synchronization primitive comparison** (see CS-48 for detailed guidance):

| Primitive | Use When | Async-Safe? |
|-----------|----------|-------------|
| `lock` | Sync-only critical section, no `await` inside | No |
| `SemaphoreSlim(1,1)` | Async critical section with `await` inside | Yes |
| `Interlocked.*` | Single atomic operation (increment, compare-exchange) | N/A |
| `Channel<T>` | Producer-consumer coordination | Yes |

### CS-12: Thread-safe collections
Use `System.Collections.Concurrent` types for shared state:
- `ConcurrentDictionary<TKey, TValue>` instead of `Dictionary` + lock
- `ConcurrentQueue<T>` instead of `Queue` + lock
- `Channel<T>` (System.Threading.Channels) for producer-consumer patterns

**Channel\<T\> bounded patterns:** When buffering messages, use `BoundedChannelOptions` to apply backpressure. Set `SingleReader`/`SingleWriter` when applicable for lock-free fast path. ALWAYS call `Writer.Complete()` when the producer finishes. Prefer `Wait` mode over `DropOldest`/`DropNewest` for messaging SDKs — message loss is unacceptable. See CS-35 for full patterns.

---

## Dependency Rules

### CS-13: Optional dependencies via conditional references
For optional dependencies (OTel, logging providers):
- Use `<PackageReference>` with `<PrivateAssets>all</PrivateAssets>` or make it a separate NuGet package.
- **Do NOT use** `Type.GetType()` for runtime detection — it breaks trimming and AOT. Use compile-time `#if` guards or separate NuGet packages instead. See CS-45.
- Create a separate package: `KubeMQ.Sdk.OpenTelemetry` that users install only if needed.
- **Never** make OTel a hard dependency of the core SDK package.
- Use `System.Diagnostics.ActivitySource` (built into .NET since 5.0) for distributed tracing in the core SDK — no OpenTelemetry NuGet dependency required. See CS-29.
- Use `System.Diagnostics.Metrics.Meter` for metrics instrumentation — no OpenTelemetry NuGet dependency required. See CS-30.

**WRONG:**
```csharp
// Runtime type detection — breaks trimming
var type = Type.GetType("OpenTelemetry.Trace.TracerProvider, OpenTelemetry");
if (type != null) { /* use OTel */ }
```

**RIGHT:**
```csharp
// Compile-time feature detection
#if KUBEMQ_OTEL
    // Separate NuGet package: KubeMQ.Sdk.OpenTelemetry
    ConfigureOtelExporter(tracerProvider);
#endif
```

### CS-14: Microsoft.Extensions.* compatibility
If using `Microsoft.Extensions.Logging`, `Microsoft.Extensions.DependencyInjection`, etc.:
- Support multiple major versions (6.x, 7.x, 8.x).
- Use the lowest common version in the core package.
- Provide extension methods for DI: `services.AddKubeMQ(options => { ... })`

**DI extension pattern** (see CS-27 for full implementation):
```csharp
public static class KubeMQServiceCollectionExtensions
{
    public static IServiceCollection AddKubeMQ(
        this IServiceCollection services,
        Action<KubeMQClientOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<KubeMQClient>();
        return services;
    }

    public static IServiceCollection AddKubeMQ(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KubeMQClientOptions>(
            configuration.GetSection("KubeMQ"));
        services.TryAddSingleton<KubeMQClient>();
        return services;
    }
}
```

**Ecosystem references:**
- NATS.Net: `services.AddNats()` with `NatsOptsBuilder`.
- Grpc.Net.Client: `services.AddGrpcClient<TClient>()` via client factory.

---

## Build Rules

### CS-15: Target framework considerations
Multi-target with a clear strategy:
```xml
<TargetFrameworks>netstandard2.0;net6.0;net8.0</TargetFrameworks>
```

| TFM | Purpose |
|-----|---------|
| `netstandard2.0` | .NET Framework 4.6.1+, Unity, Xamarin, Mono |
| `net6.0` | Access to `LoggerMessage` source gen, `CallerArgumentExpression` |
| `net8.0` | Frozen collections, keyed DI, `TimeProvider`, AOT improvements |

Features requiring newer APIs should be conditionally compiled with `#if NET6_0_OR_GREATER`. See CS-43 for detailed multi-target strategy.

**Conditional compilation example:**
```csharp
#if NET6_0_OR_GREATER
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connected to {Address}")]
    static partial void LogConnected(ILogger logger, string address);
#else
    private static readonly Action<ILogger, string, Exception?> LogConnected =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1), "Connected to {Address}");
#endif
```

### CS-16: NuGet package metadata
Specs referencing NuGet packaging must include:
- `<PackageId>`, `<Version>`, `<Authors>`, `<Description>`
- `<PackageReadmeFile>` (NuGet supports README since 2021)
- `<RepositoryUrl>` and `<PackageLicenseExpression>`
- Source Link for debugging: `<EmbedUntrackedSources>true</EmbedUntrackedSources>`

**SourceLink and deterministic build MSBuild properties** (see CS-44):
```xml
<PropertyGroup>
    <Deterministic>true</Deterministic>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>

<ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.*" PrivateAssets="All" />
</ItemGroup>
```

### CS-17: Verify gRPC generated code
Before referencing a gRPC method, verify it exists in the `.proto` file. The proto definition is the source of truth.
- C# gRPC uses `Grpc.Tools` for code generation.
- Generated files are in `obj/` — don't commit them.
- Service clients are `{ServiceName}.{ServiceName}Client`.
- **`Grpc.Core` is deprecated** and in maintenance mode. The SDK MUST use `Grpc.Net.Client` for client-side gRPC. `Grpc.Core.Api` (shared types) is NOT deprecated. See CS-36.

**WRONG:**
```csharp
// Using deprecated Grpc.Core channel
var channel = new Channel("localhost:50000", ChannelCredentials.Insecure);
```

**RIGHT:**
```csharp
// Using Grpc.Net.Client
var channel = GrpcChannel.ForAddress("http://localhost:50000");
```

**Exception:** If the SDK must support .NET Framework (not .NET Core/.NET 5+), `Grpc.Core` may be needed as a fallback via multi-targeting.

---

## Exception Hierarchy

### CS-18: Exception hierarchy must follow .NET conventions [P0]

Define a rooted exception hierarchy deriving from `System.Exception` (NOT `ApplicationException`). Every custom exception MUST implement:
1. Parameterless constructor
2. `(string message)` constructor
3. `(string message, Exception innerException)` constructor

**WRONG:**
```csharp
public class KubeMQException : Exception
{
    public KubeMQException(string message) : base(message) { }
    // Missing parameterless and innerException constructors
}
```

**RIGHT:**
```csharp
public class KubeMQException : Exception
{
    public string? ErrorCode { get; }
    public string? RequestId { get; }

    public KubeMQException() { }
    public KubeMQException(string message) : base(message) { }
    public KubeMQException(string message, Exception innerException)
        : base(message, innerException) { }
    public KubeMQException(string message, string? errorCode, string? requestId = null)
        : base(message)
    {
        ErrorCode = errorCode;
        RequestId = requestId;
    }
}
```

**Recommended hierarchy:**
```
KubeMQException (base)
├── KubeMQConnectionException      (connect/reconnect failures)
├── KubeMQAuthenticationException   (auth/TLS failures)
├── KubeMQTimeoutException          (deadline exceeded)
├── KubeMQOperationException        (send/receive failures)
│   ├── KubeMQPublishException
│   └── KubeMQSubscribeException
└── KubeMQConfigurationException    (invalid options)
```

**Ecosystem references:**
- Azure SDK: Single `RequestFailedException` with `Status` and `ErrorCode` properties — flat hierarchy, rich metadata.
- NATS.Net: `NatsException` base with `NatsNoReplyException`, `NatsNoRespondersException`.
- Confluent.Kafka: `KafkaException` base with `ProduceException<K,V>`, `ConsumeException`.

Related to CS-6 naming.

### CS-19: Always chain InnerException — never swallow original exceptions [P0]

When wrapping gRPC `RpcException` or any other exception into a KubeMQ exception, ALWAYS pass the original as `innerException`. Never convert to string-only.

**WRONG:**
```csharp
catch (RpcException ex)
{
    throw new KubeMQException($"gRPC error: {ex.Message}");
    // Original stack trace and status code LOST
}
```

**RIGHT:**
```csharp
catch (RpcException ex)
{
    throw new KubeMQConnectionException(
        $"Failed to connect to {address}: {ex.Status.Detail}",
        errorCode: ex.StatusCode.ToString(),
        innerException: ex);
}
```

**Ecosystem references:**
- Azure SDK: `RequestFailedException` always wraps the `Response` and original exception.
- StackExchange.Redis: `RedisConnectionException` chains `InnerException` from socket errors.

### CS-20: Do not use [Serializable] on new exceptions targeting .NET 5+ [P1]

The `[Serializable]` attribute and serialization constructors are only needed if the SDK targets `netstandard2.0` or .NET Framework. For `net6.0+`-only targets, skip them — `BinaryFormatter` is obsolete and disabled by default.

**RIGHT for multi-target:**
```csharp
public class KubeMQException : Exception
{
    public KubeMQException() { }
    public KubeMQException(string message) : base(message) { }
    public KubeMQException(string message, Exception inner) : base(message, inner) { }

#if NETFRAMEWORK || NETSTANDARD2_0
    [Obsolete("BinaryFormatter serialization is obsolete")]
    protected KubeMQException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
#endif
}
```

**Ecosystem references:**
- Azure SDK: Uses `#if` guards for serialization constructors.
- AWS SDK: Marks serialization constructors as `[Obsolete]` on .NET 5+.

---

## Async Patterns

### CS-21: ValueTask for hot-path synchronous-completion methods [P1]

Use `ValueTask` / `ValueTask<T>` instead of `Task` / `Task<T>` for methods that frequently complete synchronously (cache hits, already-connected checks, buffered reads). Use `Task` for all other public API methods.

**WRONG:**
```csharp
// Allocates a Task on every call even when result is cached
public async Task<bool> IsConnectedAsync(CancellationToken ct = default)
{
    return _isConnected; // synchronous completion, wasted allocation
}
```

**RIGHT:**
```csharp
public ValueTask<bool> IsConnectedAsync(CancellationToken ct = default)
{
    return _isConnected
        ? new ValueTask<bool>(true)
        : new ValueTask<bool>(CheckConnectionAsync(ct));
}
```

**Constraints on ValueTask:**
- MUST NOT be awaited multiple times
- MUST NOT be stored and awaited later (use `.AsTask()` if needed)
- MUST NOT use `.Result` or `.GetAwaiter().GetResult()` before completion

**Ecosystem references:**
- NATS.Net: Uses `ValueTask` extensively for `PublishAsync` and buffer operations.
- Grpc.Net.Client: `HttpPipelinePolicy.ProcessAsync` returns `ValueTask`.
- Azure SDK: Uses `ValueTask` for pipeline policies.

Extends CS-1.

### CS-22: IAsyncDisposable — implement alongside IDisposable [P0]

Any class implementing `IDisposable` that performs async cleanup (closing gRPC channels, flushing buffers) MUST also implement `IAsyncDisposable`. The recommended pattern:

```csharp
public class KubeMQClient : IDisposable, IAsyncDisposable
{
    private bool _disposed;
    private readonly GrpcChannel _channel;
    private readonly CancellationTokenSource _cts;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
                _channel.Dispose();
            }
            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        _cts.Cancel();
        _cts.Dispose();
        _channel.Dispose();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
```

**Ecosystem references:**
- NATS.Net: `NatsConnection` implements both `IDisposable` and `IAsyncDisposable`.
- StackExchange.Redis: `ConnectionMultiplexer` implements `IDisposable` (sync-only, historical).
- Azure SDK: Client classes implement `IDisposable`.

Strengthens CS-2.

### CS-23: Never use async void [P0]

`async void` methods lose exceptions silently and crash the process. The ONLY exception is event handlers required by framework APIs.

**WRONG:**
```csharp
public async void OnMessageReceived(Message msg)
{
    await ProcessAsync(msg); // Exception crashes the process
}
```

**RIGHT:**
```csharp
public async Task OnMessageReceivedAsync(Message msg)
{
    await ProcessAsync(msg).ConfigureAwait(false);
}

// ONLY acceptable async void — framework event handler:
private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
{
    try
    {
        await ReconnectAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Reconnect failed");
    }
}
```

Elevates the `async void` mention from CS-1 into an explicit rule with examples.

### CS-24: Never use Task.Result or Task.Wait() — sync-over-async deadlock [P0]

Calling `.Result` or `.Wait()` on a `Task` from a synchronization context (ASP.NET, WPF, WinForms) causes deadlocks. SDK code MUST NOT use these patterns.

**WRONG:**
```csharp
public Result Send(Message msg)
{
    return SendAsync(msg).Result; // DEADLOCK in ASP.NET
}

public void Connect()
{
    ConnectAsync().Wait(); // DEADLOCK in WPF
}
```

**RIGHT — if sync wrapper is truly needed:**
```csharp
public Result Send(Message msg)
{
    return Task.Run(() => SendAsync(msg)).GetAwaiter().GetResult();
}
```

**BEST — avoid sync wrappers entirely.** Provide only async API; let callers decide.

**Ecosystem references:**
- Azure SDK: Provides both sync and async methods but implements them independently, never as sync-over-async wrappers.
- NATS.Net v2: Async-only API — no sync wrappers.

### CS-25: CancellationTokenSource MUST be disposed [P0]

`CancellationTokenSource` implements `IDisposable` and holds a kernel timer. It MUST be disposed, especially when used with timeouts or linked tokens.

**WRONG:**
```csharp
public async Task<Result> SendWithTimeoutAsync(Message msg, TimeSpan timeout, CancellationToken ct)
{
    var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(timeout);
    return await SendAsync(msg, cts.Token).ConfigureAwait(false);
    // cts never disposed — timer leak
}
```

**RIGHT:**
```csharp
public async Task<Result> SendWithTimeoutAsync(Message msg, TimeSpan timeout, CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(timeout);
    return await SendAsync(msg, cts.Token).ConfigureAwait(false);
}
```

**Ecosystem references:**
- Azure SDK: All linked CTS instances use `using` declarations.
- Grpc.Net.Client: Deadline propagation with linked CTS + disposal.

Extends CS-10.

---

## Builder / Options / Configuration Patterns

### CS-26: Use Options pattern for client configuration [P1]

Client configuration MUST use a dedicated options class. Support both direct construction and `IOptions<T>` injection.

**WRONG:**
```csharp
// Too many constructor parameters
public KubeMQClient(string address, string clientId, string authToken,
    bool useTls, int maxRetries, TimeSpan reconnectInterval) { }
```

**RIGHT:**
```csharp
public class KubeMQClientOptions
{
    public string Address { get; set; } = "localhost:50000";
    public string? ClientId { get; set; }
    public string? AuthToken { get; set; }
    public bool UseTls { get; set; }
    public int MaxReconnectAttempts { get; set; } = 5;
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool DisableAutoReconnect { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Address))
            throw new KubeMQConfigurationException("Address is required.");
    }
}

// Direct construction
var client = new KubeMQClient(new KubeMQClientOptions { Address = "server:50000" });

// DI with IOptions<T>
services.Configure<KubeMQClientOptions>(configuration.GetSection("KubeMQ"));
services.AddSingleton<KubeMQClient>();
```

**Ecosystem references:**
- Azure SDK: `ClientOptions` base class for all service clients.
- NATS.Net: `NatsOpts` record with `NatsOptsBuilder`.
- StackExchange.Redis: `ConfigurationOptions` class.
- Confluent.Kafka: `ProducerConfig` / `ConsumerConfig` dictionary-based options.

### CS-27: Provide DI extension methods [P1]

SDK MUST provide `IServiceCollection` extension methods for registration. Follow the `Add{ServiceName}` convention.

```csharp
public static class KubeMQServiceCollectionExtensions
{
    public static IServiceCollection AddKubeMQ(
        this IServiceCollection services,
        Action<KubeMQClientOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<KubeMQClient>();
        return services;
    }

    public static IServiceCollection AddKubeMQ(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<KubeMQClientOptions>(
            configuration.GetSection("KubeMQ"));
        services.TryAddSingleton<KubeMQClient>();
        return services;
    }
}
```

**Usage:**
```csharp
builder.Services.AddKubeMQ(opts =>
{
    opts.Address = "kubemq-server:50000";
    opts.UseTls = true;
});
```

**Ecosystem references:**
- NATS.Net: `services.AddNats()` with `NatsOptsBuilder`.
- Azure SDK: Not DI-first but provides DI samples.
- Grpc.Net.Client: `services.AddGrpcClient<TClient>()` via client factory.

Strengthens CS-14.

### CS-28: Testability — virtual methods and protected constructors [P1]

All public client methods MUST be `virtual` to enable mocking. Provide a `protected` parameterless constructor for test subclasses.

```csharp
public class KubeMQClient : IDisposable, IAsyncDisposable
{
    // Production constructor
    public KubeMQClient(KubeMQClientOptions options) { ... }

    // Mocking constructor — protected, parameterless
    protected KubeMQClient() { }

    // Virtual so mocking frameworks (Moq, NSubstitute) can override
    public virtual Task<SendResult> SendAsync(
        Message message, CancellationToken cancellationToken = default)
    { ... }
}
```

**Test example:**
```csharp
var mock = new Mock<KubeMQClient>();
mock.Setup(c => c.SendAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new SendResult { IsSuccess = true });

var sut = new OrderService(mock.Object);
```

**Ecosystem references:**
- Azure SDK: ALL client methods are `virtual`; ALL clients have `protected` parameterless constructors. This is a core design rule.
- kubernetes-client/csharp: `IKubernetes` interface for mocking.

---

## Observability

### CS-29: Use ActivitySource for distributed tracing — NOT direct OTel dependency [P0]

Use `System.Diagnostics.ActivitySource` and `System.Diagnostics.Activity` (built into .NET since 5.0) for tracing. Do NOT depend on OpenTelemetry NuGet packages in the core SDK.

**WRONG:**
```csharp
// Core SDK directly references OpenTelemetry
using OpenTelemetry.Trace;
var tracer = TracerProvider.Default.GetTracer("KubeMQ");
var span = tracer.StartActiveSpan("Send");
```

**RIGHT:**
```csharp
// Core SDK uses System.Diagnostics (no extra dependency)
internal static class KubeMQActivitySource
{
    internal static readonly ActivitySource Source = new("KubeMQ.Sdk", "1.0.0");
}

public async Task<SendResult> SendAsync(Message msg, CancellationToken ct = default)
{
    using var activity = KubeMQActivitySource.Source.StartActivity(
        "kubemq.send",
        ActivityKind.Producer);

    activity?.SetTag("messaging.system", "kubemq");
    activity?.SetTag("messaging.destination.name", msg.Channel);
    activity?.SetTag("messaging.operation.type", "publish");

    try
    {
        var result = await InternalSendAsync(msg, ct).ConfigureAwait(false);
        activity?.SetTag("messaging.operation.result", "success");
        return result;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        throw;
    }
}
```

**Key rules:**
- `ActivitySource` MUST be `static readonly` — expensive to create.
- Name with dot-separated UpperCamelCase: `"KubeMQ.Sdk"`.
- Check `activity != null` (or use `activity?.`) — it's null when no listener is attached.
- Check `activity.IsAllDataRequested` before setting expensive tags.
- Use `using` to ensure `Activity` is stopped/disposed.

**Ecosystem references:**
- AWS SDK: Uses `ActivitySource` natively; separate `OpenTelemetry.Instrumentation.AWS` package for OTel integration.
- Azure SDK: Uses `ClientDiagnostics` internally, which wraps `ActivitySource`.
- .NET runtime: All `HttpClient`, gRPC calls emit `Activity` automatically.

Strengthens CS-13.

### CS-30: Use Meter for metrics — NOT direct OTel dependency [P1]

Use `System.Diagnostics.Metrics.Meter` for metrics instrumentation.

```csharp
internal static class KubeMQMetrics
{
    internal static readonly Meter Meter = new("KubeMQ.Sdk", "1.0.0");

    internal static readonly Counter<long> MessagesSent =
        Meter.CreateCounter<long>(
            "kubemq.messages.sent",
            unit: "{message}",
            description: "Number of messages sent");

    internal static readonly Histogram<double> SendDuration =
        Meter.CreateHistogram<double>(
            "kubemq.send.duration",
            unit: "ms",
            description: "Duration of send operations");

    internal static readonly UpDownCounter<long> ActiveSubscriptions =
        Meter.CreateUpDownCounter<long>(
            "kubemq.subscriptions.active",
            unit: "{subscription}",
            description: "Number of active subscriptions");
}
```

**Usage:**
```csharp
var sw = Stopwatch.StartNew();
var result = await InternalSendAsync(msg, ct).ConfigureAwait(false);
sw.Stop();

KubeMQMetrics.MessagesSent.Add(1,
    new KeyValuePair<string, object?>("channel", msg.Channel));
KubeMQMetrics.SendDuration.Record(sw.Elapsed.TotalMilliseconds);
```

**Key rules:**
- `Meter` MUST be `static readonly` — expensive to create.
- Use semantic naming: `kubemq.{noun}.{verb}` (e.g., `kubemq.messages.sent`).
- Choose correct instrument type: Counter (monotonic), UpDownCounter (gauge-like), Histogram (distributions).

**Ecosystem references:**
- AWS SDK: Uses `Meter` for SDK-level metrics.
- OTel .NET best practices: Recommend `Meter` as a `static readonly` field.

### CS-31: Use LoggerMessage source generators for high-performance logging [P1]

SDK logging MUST use `LoggerMessage` source generators (or `LoggerMessage.Define` for netstandard2.0). Do NOT use `ILogger.LogInformation()` extension methods in hot paths.

**WRONG:**
```csharp
_logger.LogInformation("Message sent to {Channel} in {Duration}ms",
    channel, duration);
// Allocates params array + boxes value types on EVERY call
```

**RIGHT (.NET 6+):**
```csharp
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Message sent to {Channel} in {Duration}ms")]
    public static partial void MessageSent(ILogger logger, string channel, double duration);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Connection lost to {Address}, reconnecting (attempt {Attempt})")]
    public static partial void ConnectionLost(ILogger logger, string address, int attempt);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Send failed for channel {Channel}")]
    public static partial void SendFailed(ILogger logger, string channel, Exception ex);
}

// Usage:
Log.MessageSent(_logger, msg.Channel, sw.Elapsed.TotalMilliseconds);
```

**RIGHT (netstandard2.0 — LoggerMessage.Define):**
```csharp
private static readonly Action<ILogger, string, double, Exception?> _messageSent =
    LoggerMessage.Define<string, double>(
        LogLevel.Information, new EventId(1, nameof(MessageSent)),
        "Message sent to {Channel} in {Duration}ms");

public static void MessageSent(ILogger logger, string channel, double duration)
    => _messageSent(logger, channel, duration, null);
```

**Ecosystem references:**
- .NET CA1848 analyzer: Flags `LogInformation()` in libraries as a performance issue.
- ASP.NET Core: Uses `LoggerMessage` source generators throughout.

Intersects with CS-14 (Microsoft.Extensions.* compatibility).

---

## Connection & Reconnection

### CS-32: GrpcChannel MUST be singleton and long-lived [P0]

The SDK MUST create ONE `GrpcChannel` per server address and reuse it for all calls. Never create a channel per-call or per-client-method-invocation.

**WRONG:**
```csharp
public async Task<SendResult> SendAsync(Message msg, CancellationToken ct)
{
    using var channel = GrpcChannel.ForAddress(_address);
    var client = new Kubemq.kubemqClient(channel);
    return await client.SendMessageAsync(msg, cancellationToken: ct);
    // Channel disposed after EVERY call — TCP + TLS + HTTP/2 renegotiation
}
```

**RIGHT:**
```csharp
public class KubeMQClient : IAsyncDisposable
{
    private readonly GrpcChannel _channel;
    private readonly Kubemq.kubemqClient _grpcClient;

    public KubeMQClient(KubeMQClientOptions options)
    {
        _channel = GrpcChannel.ForAddress(options.Address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            }
        });
        _grpcClient = new Kubemq.kubemqClient(_channel);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Dispose();
    }
}
```

**High concurrency:** Enable `EnableMultipleHttp2Connections = true` to avoid head-of-line blocking when the concurrent stream limit per connection is reached.

**Ecosystem references:**
- Grpc.Net.Client docs: "A channel represents a long-lived connection... Creating a channel can be expensive."
- StackExchange.Redis: `ConnectionMultiplexer` is singleton by design.
- NATS.Net: `NatsConnection` is a long-lived singleton.

### CS-33: Reconnection must be automatic with configurable backoff [P1]

The SDK MUST automatically reconnect on transient failures. The reconnection logic must:
1. Use exponential backoff with jitter
2. Be configurable (max attempts, initial delay, max delay)
3. Emit events/logs for connection state changes
4. NOT block the caller — reconnection runs in the background
5. Maintain subscriptions across reconnections

**Pattern:**
```csharp
private async Task ReconnectLoopAsync(CancellationToken ct)
{
    var attempt = 0;
    while (!ct.IsCancellationRequested)
    {
        attempt++;
        try
        {
            await ConnectInternalAsync(ct).ConfigureAwait(false);
            Log.Reconnected(_logger, _options.Address, attempt);
            attempt = 0;
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_options.MaxReconnectAttempts > 0 && attempt >= _options.MaxReconnectAttempts)
            {
                Log.MaxReconnectAttemptsExceeded(_logger, _options.Address, attempt);
                throw new KubeMQConnectionException(
                    $"Failed to reconnect after {attempt} attempts", ex);
            }

            var delay = CalculateBackoffWithJitter(attempt);
            Log.ReconnectAttempt(_logger, _options.Address, attempt, delay);
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }
}

private TimeSpan CalculateBackoffWithJitter(int attempt)
{
    var baseDelay = _options.ReconnectInterval.TotalMilliseconds;
    var maxDelay = _options.MaxReconnectInterval.TotalMilliseconds;
    var exponential = Math.Min(baseDelay * Math.Pow(2, attempt - 1), maxDelay);
    var jitter = Random.Shared.NextDouble() * exponential * 0.2;
    return TimeSpan.FromMilliseconds(exponential + jitter);
}
```

**Ecosystem references:**
- NATS.Net: Automatic reconnection with `MaxReconnect`, `ReconnectWait`, custom delay callback.
- StackExchange.Redis: Automatic reconnection via internal heartbeat.
- Confluent.Kafka: Automatic reconnection built into librdkafka.
- gRPC: Built-in retry policies via `ServiceConfig`.

---

## Streaming & IAsyncEnumerable

### CS-34: Use IAsyncEnumerable for subscription streams [P1]

Subscription methods that return a stream of messages SHOULD expose `IAsyncEnumerable<T>` for consumer-friendly `await foreach` consumption.

**WRONG:**
```csharp
// Callback-based — harder to use, harder to cancel
public void Subscribe(string channel, Action<Message> onMessage, Action<Exception> onError)
```

**RIGHT:**
```csharp
public async IAsyncEnumerable<Message> SubscribeAsync(
    string channel,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var call = _grpcClient.SubscribeToEvents(
        new Subscribe { Channel = channel },
        cancellationToken: cancellationToken);

    await foreach (var response in call.ResponseStream
        .ReadAllAsync(cancellationToken)
        .ConfigureAwait(false))
    {
        yield return MapToMessage(response);
    }
}

// Consumer usage:
await foreach (var msg in client.SubscribeAsync("events", ct))
{
    await ProcessAsync(msg);
}
```

**For netstandard2.0:** Add `Microsoft.Bcl.AsyncInterfaces` package reference.

**Ecosystem references:**
- kubernetes-client/csharp: `WatchAsync` returns `IAsyncEnumerable<WatchEvent<T>>`.
- NATS.Net: `SubscribeAsync` returns `IAsyncEnumerable<NatsMsg<T>>`.
- Grpc.Net.Client: `ReadAllAsync()` extension returns `IAsyncEnumerable<T>`.

### CS-35: Use Channel\<T\> for internal producer-consumer buffering [P2]

When the SDK needs to buffer messages between gRPC streams and consumer code, use `System.Threading.Channels.Channel<T>`, NOT `BlockingCollection<T>` or `ConcurrentQueue<T>` with manual signaling.

```csharp
private readonly Channel<Message> _messageChannel =
    Channel.CreateBounded<Message>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true,
    });

// Producer (gRPC reader)
private async Task ReadGrpcStreamAsync(CancellationToken ct)
{
    await foreach (var response in _call.ResponseStream.ReadAllAsync(ct).ConfigureAwait(false))
    {
        await _messageChannel.Writer.WriteAsync(MapToMessage(response), ct)
            .ConfigureAwait(false);
    }
    _messageChannel.Writer.Complete();
}

// Consumer
public IAsyncEnumerable<Message> ConsumeAsync(CancellationToken ct)
    => _messageChannel.Reader.ReadAllAsync(ct);
```

**Key rules:**
- Use `BoundedChannelOptions` to apply backpressure — prevent unbounded memory growth.
- Set `SingleReader`/`SingleWriter` when applicable — enables lock-free fast path.
- ALWAYS call `Writer.Complete()` when the producer finishes.
- Prefer `Wait` mode over `DropOldest`/`DropNewest` for messaging SDKs — message loss is unacceptable.

**Ecosystem references:**
- NATS.Net: Uses `Channel<T>` for subscription message delivery.
- kubernetes-client/csharp: Uses `Channel<T>` for watch event buffering.

Extends CS-12.

---

## gRPC-Specific Patterns

### CS-36: Use Grpc.Net.Client — NOT Grpc.Core [P0]

The SDK MUST use `Grpc.Net.Client` for client-side gRPC. `Grpc.Core` is deprecated and in maintenance mode. `Grpc.Core.Api` (shared types) is NOT deprecated and can still be referenced.

**WRONG:**
```csharp
// Using deprecated Grpc.Core channel
var channel = new Channel("localhost:50000", ChannelCredentials.Insecure);
```

**RIGHT:**
```csharp
// Using Grpc.Net.Client
var channel = GrpcChannel.ForAddress("http://localhost:50000");

// Or with TLS
var channel = GrpcChannel.ForAddress("https://localhost:50000", new GrpcChannelOptions
{
    HttpHandler = new SocketsHttpHandler
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            ClientCertificates = new X509CertificateCollection { clientCert }
        }
    }
});
```

**Exception:** If the SDK must support .NET Framework (not .NET Core/.NET 5+), `Grpc.Core` may be needed as a fallback via multi-targeting.

**Ecosystem references:**
- googleapis/google-cloud-dotnet: Migrated from Grpc.Core to Grpc.Net.Client.
- Microsoft Learn: Recommends Grpc.Net.Client for all new development.

Strengthens CS-17.

### CS-37: Interceptor chain for cross-cutting concerns [P1]

Use gRPC interceptors for auth token injection, logging, metrics, and retry — NOT ad-hoc code in every method.

```csharp
public class AuthInterceptor : Interceptor
{
    private readonly KubeMQClientOptions _options;

    public AuthInterceptor(KubeMQClientOptions options)
    {
        _options = options;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        if (!string.IsNullOrEmpty(_options.AuthToken))
            headers.Add("authorization", _options.AuthToken);
        if (!string.IsNullOrEmpty(_options.ClientId))
            headers.Add("client-id", _options.ClientId);

        var newOptions = context.Options.WithHeaders(headers);
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, newOptions);

        return base.AsyncUnaryCall(request, newContext, continuation);
    }

    // MUST override ALL call types: AsyncServerStreamingCall, AsyncClientStreamingCall,
    // AsyncDuplexStreamingCall — not just AsyncUnaryCall
}
```

**Channel creation with interceptors:**
```csharp
var channel = GrpcChannel.ForAddress(options.Address);
var invoker = channel.Intercept(
    new AuthInterceptor(options),
    new LoggingInterceptor(logger),
    new MetricsInterceptor());
var client = new Kubemq.kubemqClient(invoker);
```

**Ecosystem references:**
- Azure SDK: `HttpPipelinePolicy` for similar middleware-chain pattern.
- Confluent.Kafka: Builder pattern with `SetLogHandler`, `SetErrorHandler`.

### CS-38: Deadline/timeout propagation with gRPC [P1]

Use gRPC deadlines (NOT `Task.Delay` + cancellation) for per-call timeouts. Combine with `CancellationToken` for user cancellation.

**WRONG:**
```csharp
// Using Task.WhenAny with delay — wasteful, doesn't propagate to server
var cts = new CancellationTokenSource();
var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
var callTask = client.SendAsync(msg, cts.Token);
if (await Task.WhenAny(callTask, timeoutTask) == timeoutTask)
{
    cts.Cancel();
    throw new TimeoutException();
}
```

**RIGHT:**
```csharp
public async Task<SendResult> SendAsync(Message msg, CancellationToken ct = default)
{
    var deadline = DateTime.UtcNow.Add(_options.DefaultTimeout);
    var callOptions = new CallOptions(
        deadline: deadline,
        cancellationToken: ct);

    try
    {
        var response = await _grpcClient.SendMessageAsync(
            MapToRequest(msg), callOptions).ConfigureAwait(false);
        return MapToResult(response);
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
    {
        throw new KubeMQTimeoutException(
            $"Send to '{msg.Channel}' exceeded deadline of {_options.DefaultTimeout}", ex);
    }
}
```

**Ecosystem references:**
- Grpc.Net.Client: `CallOptions.Deadline` is the canonical timeout mechanism.
- Azure SDK: Uses `HttpMessage.NetworkTimeout` per request.

### CS-39: gRPC retry via ServiceConfig — NOT manual retry loops [P2]

Prefer gRPC built-in retry policies over manual retry implementations for idempotent methods. For non-idempotent methods or complex retry logic, a manual retry with well-defined semantics is acceptable.

```csharp
var defaultMethodConfig = new MethodConfig
{
    Names = { MethodName.Default },
    RetryPolicy = new RetryPolicy
    {
        MaxAttempts = 5,
        InitialBackoff = TimeSpan.FromSeconds(1),
        MaxBackoff = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 2,
        RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.Aborted }
    }
};

var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
{
    ServiceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } }
});
```

**Key rules:**
- Only retry on idempotent operations (sends with dedup keys, queries) — NEVER blindly retry publishes.
- `StatusCode.Unavailable` and `StatusCode.Aborted` are the standard retryable codes.
- Requires Grpc.Net.Client >= 2.36.0.

**Ecosystem references:**
- Grpc.Net.Client docs: Built-in retry policies via `ServiceConfig`.
- Azure SDK: Uses per-retry pipeline policies.

---

## Immutability & Type Design

### CS-40: Use record types for message/result DTOs [P1]

Use `record` types for message and result objects that are created once and never mutated. Use `init` properties for types that need property-by-property construction.

**RIGHT:**
```csharp
// Positional record for simple DTOs
public record SendResult(bool IsSuccess, string? ErrorMessage = null, string? RequestId = null);

// Record with init properties for complex types
public record Message
{
    public required string Channel { get; init; }
    public ReadOnlyMemory<byte> Body { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    public string? ClientId { get; init; }
}
```

**Key rules:**
- Use `required` (C# 11+) for mandatory properties — enforces initialization at compile time.
- Use `init` for optional properties — prevents post-construction mutation.
- Records provide value equality, `ToString()`, and `with` expressions automatically.
- For `netstandard2.0`, fall back to sealed classes with `{ get; }` properties.

Extends CS-5.

### CS-41: Use ReadOnlyMemory\<byte\> for message bodies — NOT byte[] [P2]

Message body and payload types SHOULD use `ReadOnlyMemory<byte>` instead of `byte[]` to avoid defensive copies and enable zero-copy slicing.

**WRONG:**
```csharp
public class Message
{
    public byte[] Body { get; set; } // Mutable, requires defensive copy
}
```

**RIGHT:**
```csharp
public record Message
{
    public ReadOnlyMemory<byte> Body { get; init; }
}
```

**Why:**
- `byte[]` is mutable — callers can modify the array after passing it.
- `ReadOnlyMemory<byte>` prevents mutation and enables zero-copy `Slice()`.
- gRPC's `ByteString` has `Memory` property for efficient conversion.

**Ecosystem references:**
- NATS.Net: Uses `ReadOnlyMemory<byte>` for payloads.
- StackExchange.Redis: Uses `ReadOnlyMemory<byte>` for values.

---

## Nullable Reference Types

### CS-42: Guard clauses MUST use ArgumentNullException.ThrowIfNull [P1]

With NRT enabled, still validate public API parameters at runtime. Use `ArgumentNullException.ThrowIfNull` (.NET 6+) or manual throw for netstandard2.0.

**WRONG:**
```csharp
public Task<SendResult> SendAsync(Message message, CancellationToken ct = default)
{
    if (message == null)
        throw new ArgumentNullException("message");
    // string literal, not nameof
}
```

**RIGHT (.NET 6+):**
```csharp
public Task<SendResult> SendAsync(Message message, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(message);
    ArgumentException.ThrowIfNullOrWhiteSpace(message.Channel);
    // ...
}
```

**RIGHT (netstandard2.0):**
```csharp
public Task<SendResult> SendAsync(Message message, CancellationToken ct = default)
{
    if (message is null)
        throw new ArgumentNullException(nameof(message));
    if (string.IsNullOrWhiteSpace(message.Channel))
        throw new ArgumentException("Channel is required.", nameof(message));
    // ...
}
```

Extends CS-3.

---

## Build & Packaging (Extended)

### CS-43: Multi-target framework strategy [P0]

The SDK MUST define a clear multi-target strategy:

```xml
<TargetFrameworks>netstandard2.0;net6.0;net8.0</TargetFrameworks>
```

| TFM | Purpose |
|-----|---------|
| `netstandard2.0` | .NET Framework 4.6.1+, Unity, Xamarin, Mono |
| `net6.0` | Access to `LoggerMessage` source gen, `CallerArgumentExpression` |
| `net8.0` | Frozen collections, keyed DI, `TimeProvider`, AOT improvements |

**Conditional compilation:**
```csharp
#if NET6_0_OR_GREATER
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connected to {Address}")]
    static partial void LogConnected(ILogger logger, string address);
#else
    private static readonly Action<ILogger, string, Exception?> LogConnected =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1), "Connected to {Address}");
#endif
```

Strengthens CS-15.

### CS-44: NuGet package must include SourceLink, deterministic build, symbols [P1]

All NuGet packages MUST include SourceLink for debugging and deterministic builds for reproducibility.

```xml
<PropertyGroup>
    <Deterministic>true</Deterministic>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
</PropertyGroup>

<ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.*" PrivateAssets="All" />
</ItemGroup>
```

Extends CS-16.

### CS-45: AOT / Trimming compatibility annotations [P2]

Mark the assembly as trim-compatible. Avoid reflection-heavy patterns; prefer source generators.

```xml
<PropertyGroup>
    <IsTrimmable>true</IsTrimmable>
    <IsAotCompatible>true</IsAotCompatible>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
</PropertyGroup>
```

**Key rules:**
- Avoid `Type.GetType()` for feature detection — use compile-time `#if` instead.
- Avoid `Activator.CreateInstance()` — use factory methods or DI.
- gRPC code-gen is already trim-safe (no reflection in generated code).
- If reflection is unavoidable, annotate with `[DynamicallyAccessedMembers]`, `[RequiresUnreferencedCode]`.

**WRONG:**
```csharp
// Runtime type detection — breaks trimming
var type = Type.GetType("OpenTelemetry.Trace.TracerProvider, OpenTelemetry");
if (type != null) { /* use OTel */ }
```

**RIGHT:**
```csharp
// Compile-time feature detection
#if KUBEMQ_OTEL
    // Separate NuGet package: KubeMQ.Sdk.OpenTelemetry
    ConfigureOtelExporter(tracerProvider);
#endif
```

**Ecosystem references:**
- .NET blog: "How to make libraries compatible with native AOT."
- DapperAOT: Source generator replacing runtime reflection.

Strengthens CS-13.

---

## Security

### CS-46: Never expose credentials in ToString, exceptions, or logs [P0]

Options classes, exceptions, and log messages MUST NOT include auth tokens, passwords, or TLS certificate data.

**WRONG:**
```csharp
public class KubeMQClientOptions
{
    public string? AuthToken { get; set; }

    public override string ToString()
        => $"Address={Address}, AuthToken={AuthToken}"; // CREDENTIALS LEAKED
}
```

**RIGHT:**
```csharp
public class KubeMQClientOptions
{
    public string? AuthToken { get; set; }

    public override string ToString()
        => $"Address={Address}, AuthToken={(AuthToken is null ? "<not set>" : "<redacted>")}";
}

// In exception messages:
throw new KubeMQAuthenticationException(
    $"Authentication failed for {_options.Address}");
// NOT: $"Authentication failed with token {_options.AuthToken}"
```

**Also applies to:**
- `ILogger` messages — never log tokens, even at Debug level.
- Exception `Data` dictionary — never add credentials.
- `Activity` tags — never tag with tokens.

**Ecosystem references:**
- Azure SDK: `ClientOptions.ToString()` never exposes keys.
- AWS SDK: Credentials are always redacted in logs.

### CS-47: TLS configuration via HttpHandler — NOT SslCredentials [P1]

With `Grpc.Net.Client`, configure TLS via `SocketsHttpHandler.SslOptions`, NOT legacy `SslCredentials`.

**WRONG (Grpc.Core pattern):**
```csharp
var creds = new SslCredentials(rootCert, new KeyCertificatePair(clientCert, clientKey));
var channel = new Channel("server:50000", creds); // Grpc.Core — deprecated
```

**RIGHT (Grpc.Net.Client pattern):**
```csharp
var handler = new SocketsHttpHandler
{
    SslOptions = new SslClientAuthenticationOptions
    {
        ClientCertificates = new X509CertificateCollection
        {
            X509Certificate2.CreateFromPemFile("client.pem", "client.key")
        },
        RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
        {
            return errors == SslPolicyErrors.None;
        }
    }
};

var channel = GrpcChannel.ForAddress("https://server:50000", new GrpcChannelOptions
{
    HttpHandler = handler
});
```

---

## Concurrency (Extended)

### CS-48: SemaphoreSlim vs lock vs Interlocked — when to use each [P2]

Specs must use the correct synchronization primitive:

| Primitive | Use When | Async-Safe? |
|-----------|----------|-------------|
| `lock` | Sync-only critical section, no `await` inside | No |
| `SemaphoreSlim(1,1)` | Async critical section with `await` inside | Yes |
| `Interlocked.*` | Single atomic operation (increment, compare-exchange) | N/A |
| `Channel<T>` | Producer-consumer coordination | Yes |

**WRONG:**
```csharp
// Using lock for a simple counter
private readonly object _lock = new();
private int _count;
public void Increment() { lock (_lock) { _count++; } }
```

**RIGHT:**
```csharp
private int _count;
public void Increment() => Interlocked.Increment(ref _count);
```

**WRONG:**
```csharp
// Using Interlocked for compound check-then-act
if (Interlocked.Read(ref _state) == 0)
{
    Interlocked.Exchange(ref _state, 1); // RACE: another thread can change between read and exchange
}
```

**RIGHT:**
```csharp
if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
{
    // Successfully transitioned from 0 to 1
}
```

Extends CS-11.

### CS-49: Connection state management with Interlocked [P2]

Use `Interlocked.CompareExchange` for thread-safe state transitions in connection lifecycle:

```csharp
private const int StateDisconnected = 0;
private const int StateConnecting = 1;
private const int StateConnected = 2;
private const int StateDisposed = 3;

private int _state = StateDisconnected;

public async Task ConnectAsync(CancellationToken ct = default)
{
    if (Interlocked.CompareExchange(ref _state, StateConnecting, StateDisconnected)
        != StateDisconnected)
    {
        throw new InvalidOperationException(
            $"Cannot connect: current state is {(ConnectionState)_state}");
    }

    try
    {
        await ConnectInternalAsync(ct).ConfigureAwait(false);
        Interlocked.Exchange(ref _state, StateConnected);
    }
    catch
    {
        Interlocked.Exchange(ref _state, StateDisconnected);
        throw;
    }
}
```

**Ecosystem references:**
- NATS.Net: Uses atomic state transitions for `NatsConnection`.
- StackExchange.Redis: Uses internal state flags for connection lifecycle.

---

## Documentation

### CS-50: XML doc comments on all public API members [P1]

Every public type, method, property, and parameter MUST have XML doc comments.

```csharp
/// <summary>
/// Sends a message to the specified KubeMQ channel.
/// </summary>
/// <param name="message">The message to send. Must have a non-empty <see cref="Message.Channel"/>.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A <see cref="SendResult"/> indicating whether the message was accepted.</returns>
/// <exception cref="KubeMQConnectionException">Thrown when the client is not connected.</exception>
/// <exception cref="KubeMQTimeoutException">Thrown when the send exceeds the configured deadline.</exception>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
/// <example>
/// <code>
/// await using var client = new KubeMQClient(new KubeMQClientOptions
/// {
///     Address = "localhost:50000"
/// });
///
/// var result = await client.SendAsync(new Message
/// {
///     Channel = "events",
///     Body = Encoding.UTF8.GetBytes("hello")
/// });
/// </code>
/// </example>
public virtual Task<SendResult> SendAsync(
    Message message,
    CancellationToken cancellationToken = default)
```

**Key rules:**
- `<summary>` — required on all public members.
- `<param>` — required for all parameters.
- `<returns>` — required for non-void methods.
- `<exception>` — list all exceptions the method can throw.
- `<example>` — recommended for top-level API methods.
- `<inheritdoc/>` — use on interface implementations to avoid duplication.
- Enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in the project.

**Ecosystem references:**
- Azure SDK: Comprehensive XML docs on all public APIs with examples.
- StackExchange.Redis: Thorough XML docs.

### CS-51: ObjectDisposedException checks on public methods [P2]

All public methods on disposable types MUST check for disposal and throw `ObjectDisposedException`.

```csharp
private void ThrowIfDisposed()
{
#if NET7_0_OR_GREATER
    ObjectDisposedException.ThrowIf(_disposed, this);
#else
    if (_disposed)
        throw new ObjectDisposedException(GetType().FullName);
#endif
}

public async Task<SendResult> SendAsync(Message message, CancellationToken ct = default)
{
    ThrowIfDisposed();
    ArgumentNullException.ThrowIfNull(message);
    // ...
}
```

**Ecosystem references:**
- Azure SDK: All client methods check for disposal.
- StackExchange.Redis: `ConnectionMultiplexer` throws on disposed access.

Extends CS-2.

---

## Appendix A — K8s Ecosystem SDK Pattern Matrix

| Pattern | Azure SDK | NATS.Net | Confluent.Kafka | StackExchange.Redis | K8s Client | Grpc.Net.Client | AWS SDK |
|---------|-----------|----------|-----------------|---------------------|------------|-----------------|---------|
| Custom exception hierarchy | `RequestFailedException` | `NatsException` | `KafkaException` | `RedisException` | `KubernetesException` | `RpcException` | `AmazonServiceException` |
| Options/Config class | `ClientOptions` | `NatsOpts` | `ProducerConfig` | `ConfigurationOptions` | `KubernetesClientConfiguration` | `GrpcChannelOptions` | `AWSConfigs` |
| DI registration | Manual | `AddNats()` | Manual | `AddStackExchangeRedis()` | Manual | `AddGrpcClient()` | `AddAWSService()` |
| Virtual methods (mock) | Yes (all) | No | No | No | Interface (`IKubernetes`) | N/A | No |
| `IAsyncEnumerable` | Select APIs | `SubscribeAsync` | No | No | `WatchAsync` | `ReadAllAsync()` | No |
| `ActivitySource` tracing | Yes | Yes | No (librdkafka) | Yes | No | Built-in | Yes |
| `Meter` metrics | Yes | No | No (librdkafka) | No | No | Built-in | Yes |
| `IAsyncDisposable` | Select | Yes | No | No | No | Yes | No |
| Auto reconnection | N/A (HTTP) | Yes | Yes (librdkafka) | Yes | N/A (HTTP) | N/A | N/A (HTTP) |
| Singleton connection | N/A | Yes | Builder-owned | `ConnectionMultiplexer` | N/A | Singleton channel | N/A |
| `Channel<T>` buffering | No | Yes | No | No | Yes | No | No |
| Strong naming | Yes | No | No | Yes | No | No | Yes |

---

## Appendix B — Priority Summary

### P0 — Must Add (11 rules)
- CS-18: Exception hierarchy with standard constructors
- CS-19: InnerException chaining
- CS-22: IAsyncDisposable dual implementation
- CS-23: Never use async void
- CS-24: Never use Task.Result/Wait()
- CS-25: CancellationTokenSource disposal
- CS-29: ActivitySource for tracing
- CS-32: GrpcChannel singleton
- CS-36: Use Grpc.Net.Client (not Grpc.Core)
- CS-43: Multi-target framework strategy
- CS-46: Never expose credentials

### P1 — Should Add (16 rules)
- CS-20: Conditional serialization constructors
- CS-21: ValueTask for hot paths
- CS-26: Options pattern for configuration
- CS-27: DI extension methods
- CS-28: Virtual methods and protected constructors for testability
- CS-30: Meter for metrics
- CS-31: LoggerMessage source generators
- CS-33: Automatic reconnection with backoff
- CS-34: IAsyncEnumerable for subscriptions
- CS-37: Interceptor chain for cross-cutting concerns
- CS-38: Deadline/timeout propagation
- CS-40: Record types for DTOs
- CS-42: ArgumentNullException.ThrowIfNull
- CS-44: SourceLink and deterministic builds
- CS-47: TLS via HttpHandler
- CS-50: XML doc comments

### P2 — Nice to Have (8 rules)
- CS-35: Channel\<T\> for producer-consumer
- CS-39: gRPC retry via ServiceConfig
- CS-41: ReadOnlyMemory\<byte\> for payloads
- CS-45: AOT/trimming compatibility
- CS-48: Synchronization primitive comparison
- CS-49: Interlocked state management
- CS-51: ObjectDisposedException checks

---

## Appendix C — Rule Cross-Reference Map

| New Rule | Extends Existing | Section |
|----------|-----------------|---------|
| CS-18 | (new, related to CS-6) | Exception Hierarchy |
| CS-19 | (new) | Exception Hierarchy |
| CS-20 | (new) | Exception Hierarchy |
| CS-21 | CS-1 | Async Patterns |
| CS-22 | CS-2 | Async Patterns |
| CS-23 | CS-1 | Async Patterns |
| CS-24 | (new) | Async Patterns |
| CS-25 | CS-10 | Async Patterns |
| CS-26 | (new) | Builder/Options/Config |
| CS-27 | CS-14 | Builder/Options/Config |
| CS-28 | (new) | Builder/Options/Config |
| CS-29 | CS-13 | Observability |
| CS-30 | (new) | Observability |
| CS-31 | (new) | Observability |
| CS-32 | (new) | Connection & Reconnection |
| CS-33 | (new) | Connection & Reconnection |
| CS-34 | (new) | Streaming & IAsyncEnumerable |
| CS-35 | CS-12 | Streaming & IAsyncEnumerable |
| CS-36 | CS-17 | gRPC-Specific |
| CS-37 | (new) | gRPC-Specific |
| CS-38 | (new) | gRPC-Specific |
| CS-39 | (new) | gRPC-Specific |
| CS-40 | CS-5 | Immutability & Type Design |
| CS-41 | (new) | Immutability & Type Design |
| CS-42 | CS-3 | Nullable Reference Types |
| CS-43 | CS-15 | Build & Packaging |
| CS-44 | CS-16 | Build & Packaging |
| CS-45 | CS-13 | Build & Packaging |
| CS-46 | (new) | Security |
| CS-47 | (new) | Security |
| CS-48 | CS-11 | Concurrency |
| CS-49 | (new) | Concurrency |
| CS-50 | (new) | Documentation |
| CS-51 | CS-2 | Documentation |
