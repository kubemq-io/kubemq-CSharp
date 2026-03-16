# KubeMQ C# / .NET SDK Assessment Report

## Executive Summary

- **Weighted Score (Production Readiness):** 3.9 / 5.0
- **Unweighted Score (Overall Maturity):** 3.8 / 5.0
- **Gating Rule Applied:** No (all Critical-tier categories ≥ 3.0)
- **Feature Parity Gate Applied:** No (< 25% of Category 1 features score 0)
- **Assessment Date:** 2026-03-11
- **SDK Version Assessed:** 3.0.0
- **Repository:** /Users/liornabat/development/projects/kubemq/clients/kubemq-csharp
- **Assessor:** Agent A: Code Quality Architect

### Category Scores

| # | Category | Weight | Score | Grade | Gating? |
|---|----------|--------|-------|-------|---------|
| 1 | API Completeness & Feature Parity | 14% | 4.2 | Strong | Critical |
| 2 | API Design & Developer Experience | 9% | 4.3 | Strong | |
| 3 | Connection & Transport | 11% | 4.0 | Strong | Critical |
| 4 | Error Handling & Resilience | 11% | 4.2 | Strong | Critical |
| 5 | Authentication & Security | 9% | 3.6 | Production-usable | Critical |
| 6 | Concurrency & Thread Safety | 7% | 4.1 | Strong | |
| 7 | Observability | 5% | 4.0 | Strong | |
| 8 | Code Quality & Architecture | 6% | 4.1 | Strong | |
| 9 | Testing | 9% | 3.2 | Production-usable with gaps | |
| 10 | Documentation | 7% | 3.9 | Strong with minor gaps | |
| 11 | Packaging & Distribution | 4% | 3.8 | Production-usable | |
| 12 | Compatibility, Lifecycle & Supply Chain | 4% | 3.3 | Production-usable with gaps | |
| 13 | Performance | 4% | 3.0 | Production-usable with gaps | |

### Top Strengths
1. **Exceptional error handling architecture** — Rich typed exception hierarchy with error codes, categories, retryability classification, and actionable suggestions. Best-in-class among messaging SDKs.
2. **Modern C# idioms** — `IAsyncEnumerable`, `IAsyncDisposable`, `CancellationToken` on every method, `ReadOnlyMemory<byte>` for zero-copy payloads, source-generated logging.
3. **Comprehensive retry and reconnection** — Exponential backoff with jitter, concurrent retry throttling, message buffering during reconnection, and subscription recovery.

### Critical Gaps (Must Fix)
1. **`SendQueueMessagesAsync` is not a true batch** — Iterates individual `SendQueueMessageAsync` calls sequentially instead of using `SendQueueMessagesBatch` gRPC RPC (line 535–554 of `KubeMQClient.cs`). This eliminates the throughput benefit of batching.
2. **`ListChannelsAsync` returns `Array.Empty<ChannelInfo>()`** — The method calls the server but discards the response and returns an empty array (line 842). This is a functional bug.
3. **`AuthInterceptor.GetCachedTokenSync()` uses sync-over-async** — `.GetAwaiter().GetResult()` at line 207 of `AuthInterceptor.cs`. While mitigated by pre-warming, this can deadlock in constrained SynchronizationContext scenarios.

### Not Assessable Items

| Criterion | Reason |
|-----------|--------|
| 9.3.x CI pipeline execution | dotnet CLI not available in assessment environment; CI YAML analyzed by source inspection only |
| 11.1.1 NuGet publication | Cannot verify NuGet package exists without network access to nuget.org |
| 5.2.5 Dependency security | Cannot run `dotnet list package --vulnerable` |
| 11.3.2 Build succeeds | Cannot run `dotnet build` |

---

## Detailed Findings

---

### Category 1: API Completeness & Feature Parity (Score: 4.2)

#### 1.1 Events (Pub/Sub)

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.1.1 | Publish single event | 2 | Verified by source | `KubeMQClient.PublishEventAsync()` at line 280–324. Uses `transport.SendEventAsync()` mapped to gRPC `SendEvent` RPC. Correctly sets `Store = false`. |
| 1.1.2 | Subscribe to events | 2 | Verified by source | `SubscribeToEventsAsync()` at line 344–368. Returns `IAsyncEnumerable<EventReceived>` via gRPC server streaming. |
| 1.1.3 | Event metadata | 2 | Verified by source | `EventMessage` record has `Channel`, `ClientId`, `Body` (`ReadOnlyMemory<byte>`), `Tags` (`IReadOnlyDictionary<string,string>`). No explicit `Metadata` string field — uses Tags instead. Proto `Metadata` field exists but is unused. Score 2: Tags provides equivalent functionality. |
| 1.1.4 | Wildcard subscriptions | 1 | Inferred | No explicit wildcard subscription API or documentation. Server proto supports pattern via channel name. The `EventsSubscription.Channel` accepts `*` patterns but this is not documented or validated. |
| 1.1.5 | Multiple subscriptions | 2 | Verified by source | `IAsyncEnumerable` pattern allows multiple concurrent `await foreach` loops. No singleton restriction on subscriptions. |
| 1.1.6 | Unsubscribe | 2 | Verified by source | Cancellation via `CancellationToken` on `SubscribeToEventsAsync()`. Disposing the `IAsyncEnumerable` enumerator cancels the gRPC stream. Standard C# pattern. |
| 1.1.7 | Group-based subscriptions | 2 | Verified by source | `EventsSubscription.Group` property mapped to `grpcSub.Group` at line 357. |

**Subtotal:** Raw [2,2,2,1,2,2,2] → Normalized [5,5,5,3,5,5,5] → Average: 4.7

#### 1.2 Events Store (Persistent Pub/Sub)

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.2.1 | Publish to events store | 2 | Verified by source | `PublishEventStoreAsync()` at line 371–415. Sets `Store = true` on gRPC Event. |
| 1.2.2 | Subscribe to events store | 2 | Verified by source | `SubscribeToEventStoreAsync()` at line 418–436. Uses `EncodeEventStoreSubscription()` helper. |
| 1.2.3 | StartFromNew | 2 | Verified by source | `EventStoreStartPosition.FromNew` maps to `StartNewOnly` at line 1071. |
| 1.2.4 | StartFromFirst | 2 | Verified by source | `EventStoreStartPosition.FromFirst` maps to `StartFromFirst` at line 1076. |
| 1.2.5 | StartFromLast | 2 | Verified by source | `EventStoreStartPosition.FromLast` maps to `StartFromLast` at line 1080. |
| 1.2.6 | StartFromSequence | 2 | Verified by source | `EventStoreStartPosition.FromSequence` maps to `StartAtSequence` with value at line 1085–1087. |
| 1.2.7 | StartFromTime | 2 | Verified by source | `EventStoreStartPosition.FromTime` maps to `StartAtTime` with `ToUnixTimeSeconds()` at line 1090–1092. |
| 1.2.8 | StartFromTimeDelta | 2 | Verified by source | `EventStoreStartPosition.FromTimeDelta` maps to `StartAtTimeDelta` at line 1095–1097. |
| 1.2.9 | Event store metadata | 2 | Verified by source | Same metadata support as Events — `EventStoreMessage` record has `Channel`, `ClientId`, `Body`, `Tags`. |

**Subtotal:** Raw [2,2,2,2,2,2,2,2,2] → Normalized all 5 → Average: 5.0

#### 1.3 Queues

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.3.1 | Send single message | 2 | Verified by source | `SendQueueMessageAsync()` at line 439–515. Maps to `transport.SendQueueMessageAsync()`. |
| 1.3.2 | Send batch messages | 1 | Verified by source | `SendQueueMessagesAsync()` at line 535–554 loops and calls `SendQueueMessageAsync()` individually. **Does NOT use the `SendQueueMessagesBatch` gRPC RPC** which is available in the proto. This is a functional gap — no atomicity or throughput benefit. |
| 1.3.3 | Receive/Pull messages | 2 | Verified by source | `PollQueueAsync()` at line 557–616 uses `QueuesDownstream` bidirectional streaming for pull. |
| 1.3.4 | Receive with visibility timeout | 2 | Verified by source | `QueuePollRequest` has `WaitTimeoutSeconds` and the downstream request uses `WaitTimeout`. `QueueMessageReceived.ExtendVisibilityAsync()` exists. |
| 1.3.5 | Message acknowledgment | 2 | Verified by source | `QueueMessageReceived` has `AckAsync()`, `RejectAsync()`, `RequeueAsync()`, `ExtendVisibilityAsync()` with exactly-once settlement semantics (Interlocked at line 157–163). |
| 1.3.6 | Queue stream / transaction | 2 | Verified by source | `PollQueueAsync()` uses `QueuesDownstream` bidirectional streaming. Proto defines `StreamQueueMessage` RPC. SDK uses the newer `QueuesDownstream` API. |
| 1.3.7 | Delayed messages | 2 | Verified by source | `QueueMessage.DelaySeconds` mapped to `grpcMsg.Policy.DelaySeconds` at line 457–460. |
| 1.3.8 | Message expiration | 2 | Verified by source | `QueueMessage.ExpirationSeconds` mapped to `grpcMsg.Policy.ExpirationSeconds` at line 463–466. |
| 1.3.9 | Dead letter queue | 2 | Verified by source | `QueueMessage.MaxReceiveCount` and `MaxReceiveQueue` mapped to policy at line 469–477. |
| 1.3.10 | Queue message metadata | 2 | Verified by source | `QueueMessage` record has `Channel`, `Body`, `Tags`, `ClientId`, `DelaySeconds`, `ExpirationSeconds`, `MaxReceiveCount`, `MaxReceiveQueue`. |
| 1.3.11 | Peek messages | 0 | Verified by source | `PeekQueueAsync()` throws `NotSupportedException` at line 619–626. Explicitly not implemented. |
| 1.3.12 | Purge queue | 0 | Verified by source | No `PurgeQueueAsync` method exists. Not in `IKubeMQClient` interface. |

**Subtotal:** Raw [2,1,2,2,2,2,2,2,2,2,0,0] → Normalized [5,3,5,5,5,5,5,5,5,5,1,1] → Average: 4.2

#### 1.4 RPC (Commands & Queries)

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.4.1 | Send command | 2 | Verified by source | `SendCommandAsync()` at line 629–685. Maps to `SendRequest` RPC with `RequestType.Command`. |
| 1.4.2 | Subscribe to commands | 2 | Verified by source | `SubscribeToCommandsAsync()` at line 688–712. Uses `SubscribeToRequests` gRPC streaming. |
| 1.4.3 | Command response | 2 | Verified by source | `SendCommandResponseAsync()` at line 902–930. Maps to `SendResponse` RPC. |
| 1.4.4 | Command timeout | 2 | Verified by source | `CommandMessage.TimeoutInSeconds` mapped to `grpcRequest.Timeout` at line 638–641. Falls back to `options.DefaultTimeout`. |
| 1.4.5 | Send query | 2 | Verified by source | `SendQueryAsync()` at line 715–790. Maps to `SendRequest` RPC with `RequestType.Query`. |
| 1.4.6 | Subscribe to queries | 2 | Verified by source | `SubscribeToQueriesAsync()` at line 793–817. Uses `SubscribeToRequests` gRPC streaming. |
| 1.4.7 | Query response | 2 | Verified by source | `SendQueryResponseAsync()` at line 933–965. Maps to `SendResponse` RPC with body and tags. |
| 1.4.8 | Query timeout | 2 | Verified by source | `QueryMessage.TimeoutInSeconds` mapped to `grpcRequest.Timeout` at line 724–727. |
| 1.4.9 | RPC metadata | 2 | Verified by source | `CommandMessage`/`QueryMessage` have `Channel`, `ClientId`, `Body`, `Tags`, `TimeoutInSeconds`. `QueryMessage` also has `CacheKey`, `CacheTtlSeconds`. |
| 1.4.10 | Group-based RPC | 2 | Verified by source | `CommandsSubscription.Group` and `QueriesSubscription.Group` mapped to gRPC `Subscribe.Group`. |
| 1.4.11 | Cache support for queries | 2 | Verified by source | `QueryMessage.CacheKey` and `CacheTtlSeconds` mapped to `grpcRequest.CacheKey/CacheTTL` at line 739–747. `QueryResponse.CacheHit` returned at line 788. |

**Subtotal:** Raw all 2 → Normalized all 5 → Average: 5.0

#### 1.5 Client Management

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.5.1 | Ping | 2 | Verified by source | `PingAsync()` at line 255–277. Maps to `Ping` RPC. Returns `ServerInfo`. |
| 1.5.2 | Server info | 2 | Verified by source | `ServerInfo` has `Host`, `Version`, `ServerStartTime`, `ServerUpTimeSeconds`. |
| 1.5.3 | Channel listing | 1 | Verified by source | `ListChannelsAsync()` exists at line 820–843 but **returns `Array.Empty<ChannelInfo>()`** — the response is discarded. Functional bug. |
| 1.5.4 | Channel create | 1 | Verified by source | `CreateChannelAsync()` at line 846–871 sends a `Command` Request. Does not clearly map to a server-supported channel creation API; the `channelType` parameter is accepted but not used in the gRPC request. |
| 1.5.5 | Channel delete | 1 | Verified by source | `DeleteChannelAsync()` at line 874–899 has the same issue as CreateChannel — `channelType` parameter accepted but not used. |

**Subtotal:** Raw [2,2,1,1,1] → Normalized [5,5,3,3,3] → Average: 3.8

#### 1.6 Operational Semantics

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.6.1 | Message ordering | 1 | Inferred | SDK does not explicitly document or enforce ordering guarantees. Single gRPC channel with sequential sends implies ordering, but no documentation or tests verify it. |
| 1.6.2 | Duplicate handling | 1 | Inferred | SDK generates unique `MessageID` (Guid) for queue messages. Events don't have dedup. No documentation on delivery semantics beyond README table. |
| 1.6.3 | Large message handling | 2 | Verified by source | `MaxSendSize` and `MaxReceiveSize` configurable at 100MB default. `GrpcChannelOptions.MaxSendMessageSize/MaxReceiveMessageSize` set at line 85–86 of `GrpcTransport.cs`. TROUBLESHOOTING.md documents the issue. |
| 1.6.4 | Empty/null payload | 2 | Verified by source | `EventMessage.Body` defaults to `ReadOnlyMemory<byte>.Empty`. `ByteString.CopyFrom(message.Body.Span)` handles empty spans correctly. Tags can be null. |
| 1.6.5 | Special characters | 1 | Inferred | No explicit tests for Unicode/binary in metadata or tags. Proto3 strings are UTF-8 by spec, so this should work, but no SDK-level validation or tests. |

**Subtotal:** Raw [1,1,2,2,1] → Normalized [3,3,5,5,3] → Average: 3.8

#### 1.7 Cross-SDK Feature Parity Matrix

Deferred — will be populated after all SDKs are assessed.

#### Category 1 Overall Score Calculation

Category averages: Events 4.7, EventsStore 5.0, Queues 4.2, RPC 5.0, Management 3.8, Semantics 3.8

**Category 1 Score: (4.7 + 5.0 + 4.2 + 5.0 + 3.8 + 3.8) / 6 = 4.4**

Feature parity gate check: 2 features score 0 (Peek, Purge) out of 48 total = 4.2%. Under 25% threshold — gate not applied.

Adjusting slightly down to **4.2** due to the ListChannels bug and batch-not-truly-batch issue being functionally significant.

---

### Category 2: API Design & Developer Experience (Score: 4.3)

#### 2.1 Language Idiomaticity

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.1.1 | Naming conventions | 5 | Verified by source | PascalCase throughout. `PublishEventAsync`, `SendQueueMessageAsync`, `SubscribeToCommandsAsync`. Properties: `MaxRetries`, `InitialBackoff`. Perfect C# naming. |
| 2.1.2 | Configuration pattern | 5 | Verified by source | `KubeMQClientOptions` with `{ get; set; }` per .NET Options pattern. `services.Configure<KubeMQClientOptions>()` in DI. `Validate()` called in constructor. |
| 2.1.3 | Error handling pattern | 5 | Verified by source | Typed exception hierarchy: `KubeMQException` → `KubeMQConnectionException`, `KubeMQAuthenticationException`, etc. Standard C# exception pattern. |
| 2.1.4 | Async pattern | 5 | Verified by source | All I/O methods return `Task`/`Task<T>`. Subscriptions return `IAsyncEnumerable<T>`. `ConfigureAwait(false)` everywhere. |
| 2.1.5 | Resource cleanup | 5 | Verified by source | `IDisposable` + `IAsyncDisposable`. Dual-dispose contract documented in `IKubeMQClient` remarks. `await using` pattern. GC.SuppressFinalize. Idempotent via `Interlocked.CompareExchange`. |
| 2.1.6 | Collection types | 5 | Verified by source | `IReadOnlyDictionary<string,string>` for tags, `IReadOnlyList<ChannelInfo>` for channel listing, `ReadOnlyMemory<byte>` for body. Standard BCL types throughout. |
| 2.1.7 | Null/optional handling | 4 | Verified by source | `<Nullable>enable</Nullable>` in csproj. Nullable annotations on `AuthToken?`, `Tags?`, `ClientId?`. Some minor gaps: `channelType` parameter in `CreateChannelAsync` should be an enum. |

**Subtotal:** 4.9

#### 2.2 Progressive Disclosure & Minimal Boilerplate

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.2.1 | Quick start simplicity | 5 | Verified by source | README shows publish in 5 lines: create client, connect, publish. Subscribe in 5 lines. Excellent. |
| 2.2.2 | Sensible defaults | 5 | Verified by source | `Address = "localhost:50000"`, `DefaultTimeout = 5s`, `ConnectionTimeout = 10s`, retry enabled by default, reconnect enabled. Only address needed for basic usage. |
| 2.2.3 | Opt-in complexity | 5 | Verified by source | TLS, auth, retry, keepalive all additive configuration on `KubeMQClientOptions`. None required for basic operation. |
| 2.2.4 | Consistent method signatures | 4 | Verified by source | All publish methods: `Task Publish*(message, ct)`. All subscribe: `IAsyncEnumerable<T> SubscribeTo*(subscription, ct)`. Slight inconsistency: `PollQueueAsync` takes `QueuePollRequest` while publishes take message objects directly. |
| 2.2.5 | Discoverability | 4 | Verified by source | All public types have XML doc comments. `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in csproj. Some internal types lack `<summary>` (suppressed by `CS1591` NoWarn). DocFX configured. |

**Subtotal:** 4.6

#### 2.3 Type Safety & Generics

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.3.1 | Strong typing | 4 | Verified by source | `EventMessage`, `QueueMessage`, `CommandMessage`, `QueryMessage` are strongly typed records/classes. No `object`/`any` abuse. Minor: `channelType` parameter is `string` not enum. |
| 2.3.2 | Enum/constant usage | 5 | Verified by source | `EventStoreStartPosition`, `ConnectionState`, `KubeMQErrorCode`, `KubeMQErrorCategory`, `BufferFullMode`, `JitterMode`, `SubscribeType` all properly typed enums. |
| 2.3.3 | Return types | 4 | Verified by source | `Task<QueueSendResult>`, `Task<CommandResponse>`, `Task<QueryResponse>`, `IAsyncEnumerable<EventReceived>`. One issue: `ListChannelsAsync` returns `Task<IReadOnlyList<ChannelInfo>>` but always returns empty. |

**Subtotal:** 4.3

#### 2.4 API Consistency

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.4.1 | Internal consistency | 4 | Verified by source | All operations follow validate → wait-for-ready → build-grpc-msg → execute-with-retry → map-response pattern. Telemetry and metrics consistently applied. |
| 2.4.2 | Cross-SDK concept alignment | 3 | Inferred | Single `KubeMQClient` class (not pattern-specific clients like some SDKs). Core concepts present: Event, Queue, Command, Query. Cannot fully assess without other SDKs. |
| 2.4.3 | Method naming alignment | 3 | Inferred | `PublishEventAsync`, `SendQueueMessageAsync`, `SendCommandAsync`, `SendQueryAsync`. "Publish" vs "Send" distinction is intentional (fire-and-forget vs request-response). Cannot confirm cross-SDK alignment. |
| 2.4.4 | Option/config alignment | 3 | Inferred | `Address`, `ClientId`, `AuthToken`, `Tls`, `Retry`, `Reconnect`. Standard field names. Cannot confirm cross-SDK alignment. |

**Subtotal:** 3.3

#### 2.5 Developer Journey Walkthrough

| Step | Assessment | Friction Points |
|------|-----------|-----------------|
| **1. Install** | 5/5 — `dotnet add package KubeMQ.SDK.CSharp`. Single package, no native deps. | None. Clean install. |
| **2. Connect** | 5/5 — `new KubeMQClient(new KubeMQClientOptions())` + `await client.ConnectAsync()`. | Must explicitly call ConnectAsync. DI variant auto-connects via HostedService. |
| **3. First Publish** | 5/5 — `await client.PublishEventAsync(new EventMessage { Channel = "ch", Body = bytes })`. | Clear, minimal. Convenience overload available. |
| **4. First Subscribe** | 5/5 — `await foreach (var msg in client.SubscribeToEventsAsync(sub))`. | Idiomatic C# 8.0+ pattern. |
| **5. Error Handling** | 4/5 — Typed exceptions with `IsRetryable`, `ErrorCode`, `Category`. Auto-retry handles most transients. | New users may not discover the exception hierarchy without reading docs. |
| **6. Production Config** | 4/5 — TLS, auth, retry, keepalive all configurable. DI integration with `appsettings.json` binding. | No OIDC out of the box; requires custom `ICredentialProvider`. |
| **7. Troubleshooting** | 4/5 — TROUBLESHOOTING.md covers 11+ issues with code examples. Structured logging with event IDs. | Error messages include "Suggestion:" text which is excellent. |

**Developer Journey Score: 4.6**

**Category 2 Overall: (4.9 + 4.6 + 4.3 + 3.3 + 4.6) / 5 = 4.3**

---

### Category 3: Connection & Transport (Score: 4.0)

#### 3.1 gRPC Implementation

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.1.1 | gRPC client setup | 5 | Verified by source | `GrpcTransport.ConnectAsync()` at line 68–141: `GrpcChannel.ForAddress()` with `GrpcChannelOptions`, `SocketsHttpHandler`, interceptor chain. Properly disposes on failure. |
| 3.1.2 | Protobuf alignment | 4 | Verified by source | SDK proto at `src/KubeMQ.Sdk/Proto/kubemq.proto` includes `QueuesDownstream`/`QueuesUpstream`/`QueuesInfo` RPCs that the server proto at `/tmp/kubemq-protobuf/kubemq.proto` does NOT have. SDK proto is a superset — this is fine for forward compatibility. SDK namespace changed to `KubeMQ.Grpc`. |
| 3.1.3 | Proto version | 4 | Verified by source | SDK proto has additional RPCs (`QueuesDownstream`, `QueuesUpstream`, `QueuesInfo`) beyond the reference proto. All base messages match field numbers. |
| 3.1.4 | Streaming support | 4 | Verified by source | `SubscribeToEvents` uses server streaming via `client.SubscribeToEvents()`. `PollQueueAsync` uses bidirectional streaming via `client.QueuesDownstream()`. `SendEventsStream` (event streaming publish) is defined in proto but NOT exposed in the SDK API. |
| 3.1.5 | Metadata passing | 5 | Verified by source | `AuthInterceptor` at `AuthInterceptor.cs` line 154–171 adds `authorization` header to all four call types (unary, server streaming, client streaming, duplex). |
| 3.1.6 | Keepalive | 5 | Verified by source | `KeepaliveOptions` configurable. `CreateHandler()` at `GrpcTransport.cs` line 407–419 sets `KeepAlivePingDelay`, `KeepAlivePingTimeout`, `KeepAlivePingPolicy`. Proper `HttpKeepAlivePingPolicy.Always` when `PermitWithoutStream`. |
| 3.1.7 | Max message size | 5 | Verified by source | `KubeMQClientOptions.MaxSendSize` (100MB default) and `MaxReceiveSize` (100MB) mapped to `GrpcChannelOptions.MaxSendMessageSize/MaxReceiveMessageSize` at line 85-86. Validated in `Validate()`. |
| 3.1.8 | Compression | 1 | Verified by source | No gRPC compression support found. No `GrpcChannelOptions.CompressionProviders` or `CallOptions.WithWriteOptions()`. |

**Subtotal:** 4.1

#### 3.2 Connection Lifecycle

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.2.1 | Connect | 5 | Verified by source | `ConnectAsync()` at line 191–245: state machine transition, `transport.ConnectAsync()`, ping verification, compatibility check. Proper error rollback. |
| 3.2.2 | Disconnect/close | 5 | Verified by source | `DisposeAsyncCore()` at line 1009–1054: cancels shutdown CTS, drains buffer/callbacks, closes transport, forces state to Disposed. Idempotent via Interlocked. |
| 3.2.3 | Auto-reconnection | 4 | Verified by source | `ConnectionManager.ReconnectLoopAsync()` at line 220–277. Reconnects, flushes buffer, resubscribes. However, `OnConnectionLost()` is defined but the trigger mechanism (detecting disconnection during normal operation) is not clearly wired from the streaming layer. |
| 3.2.4 | Reconnection backoff | 5 | Verified by source | `CalculateBackoffDelay()` at line 206–218: exponential backoff with full jitter using `Math.Pow(multiplier, attempt-1)` capped at `MaxDelay`. |
| 3.2.5 | Connection state events | 5 | Verified by source | `StateChanged` event with `ConnectionStateChangedEventArgs` (PreviousState, CurrentState, Timestamp, Error). `ConnectionState` enum: Disconnected, Connecting, Connected, Reconnecting, Disposed. Events fired asynchronously via `Task.Run`. |
| 3.2.6 | Subscription recovery | 4 | Verified by source | `StreamManager.ResubscribeAllAsync()` at line 41–58. Tracks subscriptions in `ConcurrentDictionary`. Adjusts EventsStore to `StartAtSequence` from last known sequence. But: subscription tracking registration is not visibly called from the public subscribe methods. |
| 3.2.7 | Message buffering during reconnect | 4 | Verified by source | `ReconnectBuffer` uses `Channel<T>` with byte-level tracking at line 43–61. Configurable `BufferSize` (8MB default) and `BufferFullMode` (Block or DropWrite). `KubeMQBufferFullException` thrown when full. However, the buffering call path from `PublishEventAsync` during reconnecting state is not clearly visible. |
| 3.2.8 | Connection timeout | 5 | Verified by source | `ConnectionTimeout` (10s default) enforced via `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter` at `GrpcTransport.cs` line 104–105. Throws `KubeMQTimeoutException`. |
| 3.2.9 | Request timeout | 4 | Verified by source | `DefaultTimeout` (5s default) used for `WaitForReady` timeout. Per-operation timeout via `CommandMessage.TimeoutInSeconds`/`QueryMessage.TimeoutInSeconds`. Events don't have per-operation timeout — rely on gRPC default. |

**Subtotal:** 4.6

#### 3.3 TLS / mTLS

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.3.1 | TLS support | 5 | Verified by source | `TlsConfigurator.ConfigureTls()` at line 32–87. Sets `SslClientAuthenticationOptions` on handler. URI switches to `https://` when TLS enabled. |
| 3.3.2 | Custom CA certificate | 5 | Verified by source | `TlsOptions.CaFile` and `CaCertificatePem`. `LoadCaCertificate()` at line 118–139. Custom CA validation via `RemoteCertificateValidationCallback` with `X509ChainTrustMode.CustomRootTrust`. |
| 3.3.3 | mTLS support | 5 | Verified by source | `TlsOptions.CertFile`/`KeyFile` and `ClientCertificatePem`/`ClientKeyPem`. `LoadClientCertificates()` at line 89–116 uses `X509Certificate2.CreateFromPemFile()`. |
| 3.3.4 | TLS configuration | 4 | Verified by source | `TlsOptions.MinTlsVersion` (default TLS 1.2). Enforces TLS 1.2+ with validation. `SslProtocols.Tls12 | Tls13`. No cipher suite selection (platform-dependent). |
| 3.3.5 | Insecure mode | 5 | Verified by source | `TlsOptions.InsecureSkipVerify = true` bypasses validation. Logs WARNING via `Log.InsecureConnection()`. Pragma `CA5359` suppressed with justification. |

**Subtotal:** 4.8

#### 3.4 Kubernetes-Native Behavior

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.4.1 | K8s DNS service discovery | 4 | Verified by source | Default `localhost:50000` for sidecar. COMPATIBILITY.md documents both sidecar and standalone DNS patterns. TROUBLESHOOTING.md shows K8s DNS address. |
| 3.4.2 | Graceful shutdown APIs | 4 | Verified by source | `DisposeAsync()` drains buffer and callbacks. `IHostedService` in DI scenario. No explicit SIGTERM handler documentation, but the dispose pattern integrates with ASP.NET host lifecycle. |
| 3.4.3 | Health/readiness integration | 4 | Verified by source | `client.State` property returns `ConnectionState`. Could be used in K8s health probes. No built-in ASP.NET Health Check integration. |
| 3.4.4 | Rolling update resilience | 4 | Verified by source | Auto-reconnection with backoff and subscription recovery handles pod restarts. Buffer preserves messages during brief disconnections. |
| 3.4.5 | Sidecar vs. standalone | 3 | Verified by source | COMPATIBILITY.md mentions both patterns. README shows `localhost:50000` for sidecar. Could have more detailed K8s deployment examples. |

**Subtotal:** 3.8

#### 3.5 Flow Control & Backpressure

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.5.1 | Publisher flow control | 3 | Verified by source | `ReconnectBuffer` with `BufferFullMode.Block`/`DropWrite` during reconnection. No publisher-side flow control during normal connected state. |
| 3.5.2 | Consumer flow control | 2 | Verified by source | No configurable prefetch or buffer size for consumers. `IAsyncEnumerable` provides natural backpressure (consumer pulls at its own rate), but no explicit buffer configuration. |
| 3.5.3 | Throttle detection | 3 | Verified by source | `GrpcErrorMapper` maps `ResourceExhausted` to `KubeMQErrorCategory.Throttling` with `IsRetryable = true`. Retry handler extends backoff. |
| 3.5.4 | Throttle error surfacing | 4 | Verified by source | `ResourceExhausted` error message: "Server is rate-limiting. The SDK will retry with extended backoff." TROUBLESHOOTING.md has a section on rate limiting. |

**Subtotal:** 3.0

**Category 3 Overall: (4.1 + 4.6 + 4.8 + 3.8 + 3.0) / 5 = 4.1, adjusted to 4.0**

---

### Category 4: Error Handling & Resilience (Score: 4.2)

#### 4.1 Error Classification & Types

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.1.1 | Typed errors | 5 | Verified by source | `KubeMQException` base + 9 subtypes: `ConnectionException`, `AuthenticationException`, `TimeoutException`, `OperationException`, `ConfigurationException`, `StreamBrokenException`, `BufferFullException`, `RetryExhaustedException`, `PartialFailureException`. |
| 4.1.2 | Error hierarchy | 5 | Verified by source | `KubeMQErrorCategory` enum with 10 categories: Transient, Timeout, Throttling, Authentication, Authorization, Validation, NotFound, Fatal, Cancellation, Backpressure. `KubeMQErrorCode` enum with 21 codes. |
| 4.1.3 | Retryable classification | 5 | Verified by source | `KubeMQException.IsRetryable` property. `GrpcErrorMapper.ClassifyStatus()` maps each gRPC status to retryable/non-retryable. `RetryHandler.ShouldRetry()` checks `IsRetryable` and idempotency safety. |
| 4.1.4 | gRPC status mapping | 5 | Verified by source | `GrpcErrorMapper.MapException()` maps all 16 gRPC status codes to typed exceptions. Handles client-initiated vs server-initiated Cancelled differently. TLS error refinement via `ClassifyTlsErrorOrConnection()`. |
| 4.1.5 | Error wrapping/chaining | 5 | Verified by source | `InnerException` always preserves original `RpcException`. Additional context: `Operation`, `Channel`, `ServerAddress`, `GrpcStatusCode`, `Timestamp`. |

**Subtotal:** 5.0

#### 4.2 Error Message Quality

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.2.1 | Actionable messages | 5 | Verified by source | `FormatMessage()` at `GrpcErrorMapper.cs` line 155–162 produces: `"{operation} failed on channel \"{channel}\": {detail} (server: {address}). Suggestion: {suggestion}"`. Every error has a "Suggestion:" suffix. |
| 4.2.2 | Context inclusion | 5 | Verified by source | `KubeMQException` includes `Operation`, `Channel`, `ServerAddress`, `GrpcStatusCode`, `Timestamp`, `ErrorCode`, `Category`. |
| 4.2.3 | No swallowed errors | 4 | Verified by source | Generally excellent — errors re-thrown with context. Two minor cases: (1) compatibility check in `ConnectAsync` swallows errors silently (line 231–235), (2) `StateChanged` handler exceptions caught and logged but not propagated (line 1346–1350). Both are intentional design choices. |
| 4.2.4 | Consistent format | 4 | Verified by source | All gRPC-originated errors follow the `FormatMessage` template. Validation errors from `MessageValidator` use a different format: "EventMessage: Channel is required." Slightly inconsistent. |

**Subtotal:** 4.5

#### 4.3 Retry & Backoff

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.3.1 | Automatic retry | 5 | Verified by source | `RetryHandler.ExecuteWithRetryAsync()` at line 55–163. All operations wrapped via `ExecuteWithRetryAsync()` in `KubeMQClient`. |
| 4.3.2 | Exponential backoff | 5 | Verified by source | `CalculateDelay()` at line 218–232: `min(maxMs, baseMs * pow(multiplier, attempt-1))`. Three jitter modes: Full, Equal, None. Full jitter default. |
| 4.3.3 | Configurable retry | 5 | Verified by source | `RetryPolicy` with `MaxRetries` (0–10), `InitialBackoff` (50ms–5s), `MaxBackoff` (1s–120s), `BackoffMultiplier` (1.5–3.0), `JitterMode`, `MaxConcurrentRetries` (0–100). All validated. |
| 4.3.4 | Retry exhaustion | 5 | Verified by source | `KubeMQRetryExhaustedException` thrown at line 147–154 with: attempt count, total elapsed, suggestion, inner exception. Clear message: "all {N} retry attempts exhausted over {X}s". |
| 4.3.5 | Non-retryable bypass | 5 | Verified by source | `ShouldRetry()` at line 203–216: returns false for `!IsRetryable`. Also: non-idempotent operations skip timeout retry (`isSafeToRetryOnTimeout = false` for queue send, command, query). |

**Subtotal:** 5.0

#### 4.4 Resilience Patterns

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.4.1 | Timeout on all operations | 4 | Verified by source | Connection: `ConnectionTimeout` enforced. Command/Query: per-operation timeout via `Timeout` field. Ping: uses caller's CT. Events publish: no explicit deadline — relies on gRPC channel defaults. |
| 4.4.2 | Cancellation support | 5 | Verified by source | Every public async method accepts `CancellationToken`. `[EnumeratorCancellation]` on `IAsyncEnumerable` methods. `ThrowIfCancellationRequested()` in retry loop. |
| 4.4.3 | Graceful degradation | 3 | Verified by source | `SendQueueMessagesAsync` fails on first error in the loop — remaining messages not sent. No partial-failure reporting for batch operations. `KubeMQPartialFailureException` exists but is never thrown. `WaitForReady` blocks operations during reconnection rather than failing fast (configurable). |
| 4.4.4 | Resource leak prevention | 4 | Verified by source | Dispose pattern with `Interlocked.CompareExchange` for idempotency. `GrpcTransport.ConnectAsync()` properly disposes handler on failure (line 117–132). `using var call` on all streaming calls. Minor: `DisposeChannel()` sets `grpcClient = null` without thread safety considerations. |

**Subtotal:** 4.0

**Category 4 Overall: (5.0 + 4.5 + 5.0 + 4.0) / 4 = 4.6, adjusted to 4.2** (accounting for the batch degradation issue being significant)

---

### Category 5: Authentication & Security (Score: 3.6)

#### 5.1 Authentication Methods

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 5.1.1 | JWT token auth | 5 | Verified by source | `KubeMQClientOptions.AuthToken` passed to `AuthInterceptor` as static token. Injected in gRPC `authorization` metadata header. |
| 5.1.2 | Token refresh | 4 | Verified by source | `ICredentialProvider` with `GetTokenAsync()`. Token caching with proactive refresh (30s before expiry at line 238–245). `InvalidateCachedToken()` on UNAUTHENTICATED response. Pre-warms cache during `ConnectAsync`. Minor: sync-over-async in `GetCachedTokenSync()`. |
| 5.1.3 | OIDC integration | 2 | Inferred | No built-in OIDC provider. `ICredentialProvider` interface allows custom implementation, but no example or documentation for OIDC flow. |
| 5.1.4 | Multiple auth methods | 3 | Verified by source | Supports: static token (`AuthToken`), dynamic provider (`ICredentialProvider`), mTLS (via `TlsOptions`). Provider takes precedence over static token. No method-switching at runtime. |

**Subtotal:** 3.5

#### 5.2 Security Best Practices

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 5.2.1 | Secure defaults | 2 | Verified by source | TLS is **disabled** by default (`TlsOptions.Enabled = false`). Default address `localhost:50000` is plaintext. Insecure is the default — requires explicit TLS opt-in. |
| 5.2.2 | No credential logging | 5 | Verified by source | `KubeMQClientOptions.ToString()` redacts AuthToken: `AuthToken = <redacted>` at line 153. `CredentialProvider` shown as type name only. Log messages use `token_present=true/false`, never the token value. |
| 5.2.3 | Credential handling | 4 | Verified by source | Token passed via gRPC metadata header (line 167). Not persisted to disk. `StaticTokenProvider` wraps raw string but doesn't store to file. Examples use placeholder tokens. Minor: `AuthInterceptor` stores `cachedToken` as `volatile string?` in memory — could be `SecureString` but this is standard practice. |
| 5.2.4 | Input validation | 4 | Verified by source | `MessageValidator` validates all outgoing messages: channel required, non-negative delays/expirations. `KubeMQClientOptions.Validate()` checks all config values. `TlsOptions.Validate()` checks file existence and consistency. |
| 5.2.5 | Dependency security | N/A | Not assessable | Cannot run `dotnet list package --vulnerable`. Dependencies are current versions (`Google.Protobuf 3.*`, `Grpc.Net.Client 2.*`, `Microsoft.Extensions.* 8.*`). No deprecated packages. |

**Subtotal (excl. N/A):** 3.75

**Category 5 Overall: (3.5 + 3.75) / 2 = 3.6**

---

### Category 6: Concurrency & Thread Safety (Score: 4.1)

#### 6.1 Thread Safety

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.1.1 | Client thread safety | 4 | Verified by source | XML doc: `<threadsafety static="true" instance="true"/>`. `IKubeMQClient` interface documents thread safety. State machine uses `Interlocked.CompareExchange` and `SemaphoreSlim`. Minor concern: `GrpcTransport.DisposeChannel()` sets `grpcClient = null` without synchronization. |
| 6.1.2 | Publisher thread safety | 4 | Verified by source | `PublishEventAsync` is stateless per-call (creates new gRPC event each time). gRPC client is thread-safe by design. `RetryHandler` uses per-attempt state with throttle semaphore. |
| 6.1.3 | Subscriber thread safety | 4 | Verified by source | Each subscription creates its own gRPC streaming call. `IAsyncEnumerable` instances are independent. `StreamManager` uses `ConcurrentDictionary`. |
| 6.1.4 | Documentation of guarantees | 5 | Verified by source | `<threadsafety static="true" instance="true"/>` XML tags on `KubeMQClient`, `IKubeMQClient`. `KubeMQClientOptions` explicitly documented as NOT thread-safe. `QueueMessageReceived` documents thread safety for settle methods. |
| 6.1.5 | Concurrency correctness validation | 2 | Verified by source | No concurrent stress tests found. No race-condition-specific tests. Unit tests are sequential. No `ParallelOptions` or `Task.WhenAll` test patterns. |

**Subtotal:** 3.8

#### 6.2 C#-Specific

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.2.C1 | async/await | 5 | Verified by source | All I/O methods are async. `ConfigureAwait(false)` used consistently throughout. No sync I/O methods on the public API. |
| 6.2.C2 | CancellationToken | 5 | Verified by source | Every async method accepts `CancellationToken = default`. `[EnumeratorCancellation]` on `IAsyncEnumerable` methods. Used in retry loops, connection timeout, drain. |
| 6.2.C3 | IAsyncDisposable | 5 | Verified by source | `IKubeMQClient : IDisposable, IAsyncDisposable`. `DisposeAsyncCore()` with proper dual-dispose pattern. `GC.SuppressFinalize(this)` in both paths. |
| 6.2.C4 | No sync-over-async | 3 | Verified by source | `AuthInterceptor.GetCachedTokenSync()` at line 179–236 uses `.GetAwaiter().GetResult()`. Comment acknowledges this as a "Known limitation — sync-over-async" due to gRPC interceptor API being synchronous. Mitigated by pre-warming in ConnectAsync. `InvalidateCachedToken()` uses `tokenLock.Wait()` (sync) instead of `WaitAsync()`. Also, `Dispose(bool)` calls `connectionManager?.DisposeAsync().AsTask().GetAwaiter().GetResult()` at line 998. |

**Subtotal:** 4.5

**Category 6 Overall: (3.8 + 4.5) / 2 = 4.1**

---

### Category 7: Observability (Score: 4.0)

#### 7.1 Logging

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 7.1.1 | Structured logging | 5 | Verified by source | `LoggerMessage` source generators used throughout `Log.cs` (30+ log messages). Example: `[LoggerMessage(EventId = 200, Level = LogLevel.Information, Message = "Connected to {Address}")]`. High-performance, zero-allocation when disabled. |
| 7.1.2 | Configurable log level | 5 | Verified by source | Uses `Microsoft.Extensions.Logging.Abstractions`. Log levels: Debug, Information, Warning, Error applied appropriately. User controls via standard `appsettings.json` log level configuration. |
| 7.1.3 | Pluggable logger | 5 | Verified by source | `KubeMQClientOptions.LoggerFactory` accepts `ILoggerFactory`. Defaults to `NullLoggerFactory.Instance`. Standard MEL integration. |
| 7.1.4 | No stdout/stderr spam | 5 | Verified by source | All output through `ILogger`. CHANGELOG notes removal of `Console.WriteLine` from v2. No `Console.*` calls in v3 source. |
| 7.1.5 | Sensitive data exclusion | 5 | Verified by source | Auth tokens redacted in `ToString()`. Log messages use `token_present=true/false`. No message body/payload logging. |
| 7.1.6 | Context in logs | 5 | Verified by source | Log messages include: Address, Channel, Group, attempt count, delay, error type, state transitions. Each log has unique EventId (200–601). |

**Subtotal:** 5.0

#### 7.2 Metrics

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 7.2.1 | Metrics hooks | 5 | Verified by source | `KubeMQMetrics` uses `System.Diagnostics.Metrics.Meter` (built into .NET 6+). No OTel NuGet dependency. |
| 7.2.2 | Key metrics exposed | 5 | Verified by source | 7 instruments: `messaging.client.operation.duration` (Histogram), `messaging.client.sent.messages` (Counter), `messaging.client.consumed.messages` (Counter), `messaging.client.connection.count` (UpDownCounter), `kubemq.client.reconnections` (Counter), `kubemq.client.retry.attempts` (Counter), `kubemq.client.retry.exhausted` (Counter). |
| 7.2.3 | Prometheus/OTel compatible | 4 | Verified by source | `System.Diagnostics.Metrics` is OTel-compatible. Users add `OpenTelemetry.Exporter.Prometheus` NuGet to export. Semantic conventions follow OTel messaging spec. Cardinality protection via `ShouldIncludeChannel()`. |
| 7.2.4 | Opt-in | 5 | Verified by source | Meter only emits when a listener subscribes (e.g., `MeterListener`). ActivitySource returns null when no listener — near-zero overhead when disabled. |

**Subtotal:** 4.75

#### 7.3 Tracing

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 7.3.1 | Trace context propagation | 3 | Verified by source | `TextMapCarrier.cs` exists for W3C trace context propagation via message tags. `KubeMQActivitySource` starts activities. However, trace context injection into message tags is not visibly called in the publish path — only activity creation. |
| 7.3.2 | Span creation | 4 | Verified by source | `StartProducerActivity()`, `StartConsumerActivity()`, `StartClientActivity()`, `StartServerActivity()`. Producer spans created in `PublishEventAsync`, `PublishEventStoreAsync`. Client spans for queue/command/query. |
| 7.3.3 | OTel integration | 4 | Verified by source | Uses `System.Diagnostics.ActivitySource` (OTel-compatible since .NET 5). Semantic conventions: `messaging.system`, `messaging.operation.name`, `messaging.destination.name`, `server.address`, `server.port`. |
| 7.3.4 | Opt-in | 5 | Verified by source | `ActivitySource.StartActivity()` returns null when no listener. All call sites check `if (activity is null)`. Zero overhead when tracing disabled. |

**Subtotal:** 4.0

**Category 7 Overall: (5.0 + 4.75 + 4.0) / 3 = 4.6, adjusted to 4.0** (trace context propagation gap is significant for distributed systems)

---

### Category 8: Code Quality & Architecture (Score: 4.1)

#### 8.1 Code Structure

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.1.1 | Package/module organization | 5 | Verified by source | Clear namespace hierarchy: `KubeMQ.Sdk.Client`, `.Events`, `.EventsStore`, `.Queues`, `.Commands`, `.Queries`, `.Auth`, `.Config`, `.Exceptions`, `.Common`, `.DependencyInjection`, `.Internal.Transport`, `.Internal.Protocol`, `.Internal.Telemetry`, `.Internal.Logging`. |
| 8.1.2 | Separation of concerns | 5 | Verified by source | Transport (`GrpcTransport`, `ConnectionManager`), Protocol (`RetryHandler`, `GrpcErrorMapper`, `MessageValidator`, `AuthInterceptor`), Telemetry (`KubeMQActivitySource`, `KubeMQMetrics`), Config (`TlsOptions`, `RetryPolicy`, etc.) all cleanly separated. |
| 8.1.3 | Single responsibility | 4 | Verified by source | `KubeMQClient` is the main entry point — it's large (1443 lines) but well-organized with clear method responsibilities. Could benefit from extracting pattern-specific logic into internal helpers. |
| 8.1.4 | Interface-based design | 5 | Verified by source | `IKubeMQClient` interface for mocking. `ITransport` internal interface for testing transport layer. `ICredentialProvider` for auth extensibility. `InternalsVisibleTo` for test project. |
| 8.1.5 | No circular dependencies | 5 | Verified by source | Clean dependency flow: Client → Internal.Transport/Protocol → Config/Exceptions. No circular imports detected. |
| 8.1.6 | Consistent file structure | 5 | Verified by source | One type per file. Files named after their type. Consistent directory structure matching namespaces. |
| 8.1.7 | Public API surface isolation | 5 | Verified by source | `Internal` namespace with `internal` visibility. Public API: `Client/`, `Events/`, `Queues/`, etc. `InternalsVisibleTo` only for test project. `PrivateAssets=All` on build-time dependencies. |

**Subtotal:** 4.9

#### 8.2 Code Quality

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.2.1 | Linter compliance | 5 | Verified by source | `TreatWarningsAsErrors = true`, `EnforceCodeStyleInBuild = true`, `AnalysisLevel = latest-recommended`, `Microsoft.CodeAnalysis.NetAnalyzers 9.*`, `StyleCop.Analyzers 1.2.0-beta.556`. CI runs `dotnet format --verify-no-changes` and `-warnaserror`. |
| 8.2.2 | No dead code | 4 | Verified by source | Some potentially unused infrastructure: `CallbackDispatcher.cs`, `InFlightCallbackTracker.cs` have tracking mechanisms but their integration points are not all visible. `KubeMQPartialFailureException` exists but is never thrown. |
| 8.2.3 | Consistent formatting | 5 | Verified by source | `dotnet format` enforced in CI. `.editorconfig` present. StyleCop analyzers active. |
| 8.2.4 | Meaningful naming | 5 | Verified by source | Clear, descriptive names throughout: `CalculateBackoffDelay`, `ThrowIfDisposed`, `WaitForReadyAsync`, `MapToEventReceived`, `ClassifyTlsErrorOrConnection`. |
| 8.2.5 | Error path completeness | 4 | Verified by source | Generally excellent. All gRPC calls wrapped in try/catch with mapping. Minor: `ConnectAsync` compatibility check swallows all exceptions. `ListChannelsAsync` discards the response. |
| 8.2.6 | Magic number/string avoidance | 4 | Verified by source | `SemanticConventions` class for OTel strings. `OperationDefaults` for default values. Some inline strings remain: `"PublishEvent"`, `"SendQueueMessage"` passed as operation names. Default port `50000` appears in multiple places. |
| 8.2.7 | Code duplication | 3 | Verified by source | `ParseAddress()` duplicated between `KubeMQClient.cs` (line 1240) and `GrpcTransport.cs` (line 386). `CopyTags()` pattern repeated. Subscribe methods follow similar structure that could be extracted. |

**Subtotal:** 4.3

#### 8.3 Serialization & Message Handling

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.3.1 | JSON marshaling helpers | 1 | Verified by source | No JSON helpers. CHANGELOG notes `Newtonsoft.Json` was removed. No `System.Text.Json` convenience methods. Users must serialize/deserialize manually. |
| 8.3.2 | Protobuf message wrapping | 5 | Verified by source | SDK types (`EventMessage`, `QueueMessage`, etc.) completely isolate users from gRPC types. Mapping in `KubeMQClient` private methods: `MapToEventReceived`, `MapToCommandReceived`, etc. No proto types leak to public API. |
| 8.3.3 | Typed payload support | 2 | Verified by source | No generic `Publish<T>` or typed deserialization. Body is always `ReadOnlyMemory<byte>`. Users must handle serialization. |
| 8.3.4 | Custom serialization hooks | 1 | Verified by source | No serialization hook or `ISerializer` interface. No pluggable serialization. |
| 8.3.5 | Content-type handling | 1 | Verified by source | No content-type metadata support. Users could use Tags for this manually, but no SDK support. |

**Subtotal:** 2.0

#### 8.4 Technical Debt

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.4.1 | TODO/FIXME/HACK comments | 5 | Verified by source | No TODO/FIXME/HACK found in source. Clean codebase. |
| 8.4.2 | Deprecated code | 5 | Verified by source | No deprecated methods in v3. `Log.DeprecatedApiUsage()` exists for future use. Old v2 code in `Archive/` directory (separate). |
| 8.4.3 | Dependency freshness | 5 | Verified by source | All dependencies use latest major versions: `Google.Protobuf 3.*`, `Grpc.Net.Client 2.*`, `Microsoft.Extensions.* 8.*`. No deprecated packages. |
| 8.4.4 | Language version | 5 | Verified by source | C# 12.0 on .NET 8.0 (LTS). Current and supported. |
| 8.4.5 | gRPC/protobuf library version | 5 | Verified by source | `Grpc.Net.Client 2.*` (current), `Google.Protobuf 3.*` (current), `Grpc.Tools 2.*` (current). All latest stable. |

**Subtotal:** 5.0

#### 8.5 Extensibility

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.5.1 | Interceptor/middleware support | 3 | Verified by source | `TelemetryInterceptor` and `AuthInterceptor` use gRPC interceptor chain. No public API to add custom interceptors. |
| 8.5.2 | Event hooks | 4 | Verified by source | `StateChanged` event for connection lifecycle. No per-message hooks (onSend, onReceive). |
| 8.5.3 | Transport abstraction | 5 | Verified by source | `ITransport` interface at line 9. `InternalsVisibleTo` enables test project to inject mock transport. Clean abstraction. |

**Subtotal:** 4.0

**Category 8 Overall: (4.9 + 4.3 + 2.0 + 5.0 + 4.0) / 5 = 4.0, adjusted to 4.1** (architecture and debt scores elevate the rating)

---

### Category 9: Testing (Score: 3.2)

#### 9.1 Unit Tests

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 9.1.1 | Unit test existence | 4 | Verified by source | 16 test files covering: Client lifecycle, publish, command/query, queue, channel management, error mapping, retry handler, auth interceptor, telemetry, config validation, message validation, exception hierarchy. |
| 9.1.2 | Coverage percentage | 3 | Inferred | ~16 test files for ~70 source files. Test classes exist for core paths: retry, error mapping, validation, lifecycle. No coverage for: `StreamManager`, `ReconnectBuffer`, `TlsConfigurator`, `StateMachine`, `ConnectionManager.ReconnectLoopAsync`. Estimated 40–50% coverage. |
| 9.1.3 | Test quality | 4 | Verified by source | Tests verify behavior, not implementation: retry exhaustion, error classification, idempotent disposal, metric recording. `FluentAssertions` for readable assertions. Edge cases: cancellation, disabled retry, non-idempotent timeout bypass. |
| 9.1.4 | Mocking | 5 | Verified by source | `ITransport` mocked via Moq. `TestClientFactory.Create()` provides clean factory pattern. Transport mock allows testing client logic without server. |
| 9.1.5 | Table-driven / parameterized tests | 4 | Verified by source | `[Theory]` + `[InlineData]` used in `GrpcErrorMapperTests` for status code mapping (15 status codes × retryable + 13 × category). `RetryHandlerTests` covers multiple scenarios. |
| 9.1.6 | Assertion quality | 5 | Verified by source | FluentAssertions: `.Should().Be()`, `.Should().BeOfType<>()`, `.Should().ThrowAsync<>()`, `.Should().NotThrowAsync()`. Type-safe, readable, meaningful assertions. |

**Subtotal:** 4.2

#### 9.2 Integration Tests

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 9.2.1 | Integration test existence | 1 | Verified by source | No integration test project found. No tests against a real KubeMQ server. |
| 9.2.2 | All patterns covered | 1 | Verified by source | No integration tests for any pattern. |
| 9.2.3 | Error scenario testing | 1 | Verified by source | No integration error scenario tests. |
| 9.2.4 | Setup/teardown | 1 | Verified by source | N/A — no integration tests. |
| 9.2.5 | Parallel safety | 1 | Verified by source | N/A — no integration tests. |

**Subtotal:** 1.0

#### 9.3 CI/CD Pipeline

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 9.3.1 | CI pipeline exists | 5 | Verified by source | `.github/workflows/ci.yml` with 3 jobs: lint, unit-tests, coverage. `.github/workflows/release.yml` for releases. |
| 9.3.2 | Tests run on PR | 5 | Verified by source | `on: pull_request: branches: [main, master]`. Tests run on PRs. Concurrency group prevents duplicates. |
| 9.3.3 | Lint on CI | 5 | Verified by source | Dedicated `lint` job: `dotnet format --verify-no-changes` + `dotnet build -warnaserror`. |
| 9.3.4 | Multi-version testing | 2 | Verified by source | Matrix defined with `dotnet: ['8.0.x']` only. Single version. Easy to expand but currently single-target. |
| 9.3.5 | Security scanning | 2 | Verified by source | `.codecov.yml` for coverage. No `dotnet list package --vulnerable` or Dependabot/Snyk in CI. |

**Subtotal:** 3.8

**Category 9 Overall: (4.2 + 1.0 + 3.8) / 3 = 3.0, adjusted to 3.2** (unit test quality elevates slightly)

---

### Category 10: Documentation (Score: 3.9)

#### 10.1 API Reference

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.1.1 | API docs exist | 4 | Verified by source | DocFX configured at `docfx/`. `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. README links to published docs. |
| 10.1.2 | All public methods documented | 4 | Verified by source | All `IKubeMQClient` methods have `<summary>`, `<param>`, `<returns>`, `<exception>` XML doc comments. Some internal types suppress CS1591. |
| 10.1.3 | Parameter documentation | 4 | Verified by source | All parameters documented with types and descriptions. `CancellationToken` parameters consistently documented. |
| 10.1.4 | Code doc comments | 4 | Verified by source | Rich XML docs on public types. Thread safety documented via `<threadsafety>` tags. `<remarks>` sections on key types. |
| 10.1.5 | Published API docs | 3 | Inferred | README links to `https://kubemq-io.github.io/kubemq-CSharp/`. DocFX configured with `filterConfig.yml`. Cannot verify the site is live. |

**Subtotal:** 3.8

#### 10.2 Guides & Tutorials

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.2.1 | Getting started guide | 5 | Verified by source | README has clear Quick Start: prerequisites, Docker command, 5-line publish, 5-line subscribe, expected output. |
| 10.2.2 | Per-pattern guide | 4 | Verified by source | README has sections for all 5 patterns with links to examples. Not full narrative guides, but clear descriptions with links. |
| 10.2.3 | Authentication guide | 4 | Verified by source | TROUBLESHOOTING.md covers auth. Examples in `Examples/Config/Config.TokenAuth`. `ICredentialProvider` documented in source. |
| 10.2.4 | Migration guide | 5 | Verified by source | `MIGRATION-v3.md` (256 lines) with before/after code, breaking changes, namespace mapping. |
| 10.2.5 | Performance tuning guide | 2 | Verified by source | No dedicated performance guide. README mentions timeout/retry config. Benchmarks exist but no guide on interpreting results. |
| 10.2.6 | Troubleshooting guide | 5 | Verified by source | `TROUBLESHOOTING.md` (332 lines) covers 11+ issues: connection refused, auth failed, timeout, throttling, TLS errors, no messages received, queue ack issues. Code examples for each. |

**Subtotal:** 4.2

#### 10.3 Examples & Cookbook

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.3.1 | Example code exists | 5 | Verified by source | 93 example .cs files in `Examples/` directory. Cookbook at `/tmp/kubemq-csharp-cookbook` with 32 files. |
| 10.3.2 | All patterns covered | 5 | Verified by source | Examples for: Events, EventsStore, Queues (send/receive, batch, DLQ, delayed, visibility timeout, ack/reject), Commands, Queries. |
| 10.3.3 | Examples compile/run | 3 | Inferred | Cannot compile (dotnet not available). Examples reference correct types and namespaces based on source inspection. |
| 10.3.4 | Real-world scenarios | 4 | Verified by source | DLQ handling, batch operations, delayed messages, visibility timeout, cached queries, TLS/mTLS setup, token auth. Beyond hello-world. |
| 10.3.5 | Error handling shown | 3 | Verified by source | README shows typed exception handling. Some examples may lack error handling. |
| 10.3.6 | Advanced features | 4 | Verified by source | Examples for: TLS, mTLS, token auth, custom timeouts, OpenTelemetry, delayed messages, DLQ, group subscriptions, cached queries. |

**Subtotal:** 4.0

#### 10.4 README Quality

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.4.1 | Installation instructions | 5 | Verified by source | `dotnet add package KubeMQ.SDK.CSharp` + PackageReference XML. Clear and complete. |
| 10.4.2 | Quick start code | 5 | Verified by source | Copy-paste-ready publisher and subscriber code in README. Expected output shown. |
| 10.4.3 | Prerequisites | 5 | Verified by source | .NET 8.0, KubeMQ server ≥3.0, Docker quick start command. |
| 10.4.4 | License | 5 | Verified by source | Apache 2.0. LICENSE file present. Referenced in README. PackageLicenseExpression in csproj. |
| 10.4.5 | Changelog | 4 | Verified by source | `CHANGELOG.md` (91 lines) follows Keep a Changelog format. v3.0.0 entry is comprehensive. v2.0.0 says "No changelog maintained." |

**Subtotal:** 4.8

**Category 10 Overall: (3.8 + 4.2 + 4.0 + 4.8) / 4 = 4.2, adjusted to 3.9** (API docs publication uncertainty and performance guide gap)

---

### Category 11: Packaging & Distribution (Score: 3.8)

#### 11.1 Package Manager

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.1.1 | Published to canonical registry | 4 | Inferred | README shows NuGet badge linking to `nuget.org/packages/KubeMQ.SDK.CSharp`. csproj has `PackageId = KubeMQ.SDK.CSharp`. Cannot verify live package. |
| 11.1.2 | Package metadata | 5 | Verified by source | csproj has: Description, Authors, Company, PackageTags, PackageProjectUrl, RepositoryUrl, PackageLicenseExpression, PackageReadmeFile, PackageReleaseNotes. Comprehensive. |
| 11.1.3 | Reasonable install | 5 | Verified by source | Single `dotnet add package KubeMQ.SDK.CSharp`. No native dependencies (Grpc.Net.Client is managed-only). |
| 11.1.4 | Minimal dependency footprint | 4 | Verified by source | 9 direct runtime dependencies. All are Microsoft.Extensions.* and Google/Grpc packages — standard for a gRPC-based .NET SDK. Grpc.Tools and analyzers are build-only (`PrivateAssets=All`). |

**Subtotal:** 4.5

#### 11.2 Versioning & Releases

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.2.1 | Semantic versioning | 5 | Verified by source | `<Version>3.0.0</Version>`. CHANGELOG references `v3.0.0`, `v2.0.0`. Breaking changes in major version only. |
| 11.2.2 | Release tags | 3 | Inferred | `release.yml` triggered by `v*` tags. Cannot verify tags exist on remote. |
| 11.2.3 | Release notes | 3 | Verified by source | CHANGELOG exists with v3.0.0 entry. Date says "YYYY-MM-DD" — placeholder. `PackageReleaseNotes` links to CHANGELOG. |
| 11.2.4 | Current version | 3 | Verified by source | v3.0.0 but date is placeholder. If not published yet, this is pre-release. |
| 11.2.5 | Version consistency | 4 | Verified by source | Single `<Version>3.0.0</Version>` drives all version attributes. SourceLink enabled. |

**Subtotal:** 3.6

#### 11.3 Build & Development Setup

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.3.1 | Build instructions | 4 | Verified by source | CONTRIBUTING.md (65 lines) has build, test, and PR requirements. |
| 11.3.2 | Build succeeds | N/A | Not assessable | Cannot run `dotnet build`. CI config suggests it builds. |
| 11.3.3 | Development dependencies | 5 | Verified by source | `PrivateAssets=All` on Grpc.Tools, analyzers, SourceLink. Test project has separate dependencies (Moq, FluentAssertions, coverlet). |
| 11.3.4 | Contributing guide | 4 | Verified by source | CONTRIBUTING.md exists with build steps, code style requirements, PR process. |

**Subtotal (excl. N/A):** 4.3

#### 11.4 SDK Binary Size & Footprint

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.4.1 | Dependency weight | 4 | Verified by source | All dependencies are standard .NET BCL extensions and gRPC packages. No bloat. |
| 11.4.2 | No native compilation required | 5 | Verified by source | `Grpc.Net.Client` is fully managed. No native binaries required. Works on Alpine/musl. |

**Subtotal:** 4.5

**Category 11 Overall: (4.5 + 3.6 + 4.3 + 4.5) / 4 = 4.2, adjusted to 3.8** (version placeholder and release uncertainty lower the score)

---

### Category 12: Compatibility, Lifecycle & Supply Chain (Score: 3.3)

#### 12.1 Compatibility

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 12.1.1 | Server version matrix | 4 | Verified by source | COMPATIBILITY.md has matrix: v3.x supports Server ≥3.0, ≥4.0. Server <3.0 marked untested. Runtime compatibility check logs warning. |
| 12.1.2 | Runtime support matrix | 4 | Verified by source | COMPATIBILITY.md documents: Linux x64/arm64, Windows x64, macOS x64/arm64, Alpine. Container images listed. |
| 12.1.3 | Deprecation policy | 4 | Verified by source | README documents: `[Obsolete]` annotations, CHANGELOG documentation, 2 minor versions or 6 months notice, 12-month security patch window. `Log.DeprecatedApiUsage()` ready. |
| 12.1.4 | Backward compatibility discipline | 4 | Verified by source | CHANGELOG clearly marks all breaking changes. Migration guide provided. Semver followed. |

**Subtotal:** 4.0

#### 12.2 Supply Chain & Release Integrity

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 12.2.1 | Signed releases | 2 | Inferred | No GPG signing or Sigstore. NuGet package may use NuGet signing but no evidence. SourceLink enabled. |
| 12.2.2 | Reproducible builds | 4 | Verified by source | `<Deterministic>true</Deterministic>`, `<ContinuousIntegrationBuild>` conditional. SourceLink for source mapping. No lock file (not standard in .NET). |
| 12.2.3 | Dependency update process | 2 | Verified by source | No Dependabot/Renovate configuration found. Version ranges (`3.*`, `2.*`) allow automatic minor updates. |
| 12.2.4 | Security response process | 4 | Verified by source | SECURITY.md (31 lines) exists with security policy. |
| 12.2.5 | SBOM | 1 | Verified by source | No SBOM generation. No SPDX or CycloneDX integration in build/release. |
| 12.2.6 | Maintainer health | 3 | Inferred | v3.0.0 is a major rewrite. Cannot verify GitHub activity (commits, issues, PRs) without network access. |

**Subtotal:** 2.7

**Category 12 Overall: (4.0 + 2.7) / 2 = 3.3**

---

### Category 13: Performance (Score: 3.0)

#### 13.1 Benchmark Infrastructure

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 13.1.1 | Benchmark tests exist | 4 | Verified by source | 10 benchmark files in `benchmarks/KubeMQ.Sdk.Benchmarks/`: `PublishLatencyBenchmark`, `PublishThroughputBenchmark`, `QueueRoundtripBenchmark`, `ConnectionSetupBenchmark`, `MessageValidationBenchmarks`, `RetryPolicyBenchmarks`, `SerializationBenchmarks`. Uses BenchmarkDotNet. |
| 13.1.2 | Benchmark coverage | 4 | Verified by source | Covers: publish latency, publish throughput, queue roundtrip, connection setup, message validation, retry policy, serialization. Good breadth. |
| 13.1.3 | Benchmark documentation | 2 | Verified by source | `BenchmarkConfig.cs` and `BenchmarkEnvironment.cs` exist. No README or guide on how to run benchmarks or interpret results. |
| 13.1.4 | Published results | 1 | Verified by source | No published baseline performance numbers in README, docs, or any report file. |

**Subtotal:** 2.75

#### 13.2 Optimization Patterns

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 13.2.1 | Object/buffer pooling | 2 | Verified by source | No `ArrayPool<byte>` or object pooling. `ByteString.CopyFrom()` allocates on every publish. `new Dictionary<string,string>()` created for each received message tags. |
| 13.2.2 | Batching support | 2 | Verified by source | `SendQueueMessagesAsync` exists but sends messages individually. No true batch optimization. |
| 13.2.3 | Lazy initialization | 3 | Verified by source | gRPC channel created only on `ConnectAsync()`. Auth interceptor fetches token lazily. Retry handler only created when retry is enabled. |
| 13.2.4 | Memory efficiency | 3 | Verified by source | `ReadOnlyMemory<byte>` for zero-copy payloads in public API. `ByteString.CopyFrom(message.Body.Span)` creates a copy for gRPC. `Stopwatch.StartNew()` on every operation. `TagList` structs for metrics (stack-allocated). |
| 13.2.5 | Resource leak risk | 4 | Verified by source | `using var call` on all streaming operations. Dispose pattern with idempotent Interlocked. `GrpcTransport.ConnectAsync()` disposes handler on failure. Minor: `Task.Run` for state change events could leak if handler is slow. |
| 13.2.6 | Connection overhead | 5 | Verified by source | Single `GrpcChannel` shared across all operations. `EnableMultipleHttp2Connections = true` for HTTP/2 multiplexing. No per-operation channel creation. |

**Subtotal:** 3.2

**Category 13 Overall: (2.75 + 3.2) / 2 = 3.0**

---

## Score Calculation

### Weighted Score

| # | Category | Weight | Score | Weighted |
|---|----------|--------|-------|----------|
| 1 | API Completeness | 14% | 4.2 | 0.588 |
| 2 | API Design & DX | 9% | 4.3 | 0.387 |
| 3 | Connection & Transport | 11% | 4.0 | 0.440 |
| 4 | Error Handling | 11% | 4.2 | 0.462 |
| 5 | Auth & Security | 9% | 3.6 | 0.324 |
| 6 | Concurrency | 7% | 4.1 | 0.287 |
| 7 | Observability | 5% | 4.0 | 0.200 |
| 8 | Code Quality | 6% | 4.1 | 0.246 |
| 9 | Testing | 9% | 3.2 | 0.288 |
| 10 | Documentation | 7% | 3.9 | 0.273 |
| 11 | Packaging | 4% | 3.8 | 0.152 |
| 12 | Compatibility | 4% | 3.3 | 0.132 |
| 13 | Performance | 4% | 3.0 | 0.120 |
| | **Total** | 100% | | **3.90** |

### Gating Rules

- **Gate A:** All Critical categories ≥ 3.0: Cat 1 (4.2) ✅, Cat 3 (4.0) ✅, Cat 4 (4.2) ✅, Cat 5 (3.6) ✅ → Gate NOT applied.
- **Gate B:** Category 1 features scoring 0: 2 out of 48 = 4.2% → Under 25% → Gate NOT applied.

### Unweighted Score
(4.2 + 4.3 + 4.0 + 4.2 + 3.6 + 4.1 + 4.0 + 4.1 + 3.2 + 3.9 + 3.8 + 3.3 + 3.0) / 13 = **3.8**

---

## Developer Journey Assessment

| Step | Score | Assessment | Friction Points |
|------|-------|-----------|-----------------|
| **1. Install** | 5/5 | `dotnet add package KubeMQ.SDK.CSharp`. Single package, no native dependencies, NuGet standard. | None. |
| **2. Connect** | 4/5 | Create `KubeMQClient` + `ConnectAsync()`. DI variant auto-connects via `AddKubeMQ()`. Two-step (create + connect) is explicit but standard for .NET. | Must remember to call `ConnectAsync()`. DI path hides this but manual path requires it. |
| **3. First Publish** | 5/5 | `await client.PublishEventAsync(new EventMessage { Channel = "ch", Body = bytes })`. Convenience overload: `PublishEventAsync("ch", bytes)`. | None — minimal and clear. |
| **4. First Subscribe** | 5/5 | `await foreach (var msg in client.SubscribeToEventsAsync(sub))`. Idiomatic `IAsyncEnumerable` pattern. | Excellent — best-in-class C# pattern. |
| **5. Error Handling** | 4/5 | Typed exceptions with `IsRetryable`, `ErrorCode`, `Category`, `Suggestion` text. Auto-retry handles most transients transparently. | Discovery requires reading docs. Exception hierarchy has 10 types — could overwhelm new users, but catch-all `KubeMQException` exists. |
| **6. Production Config** | 4/5 | TLS, auth, retry, reconnect, keepalive all configurable via `KubeMQClientOptions`. DI supports `appsettings.json` binding. | No OIDC out-of-box. Must implement `ICredentialProvider` for dynamic tokens. Secure defaults gap (TLS off by default). |
| **7. Troubleshooting** | 4/5 | TROUBLESHOOTING.md covers 11+ scenarios with code. Error messages include "Suggestion:" text. Structured logging with event IDs for correlation. | No built-in diagnostics dump or health-check endpoint. |

**Overall Developer Journey Score: 4.4/5**

---

## Competitor Comparison

| Area | KubeMQ C# SDK (v3) | NATS.Client | Confluent.Kafka | Azure.Messaging.ServiceBus | RabbitMQ.Client |
|------|-------------------|-------------|-----------------|---------------------------|-----------------|
| **API Design** | Single client, strongly typed, modern C# 12 | Multi-client (Core/JetStream), callback-based | Producer/Consumer separation, config-heavy | ServiceBusClient → Sender/Receiver, Azure SDK patterns | ConnectionFactory → Connection → Channel, AMQP model |
| **Async Pattern** | `IAsyncEnumerable`, `async/await` throughout | Callbacks + `IAsyncEnumerable` in newer versions | `Task`-based, no `IAsyncEnumerable` | `async/await`, `ServiceBusProcessor` | Sync-first, `AsyncEventingBasicConsumer` |
| **Error Handling** | Typed hierarchy (10 types), retryable classification | `NatsException` base, connection-level retry | `ProduceException`, `ConsumeException` | `ServiceBusException` with `Reason` enum | `BrokerUnreachableException`, basic types |
| **Retry** | Built-in exponential backoff with jitter | Connection-level only | Producer retry built-in | Azure SDK retry pipeline | None built-in |
| **Observability** | ActivitySource + Meter (OTel-compatible, zero-dep) | EventSource-based | librdkafka stats callback | Azure.Core diagnostics | No built-in |
| **DI Integration** | `AddKubeMQ()` extensions, hosted service | Manual registration | Manual registration | `AddServiceBusClient()` in Azure.Extensions | Manual registration |
| **Documentation** | Good README, troubleshooting, migration guide | Extensive docs site | Extensive with Confluent docs | Comprehensive Azure docs | Extensive RabbitMQ docs |
| **Community** | Small (niche product) | Large (CNCF project) | Very large (de facto standard) | Very large (Azure ecosystem) | Very large (established) |

**Key differentiators of KubeMQ C# SDK:**
- More modern C# patterns than most competitors (IAsyncEnumerable, record types)
- Built-in retry with richer configuration than NATS/RabbitMQ
- OTel-compatible observability without OTel NuGet dependency
- Weaker on community size and documentation depth vs established competitors

---

## Remediation Roadmap

### Phase 0: Assessment Validation (1–2 days)
Validate the top 5 most impactful findings with targeted manual smoke tests:
1. Verify `ListChannelsAsync` returns empty — confirm this is a bug vs design
2. Test `SendQueueMessagesAsync` batch behavior — confirm sequential sends
3. Run `dotnet build` and `dotnet test` to verify build health
4. Test AuthInterceptor token refresh under load (sync-over-async risk)
5. Confirm subscription recovery actually works end-to-end with a server restart

### Phase 1: Quick Wins (Effort: S-M)

| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|
| 1 | Fix `ListChannelsAsync` to return actual results | Cat 1 | 1 | 5 | S | High | — | language-specific | Integration test verifies channels returned match server state |
| 2 | Use `SendQueueMessagesBatch` gRPC RPC for batch sends | Cat 1 | 3 | 5 | M | High | — | language-specific | Batch of 100 messages sent in single RPC; latency < 10x single send |
| 3 | Fix `CreateChannelAsync`/`DeleteChannelAsync` to use `channelType` parameter | Cat 1 | 3 | 5 | S | Medium | — | language-specific | Integration test creates/deletes each channel type |
| 4 | Add integration test project with basic smoke tests | Cat 9 | 1 | 3 | M | High | — | cross-SDK | Tests for all 4 patterns pass against real KubeMQ server in CI |
| 5 | Add Dependabot/Renovate configuration | Cat 12 | 2 | 4 | S | Medium | — | cross-SDK | `.github/dependabot.yml` exists; first PR auto-created |
| 6 | Extract `ParseAddress()` to shared utility (eliminate duplication) | Cat 8 | 3 | 5 | S | Low | — | language-specific | Single `ParseAddress` method used by both `KubeMQClient` and `GrpcTransport` |

### Phase 2: Medium-Term Improvements (Effort: M-L)

| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|
| 7 | Add gRPC compression support | Cat 3 | 1 | 4 | M | Medium | — | cross-SDK | `GrpcChannelOptions` includes gzip compression; benchmark shows reduced bandwidth |
| 8 | Add JSON serialization helpers | Cat 8 | 1 | 4 | M | Medium | — | cross-SDK | `message.ToJson<T>()` and `Message.FromJson<T>()` convenience methods exist |
| 9 | Add concurrent stress tests | Cat 6 | 2 | 4 | M | Medium | #4 | language-specific | Tests with 100 concurrent publishers/subscribers pass without race conditions |
| 10 | Add multi-version .NET testing in CI | Cat 9 | 2 | 4 | S | Medium | — | language-specific | CI matrix includes net8.0 and net9.0 |
| 11 | Add security scanning to CI | Cat 9/12 | 2 | 4 | S | Medium | — | cross-SDK | `dotnet list package --vulnerable` runs in CI; fails on critical CVEs |
| 12 | Add ASP.NET Health Check integration | Cat 3 | N/A | 4 | M | Medium | — | language-specific | `services.AddHealthChecks().AddKubeMQ()` registers health check; `/health` returns healthy when connected |
| 13 | Fix sync-over-async in `AuthInterceptor` | Cat 6 | 3 | 5 | L | Medium | — | language-specific | No `.GetAwaiter().GetResult()` calls; async interceptor pattern or cached-only sync path |

### Phase 3: Major Rework (Effort: L-XL)

| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|
| 14 | Default TLS-on with explicit insecure opt-in | Cat 5 | 2 | 5 | L | High | — | cross-SDK | `new KubeMQClientOptions()` defaults to TLS; insecure requires `Tls = new TlsOptions { Enabled = false }` |
| 15 | Add typed payload support with generics | Cat 8 | 2 | 4 | L | Medium | #8 | cross-SDK | `PublishEventAsync<T>(channel, payload)` with configurable serializer |
| 16 | Wire subscription tracking into public subscribe methods | Cat 3 | 4 | 5 | L | High | — | language-specific | Subscription recovery verified by integration test: server restart → all subs resume |
| 17 | Add ArrayPool/buffer pooling for hot paths | Cat 13 | 2 | 4 | L | Medium | — | language-specific | Benchmark shows 30% reduction in allocations per publish |
| 18 | Add SBOM generation to release pipeline | Cat 12 | 1 | 4 | M | Low | — | cross-SDK | SBOM file (SPDX or CycloneDX) published with each NuGet package |

### Effort Key
- **S (Small):** < 1 day
- **M (Medium):** 1–3 days
- **L (Large):** 1–2 weeks
- **XL (Extra Large):** 2+ weeks
