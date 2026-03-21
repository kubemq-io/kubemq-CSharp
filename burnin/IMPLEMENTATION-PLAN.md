# C#/.NET Burn-In Test App — Implementation Plan

## Context

Fourth and final SDK burn-in implementation after Go, Python, and JavaScript. The C# SDK at `kubemq-csharp/src/KubeMQ.Sdk/` uses a **single unified `KubeMQClient`** with async-first design. Subscriptions return **`IAsyncEnumerable<T>`** (modern C# async streams) controlled by `CancellationToken`. The SDK has full streaming support: `CreateEventStreamAsync()`, `CreateEventStoreStreamAsync()`, `SendQueueMessagesUpstreamAsync()`, `ReceiveQueueDownstreamAsync()`. All 51 consolidated lessons from Go + Python + JS are pre-applied.

---

## Key C# SDK Characteristics

1. **Single unified client** — one `KubeMQClient` for all patterns (same as JS, unlike Python's 3 clients)
2. **Async-first** — all methods return `Task<T>`, constructor is sync but `ConnectAsync()` is async
3. **`IAsyncEnumerable<T>` subscriptions** — `await foreach` pattern with `CancellationToken` cancellation (not callbacks like JS, not blocking like Go)
4. **Native stream APIs** — `EventStream`, `EventStoreStream` (with `SendAsync()` confirmation), `SendQueueMessagesUpstreamAsync()`, `ReceiveQueueDownstreamAsync()`
5. **`ReadOnlyMemory<byte>` for bodies** — not `byte[]` or `Uint8Array`
6. **`EventStoreResult` with `.Sent` boolean** — must check before counting events_store sends
7. **`QueueSendResult` with `.IsError` boolean** — must check for queue sends
8. **`CommandResponse.Executed` boolean + `.Error` string** — for RPC response checking
9. **`ReconnectOptions` with jitter** — `JitterMode.Full/Equal/None`, fully configurable from burn-in
10. **`IAsyncDisposable`** — prefer `await using` for client lifecycle
11. **`KubeMQClientOptions`** — constructor takes options object, then `ConnectAsync()` is called separately
12. **`SendCommandResponseAsync(CommandResponse response, CancellationToken ct = default)` — takes a response object**
13. **.NET 8.0 target** — `System.Threading.Channels`, `System.IO.Hashing.Crc32`, `System.Diagnostics.Stopwatch`

---

## File Structure

```
kubemq-csharp/burnin/
  KubeMQ.Burnin.csproj         # .NET 8 console app, SDK local ProjectReference
  Dockerfile                    # Multi-arch, non-root user
  burnin-config.yaml            # Example config

  Config.cs                     # Config record, YAML load, env override, validation
  Program.cs                    # Entry point: CLI args, signal handling, exit codes
  Engine.cs                     # Orchestrator: client lifecycle, warmup, periodic tasks, 2-phase shutdown
  Payload.cs                    # JSON encode/decode, CRC32 (System.IO.Hashing), random padding
  Tracker.cs                    # Bitset sequence tracker (sliding window, delta gaps)
  RateLimiter.cs                # Token-bucket with 1-second burst capacity
  TimestampStore.cs             # (producerId, seq) → Stopwatch.GetTimestamp() mapping
  PeakRate.cs                   # PeakRateTracker (10s window) + LatencyAccumulator (HdrHistogram)
  Metrics.cs                    # All 26 Prometheus metrics + helpers (prometheus-net)
  HttpServer.cs                 # Kestrel minimal API: /health /ready /status /summary /metrics
  Report.cs                     # 10 checks + advisory, PASSED/PASSED_WITH_WARNINGS/FAILED
  Disconnect.cs                 # Forced disconnect manager

  Workers/
    BaseWorker.cs               # CancellationTokenSource for 2-phase shutdown, dual tracking
    EventsWorker.cs             # CreateEventStreamAsync() + SubscribeToEventsAsync()
    EventsStoreWorker.cs        # CreateEventStoreStreamAsync() + SubscribeToEventsStoreAsync()
    QueueStreamWorker.cs        # SendQueueMessagesUpstreamAsync() + ReceiveQueueDownstreamAsync()
    QueueSimpleWorker.cs        # SendQueueMessageAsync() + ReceiveQueueMessagesAsync()
    CommandsWorker.cs           # SendCommandAsync() + SubscribeToCommandsAsync() + SendCommandResponseAsync()
    QueriesWorker.cs            # SendQueryAsync() + SubscribeToQueriesAsync() + SendQueryResponseAsync()

  IMPLEMENTATION-RETROSPECTIVE.md
```

~18 C# source files.

---

## Threading Architecture (.NET Task-Based)

```
Main Thread → Engine.RunAsync()
  ├── Per-pattern: Consumer Task (await foreach on IAsyncEnumerable)
  ├── Per-pattern: Producer Task (async loop with await rateLimiter.WaitAsync)
  ├── Timer: periodicReporter (every BURNIN_REPORT_INTERVAL)
  ├── Timer: peakRateAdvancer (every 1s)
  ├── Timer: uptimeTracker (every 1s)
  ├── Timer: memoryTracker (every 10s)
  ├── Timer: timestampPurger (every 60s)
  ├── Kestrel HTTP Server (background)
  └── [Optional] Timer: disconnectManager
```

2-phase shutdown via two `CancellationTokenSource` instances:
```
producerCts.Cancel() ──→ stops all producer Tasks
       ↓ drain timeout
consumerCts.Cancel() ──→ stops all IAsyncEnumerable subscriptions
```

---

## SDK API Mapping (C# ↔ Go ↔ Spec)

| Spec Pattern | Go API | C# API |
|-------------|--------|--------|
| Events send (stream) | `SendEventStream()` | `CreateEventStreamAsync()` → `stream.SendAsync()` |
| Events subscribe | `SubscribeToEvents()` | `SubscribeToEventsAsync()` → `IAsyncEnumerable<EventReceived>` |
| Events Store send (stream+confirm) | `SendEventStoreStream()` | `CreateEventStoreStreamAsync()` → `stream.SendAsync()` returns `EventStoreResult` |
| Events Store subscribe | `SubscribeToEventsStore()` | `SubscribeToEventsStoreAsync()` → `IAsyncEnumerable<EventStoreReceived>` |
| Queue Stream send (upstream) | `QueueUpstream()` | `SendQueueMessagesUpstreamAsync()` returns `QueueUpstreamResult` |
| Queue Stream receive (downstream) | `QueueDownstream()` | `ReceiveQueueDownstreamAsync()` returns `QueueDownstreamResult` |
| Queue Simple send | `SendQueueMessage()` | `SendQueueMessageAsync()` returns `QueueSendResult` |
| Queue Simple receive | `ReceiveQueueMessages()` | `ReceiveQueueMessagesAsync()` returns `QueueReceiveResult` |
| Commands send | `SendCommand()` | `SendCommandAsync()` returns `CommandResponse` |
| Commands subscribe | `SubscribeToCommands()` | `SubscribeToCommandsAsync()` → `IAsyncEnumerable<CommandReceived>` |
| Commands respond | `SendResponse()` | `SendCommandResponseAsync(CommandResponse response)` |
| Queries send | `SendQuery()` | `SendQueryAsync()` returns `QueryResponse` |
| Queries subscribe | `SubscribeToQueries()` | `SubscribeToQueriesAsync()` → `IAsyncEnumerable<QueryReceived>` |
| Queries respond | `SendResponse()` | `SendQueryResponseAsync(QueryResponse response)` |
| Ping | `Ping()` | `PingAsync()` returns `ServerInfo` |
| Channel create | `CreateChannel()` | `CreateEventsChannelAsync()` etc. |
| Channel delete | `DeleteChannel()` | `DeleteEventsChannelAsync()` etc. |
| Channel list | `ListChannels()` | `ListEventsChannelsAsync()` etc. |

---

## Key C#-Specific Design Decisions

### CRC32
Use `System.IO.Hashing.Crc32` (built into .NET 8) — no external dependency needed.

### HdrHistogram
Use `HdrHistogram` NuGet package (well-maintained .NET port).

### Prometheus Metrics
Use `prometheus-net` NuGet package (standard for .NET). Expose via Kestrel's `/metrics` endpoint.

### HTTP Server
Use ASP.NET Core minimal API (built into .NET 8) with `WebApplication.CreateSlimBuilder()` for lightweight HTTP endpoints.

### YAML Parsing
Use `YamlDotNet` NuGet package.

### Rate Limiting
Token-bucket with 1-second burst capacity using `SemaphoreSlim` + `Task.Delay` for async waiting.

### Memory Tracking
`Process.GetCurrentProcess().WorkingSet64` for RSS in bytes.

### Monotonic Time
`Stopwatch.GetTimestamp()` for high-resolution monotonic timestamps. `Stopwatch.GetElapsedTime(start)` for duration.

### Shutdown
- `IHostApplicationLifetime` or direct `CancellationTokenSource` triggered by `Console.CancelKeyPress` + `AppDomain.ProcessExit`
- Spec Section 12.2 note: "The default `ProcessExit` timeout (~2s) is insufficient" — must extend or handle carefully

---

## Pre-Applied Lessons (51 rules consolidated)

| # | Source | Rule | C# Implementation |
|---|--------|------|-------------------|
| 1 | Go#1 | Prometheus counter deltas only | `Tracker.DetectGaps()` returns delta via `lastReportedLost` |
| 2 | Go#2 | 2-phase shutdown in base class | `BaseWorker` creates `producerCts` + `consumerCts` in constructor |
| 3 | Go#3/20 | Memory baseline at 5-min | Seed on first sample, update at 300s, shutdown fallback |
| 4 | Go#5 | Every /summary field populated | Dual tracking (Prometheus + in-process fields) |
| 5 | Go#6 | Warmup ALL 6 patterns | Each pattern warmup uses its production API |
| 6 | Go#7 | Sent counter after success | Events: no throw. ES: `result.Sent==true`. Queue: `!result.IsError`. RPC: `resp.Executed && resp.Error==null` |
| 7 | Go#8 | Cleanup broad prefix | `csharp_burnin_` (no run_id) |
| 8 | Go#9 | Error rate formula | `errors / (sent + received) * 100` |
| 9 | Go#10 | RPC status format | `resp=/tout=` for commands/queries periodic log |
| 10 | Go#12 | 3-state verdict | PASSED, PASSED_WITH_WARNINGS, FAILED |
| 11 | Go#14 | Peak rate wired | `Record()` on send, `Advance()` every 1s, `Peak()` in summary |
| 12 | Go#17 | Warmup CRC tag | All warmup messages include `content_hash` tag |
| 13 | Go#18 | Responders check warmup | `tags["warmup"] == "true"` checked FIRST in all responder callbacks |
| 14 | Go#19 | Queue batch drain | Process full batch, then ackAll, then next request |
| 15 | Py#2 | Non-blocking subscriptions | `IAsyncEnumerable` is non-blocking via `await foreach` — just cancel the CancellationToken |
| 16 | JS-F2 | Stream send sync vs async | `EventStream.SendAsync()` is async. Check C# stream handle behavior |
| 17 | JS-F3 | Queue upstream takes collection | `SendQueueMessagesUpstreamAsync(IEnumerable<QueueMessage>)` |
| 18 | JS-F5 | Config discovery priority | env var > CLI > ./burnin-config.yaml > /etc/burnin/config.yaml |
| 19 | JS-F6 | Histogram buckets exact | Latency: `[0.0005..5]`, RPC: `[0.001..5]` — must match spec |
| 20 | JS-F7 | Unhandled exceptions | C# equivalent: `AppDomain.CurrentDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` |
| 21 | JS-F8 | EventStore initial=StartNewOnly | SDK handles reconnect with StartAtSequence internally |
| 22 | JS-F12 | Reconnection in ALL onError | Every subscription error handler calls `IncReconnection()` |
| 23 | JS-F13 | error_type enum strings | `send_failure`, `receive_failure`, `subscription_error`, `decode_failure`, `response_send_failure` |
| 24 | JS-F14 | Shutdown: cleanup before report | channels → report file → console print |
| 25 | JS-F17 | Throughput check skipped benchmark | `if (mode != "benchmark")` before throughput evaluation |
| 26 | JS-F18 | Downtime: max across patterns | `max(downtime_pct)` not sum |
| 27 | JS-F19 | benchmark rate=0 auto | Engine sets all rates to 0 when `mode == "benchmark"` |
| 28 | JS-N | Backpressure logs warning | `Console.Warn()` once when lag exceeds max_depth |

---

## Implementation Order

### Phase 1: Foundation
1. `KubeMQ.Burnin.csproj` — .NET 8, SDK ProjectReference, NuGet deps
2. `Config.cs` — full config with all env vars, YAML loading, validation
3. `Payload.cs` — encode/decode, System.IO.Hashing.Crc32, random padding
4. `Tracker.cs` — bitset tracker with sliding window and delta gaps
5. `RateLimiter.cs` — async token-bucket
6. `TimestampStore.cs` — send time store (ConcurrentDictionary)
7. `PeakRate.cs` — PeakRateTracker + LatencyAccumulator
8. `Metrics.cs` — all 26 metrics + helpers

### Phase 2: Infrastructure
9. `HttpServer.cs` — 5 endpoints via Kestrel
10. `Workers/BaseWorker.cs` — full interface with 2-phase CTS
11. `Program.cs` — CLI entry point

### Phase 3: Workers (all 6)
12. `Workers/EventsWorker.cs`
13. `Workers/EventsStoreWorker.cs`
14. `Workers/QueueStreamWorker.cs`
15. `Workers/QueueSimpleWorker.cs`
16. `Workers/CommandsWorker.cs`
17. `Workers/QueriesWorker.cs`

### Phase 4: Engine + Report + Disconnect
18. `Engine.cs` — orchestrator
19. `Report.cs` — verdict generation
20. `Disconnect.cs` — forced disconnect manager

### Phase 5: Polish
21. `burnin-config.yaml`
22. `Dockerfile`
23. `IMPLEMENTATION-RETROSPECTIVE.md`

---

## Startup Sequence (Section 12.1)

1. Parse CLI args, load YAML config, apply env overrides, validate — exit 2 on error
2. Create `KubeMQClient(options)`, call `ConnectAsync()`, then `PingAsync()`
3. Clean stale channels (broad `csharp_burnin_` prefix)
4. Create all channels (idempotent)
5. Start Kestrel HTTP server → `/health` returns 200
6. Create workers, start consumers only (launch `await foreach` Tasks)
7. Run warmup: 10 messages per pattern using production APIs, verify receipt
8. `/ready` returns 200
9. Start producers (after consumers confirmed + warmup verified)
10. Print startup banner
11. If warmup_duration > 0: wait, then reset counters
12. Start periodic tasks (System.Threading.Timer)
13. Start disconnect manager (if configured)
14. `await Task.Delay(duration, cts.Token)` — wait for duration or SIGTERM

---

## Shutdown Sequence (Section 12.2)

1. `producerCts.Cancel()` — stops all producer Tasks
2. `await Task.Delay(drainTimeoutSeconds * 1000)`
3. `consumerCts.Cancel()` — cancels all IAsyncEnumerable subscriptions
4. Enforce hard deadline: `drainTimeoutSeconds + 5` seconds total
5. Delete channels (best-effort, with timeout guard)
6. Write JSON report to file (if configured)
7. Print console report
8. `await client.DisposeAsync()` — close gRPC channel
9. Stop Kestrel
10. `Environment.Exit(exitCode)` — 0=PASSED, 1=FAILED, 2=config error

---

## Verification Steps

1. `dotnet run -- --validate-config --config burnin-config.yaml` — exit 0
2. `dotnet run -- --cleanup-only --config burnin-config.yaml` — broad prefix in logs
3. Grep every metric helper → at least 1 call site
4. Grep every /summary field → populated in `BuildSummary()`
5. 15-minute run against live broker on localhost:50000
6. `curl localhost:8888/summary | jq .` — all fields non-zero
7. `curl localhost:8888/metrics | grep burnin_` — all 26 metrics present
8. SIGTERM → clean 2-phase shutdown in logs
9. Exit code 0 for PASSED
