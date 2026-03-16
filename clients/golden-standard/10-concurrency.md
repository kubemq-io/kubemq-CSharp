# Category 10: Concurrency & Thread Safety

**Tier:** 2 (Should-have)
**Current Average Score:** 3.09 / 5.0
**Target Score:** 4.0+
**Weight:** 7%

## Purpose

Users must know which types are safe to share across threads/goroutines and which are not. Cancellation and timeout must be supported on all blocking operations.

---

## Requirements

### REQ-CONC-1: Thread Safety Documentation

Every public type must document its thread-safety guarantee.

| Type | Guarantee |
|------|-----------|
| `Client` / `Connection` | Thread-safe. Share across goroutines/threads. One instance per application. |
| `Subscription` | Thread-safe. Can be cancelled from any thread. |
| `Message` (outbound) | Not thread-safe. Create per-send, do not share. |
| `ReceivedMessage` | Safe to read from multiple threads. Do not modify. |

**Acceptance criteria:**
- [ ] Client type is explicitly documented as thread-safe / goroutine-safe
- [ ] Doc comments on each public type state its concurrency guarantee
- [ ] Non-thread-safe types document the restriction

### REQ-CONC-2: Cancellation & Timeout Support

All blocking/async operations must support cancellation.

| Language | Mechanism | Requirement |
|----------|-----------|-------------|
| Go | `context.Context` | Every blocking method accepts `ctx` as first parameter |
| Java | `CompletableFuture<T>` | Async methods return `CompletableFuture`; sync variants accept `Duration` |
| C# | `CancellationToken` | Every `async Task` method accepts `CancellationToken` |
| Python | `timeout` parameter + `asyncio.wait_for` | Every async method accepts `timeout` kwarg |
| JS/TS | `AbortSignal` or `timeout` option | Every async method accepts signal/timeout |

**Acceptance criteria:**
- [ ] All blocking operations accept language-appropriate cancellation mechanism
- [ ] Cancellation is propagated to the underlying gRPC call
- [ ] Cancelled operations produce a clear cancellation error (not a generic timeout)
- [ ] Long-lived subscriptions accept and honor the cancellation mechanism (context in Go, CancellationToken in C#, etc.). Cancelling the context unsubscribes.

### REQ-CONC-3: Subscription Callback Behavior

**Acceptance criteria:**
- [ ] Document whether callbacks may fire concurrently
- [ ] Default callback concurrency is 1 (sequential processing). Higher concurrency is opt-in via `maxConcurrentCallbacks` option (or language equivalent).
- [ ] Provide a mechanism to control callback concurrency (e.g., max concurrent handlers)
- [ ] Callbacks must not block the SDK's internal event loop / connection reader
- [ ] Long-running callbacks should be documented with guidance (use a worker pool)

### REQ-CONC-4: Async-First Where Idiomatic

| Language | Primary API Style |
|----------|------------------|
| Go | Synchronous (goroutines for async) |
| Java | Both sync and async (`CompletableFuture`) |
| C# | Async-first (`async Task`) with sync wrappers if needed |
| Python | Both sync and async. Sync as primary API, async as opt-in. |
| JS/TS | Async-only (`Promise` / `async/await`) |

**Acceptance criteria:**
- [ ] Primary API style matches language convention
- [ ] Async APIs don't block the calling thread/event loop

### REQ-CONC-5: Shutdown-Callback Safety

`Close()` must handle in-flight callbacks gracefully.

**Acceptance criteria:**
- [ ] `Close()` waits for in-flight callbacks to complete, with a configurable timeout (default 30 seconds). Note: this is the callback completion timeout, separate from the drain timeout in REQ-CONN-4 (default: 5s) which covers flushing in-flight operations.
- [ ] Operations attempted after `Close()` return `ErrClientClosed`
- [ ] `Close()` is idempotent (calling it multiple times is safe)

---

## What 4.0+ Looks Like

- Thread-safety guarantees documented on every public type
- Cancellation works on every blocking operation, including long-lived subscriptions
- Subscription callbacks default to sequential processing; concurrency is opt-in
- `Close()` safely drains in-flight callbacks before shutting down
- API style matches language conventions (async in C#/JS, both sync and async in Java/Python, sync in Go)
