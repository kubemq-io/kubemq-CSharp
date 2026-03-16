# Category 1: Error Handling & Resilience

**Tier:** 1 (Critical — gate blocker)
**Current Average Score:** 2.10 / 5.0
**Target Score:** 4.0+
**Weight:** 11%

## Purpose

Users must be able to understand, handle, and recover from errors without reading SDK source code. Transient failures must be retried automatically. Error messages must help resolve problems, not just state them.

---

## Requirements

### REQ-ERR-1: Typed Error Hierarchy

Every SDK must define a structured error type hierarchy. All errors returned by the SDK must be instances of SDK-defined types, never raw gRPC errors or generic language exceptions.

**Error type fields (minimum):**

| Field | Type | Description |
|-------|------|-------------|
| `Code` | enum/const | Machine-readable error code (e.g., `ErrConnectionTimeout`, `ErrAuthFailed`) |
| `Message` | string | Human-readable description |
| `Operation` | string | What operation failed (e.g., `SendMessage`, `Subscribe`) |
| `Channel` | string | Which channel/queue was targeted |
| `IsRetryable` | boolean | Whether the error is transient and retryable |
| `Cause` | error | Underlying/wrapped error (preserving the chain) |
| `RequestID` | string | Client-generated unique ID for correlating with server logs |

**Optional fields (recommended):**

| Field | Type | Description |
|-------|------|-------------|
| `MessageID` | string | Which message failed (if applicable) |
| `StatusCode` | int | gRPC status code |
| `Timestamp` | time | When the error occurred |
| `ServerAddress` | string | Which server endpoint returned the error |

**Acceptance criteria:**
- [ ] All SDK methods return SDK-typed errors, not raw gRPC `status.Error`
- [ ] Error types support language-standard unwrapping (Go: `errors.Is/As/Unwrap`, Java: `getCause()`, C#: `InnerException`, Python: `__cause__`, JS: `cause`)
- [ ] Error codes are documented and stable across releases
- [ ] Error codes follow semantic versioning: new codes may be added in minor versions, existing codes are never removed or changed in meaning within a major version. See Category 12 for full versioning policy.

### REQ-ERR-2: Error Classification

Every error must be classified into one of these categories:

| Category | Retryable | gRPC Codes | Action |
|----------|-----------|------------|--------|
| **Transient** | Yes | `UNAVAILABLE`, `ABORTED` | Auto-retry with backoff |
| **Timeout** | Yes (with caution) | `DEADLINE_EXCEEDED` | Auto-retry, may need longer timeout |
| **Throttling** | Yes (longer backoff) | `RESOURCE_EXHAUSTED` | Auto-retry with extended backoff |
| **Authentication** | No | `UNAUTHENTICATED` | Credential refresh may help |
| **Authorization** | No | `PERMISSION_DENIED` | Do not retry, fix permissions |
| **Validation** | No | `INVALID_ARGUMENT`, `FAILED_PRECONDITION` | Do not retry, fix input |
| **Not Found** | No | `NOT_FOUND` | Do not retry, resource missing |
| **Fatal** | No | `INTERNAL`, `UNIMPLEMENTED`, `DATA_LOSS` | Do not retry |
| **Cancellation** | No | `CANCELLED` (client-initiated) | Do not retry, operation was cancelled by caller |
| **Backpressure** | No | N/A (SDK-generated) | Wait for reconnection or increase buffer size |

**Acceptance criteria:**
- [ ] Every error returned by the SDK has a classification
- [ ] `IsRetryable` flag is accurate for all error types
- [ ] Classification is documented in the SDK's error reference
- [ ] `BufferFullError` is classified as `Backpressure` with `IsRetryable=false`

### REQ-ERR-3: Auto-Retry with Configurable Policy

The SDK must automatically retry transient errors before surfacing them to the user. The retry policy must be configurable.

**Default retry policy:**

| Parameter | Default | Range |
|-----------|---------|-------|
| Max retries | 3 | 0–10 |
| Initial backoff | 500ms | 50ms–5s |
| Max backoff | 30s | 1s–120s |
| Backoff multiplier | 2.0 | 1.5–3.0 |
| Jitter | Full jitter | Full, Equal, or None |

**Retry algorithm:**
```
Full jitter:    sleep = random(0, min(maxBackoff, initialBackoff * multiplier ^ attempt))
Equal jitter:   temp = min(maxBackoff, initialBackoff * multiplier ^ attempt); sleep = temp/2 + random(0, temp/2)
```

**Retry Safety by Operation Type:**

Not all operations are safe to retry on ambiguous failures. The SDK must respect the following safety classification:

| Operation | Safe to Retry | Notes |
|-----------|---------------|-------|
| Events Publish | Yes | Fire-and-forget, duplicates acceptable |
| Events Store Publish | Yes | Server-side sequence dedup prevents duplicates |
| Queue Send | NO (by default) | Only retry if error proves server did NOT receive (connection refused, `UNAVAILABLE` before stream established). For `DEADLINE_EXCEEDED`, do NOT auto-retry -- return the error with `IsRetryable=false` and a message: "Request may have been processed by the server. Check before retrying." |
| Command/Query (RPC) | NO (by default) | Same rules as Queue Send |
| Subscribe | Yes | Idempotent re-subscribe |

**gRPC retry disabled:** gRPC-level retry is disabled; all retry logic is handled by the SDK. `grpc.EnableRetry()` must NOT be called, and `maxRetries` must not be set in the gRPC service config. This prevents double-retry amplification.

**Retry policy immutability:** Retry policy is immutable after client construction. To change retry behavior, create a new client instance.

**Independent backoff policies:** Operation retry backoff (this section) and connection reconnection backoff (Category 2, REQ-CONN-1) are independent policies with independent configuration. Changing one does not affect the other, even though their defaults are identical.

**Worst-case latency:** Users should understand total worst-case latency before configuring retry policies. The total latency is the sum of (operation timeout + backoff delay) across all retry attempts. Example with defaults: SendMessage with 3 retries = 5s + 0.5s + 5s + 1s + 5s + 2s + 5s = 23.5s worst case (with no jitter). SDKs SHOULD document this calculation.

**Acceptance criteria:**
- [ ] Transient and timeout errors are retried automatically by default
- [ ] Retry policy is configurable via SDK options/builder
- [ ] Retries can be disabled entirely (`maxRetries = 0`)
- [ ] Each retry attempt is logged at DEBUG level
- [ ] After exhausting retries, the last error is returned with context about retry attempts
- [ ] Non-retryable errors are returned immediately without retry
- [ ] Non-idempotent operations are not auto-retried on ambiguous failures (`DEADLINE_EXCEEDED`)
- [ ] gRPC-level retry is disabled; all retry logic is handled by the SDK
- [ ] Retry policy cannot be modified after the client is created
- [ ] Worst-case latency is documented for default retry policy settings

### REQ-ERR-4: Per-Operation Timeouts

Every blocking/async operation must accept a timeout or deadline.

**Default timeouts:**

| Operation | Default Timeout |
|-----------|----------------|
| Send / Publish | 5s |
| Subscribe (initial connection) | 10s |
| Request / Query (RPC) | 10s |
| Queue Receive (single) | 10s |
| Queue Receive (streaming/poll) | 30s |
| Connection establishment | See Category 2, REQ-CONN-5 (default: 10s) |

**Acceptance criteria:**
- [ ] Go: every method accepts `context.Context` for timeout/cancellation
- [ ] Java: every method accepts `Duration timeout` parameter or uses `CompletableFuture` with `.orTimeout()`
- [ ] C#: every async method accepts `CancellationToken`
- [ ] Python: every method accepts `timeout` parameter (seconds as float)
- [ ] JS/TS: every method accepts `AbortSignal` or `timeout` in options
- [ ] Default timeouts are applied when user doesn't specify one
- [ ] Timeout errors are classified as retryable (with caution)

### REQ-ERR-5: Actionable Error Messages

Error messages must include enough context to diagnose and resolve the problem.

Error messages MUST contain the following information elements: operation, channel, cause, suggestion, and retry context when applicable. The exact format is language-idiomatic. The template below is a reference example, not a required format:

```
{Operation} failed on channel "{Channel}": {cause}
  Suggestion: {how to fix}
  [Retries exhausted: {n}/{max} attempts over {duration}]
```

**Example:**
```
SendMessage failed on channel "orders.events": connection timeout after 5s (server: kubemq-server:50000)
  Suggestion: Check server connectivity and firewall rules. Current retry policy will not retry (max retries exhausted: 3/3 over 12.4s)
```

**Acceptance criteria:**
- [ ] Error messages include the operation name
- [ ] Error messages include the target channel/queue when applicable
- [ ] Error messages include a suggestion for resolution
- [ ] Retry exhaustion messages include attempt count and total duration
- [ ] Error messages never expose internal implementation details (stack traces, raw gRPC frames)

### REQ-ERR-6: gRPC Error Mapping

All gRPC status codes must be mapped to SDK error types. Raw gRPC errors must never leak to users.

**Mapping:**

| gRPC Code | SDK Error Category | Notes |
|-----------|-------------------|-------|
| `OK` | (no error) | |
| `CANCELLED` | Cancellation / Transient | Client-initiated (local context/token is done): `Cancellation`, NOT retryable, return immediately. Server-initiated (local context still active): `Transient`, retryable. |
| `UNKNOWN` | Transient (max 1 retry) | Retried once regardless of configured max retries; intermediaries (proxies, load balancers) sometimes return this code for transient conditions |
| `INVALID_ARGUMENT` | Validation | Bad request |
| `DEADLINE_EXCEEDED` | Timeout | Retryable with caution (see REQ-ERR-3 Retry Safety by Operation Type) |
| `NOT_FOUND` | NotFound | Channel/queue doesn't exist |
| `ALREADY_EXISTS` | Validation | Duplicate |
| `PERMISSION_DENIED` | Authorization | Wrong permissions |
| `RESOURCE_EXHAUSTED` | Throttling | Rate limited |
| `FAILED_PRECONDITION` | Validation | State precondition not met |
| `ABORTED` | Transient | Conflict, retry |
| `OUT_OF_RANGE` | Validation | Iterator/pagination boundary exceeded |
| `UNIMPLEMENTED` | Fatal | Feature not supported |
| `INTERNAL` | Fatal | Server error. Optional: allow a single retry (configurable, default: no retry). This is a user opt-in, not a default behavior change. |
| `UNAVAILABLE` | Transient | Server temporarily unavailable |
| `DATA_LOSS` | Fatal | Unrecoverable |
| `UNAUTHENTICATED` | Authentication | Invalid credentials |

**Future enhancement:** If the server provides retry timing hints via gRPC metadata (e.g., `Retry-After`), the SDK SHOULD respect them for `RESOURCE_EXHAUSTED` responses.

**Acceptance criteria:**
- [ ] All 17 gRPC status codes are mapped (0-16)
- [ ] The original gRPC error is preserved in the error chain (unwrappable)
- [ ] Rich error details from `google.rpc.Status` are extracted when present
- [ ] `CANCELLED` is correctly split between client-initiated (not retryable) and server-initiated (retryable)
- [ ] `UNKNOWN` is retried at most once regardless of retry policy configuration
- [ ] Error events SHOULD be recorded as OpenTelemetry span events. See Category 5 for observability requirements.

### REQ-ERR-7: Retry Throttling

The SDK must limit concurrent retry attempts to prevent retry storms during server brownouts.

**Default policy:**

| Parameter | Default | Range |
|-----------|---------|-------|
| Max concurrent retries | 10 | 0 (unlimited, not recommended) – 100 |

When the concurrent retry limit is reached, new retry attempts are skipped and the error is returned immediately to the caller with a flag indicating the retry was throttled.

**Acceptance criteria:**
- [ ] Concurrent retry attempts are limited per client instance
- [ ] The limit is configurable via SDK options/builder
- [ ] When the limit is reached, errors are returned immediately with a throttle indicator
- [ ] Retry attempts are throttled to prevent retry storms during server brownouts

### REQ-ERR-8: Streaming Error Handling

KubeMQ uses bidirectional streaming for subscriptions and queue downstream operations. Stream errors are fundamentally different from unary errors and require distinct handling.

**Stream-level errors:** When a stream-level error occurs (RST_STREAM, server closure, etc.), the SDK triggers stream reconnection -- not connection reconnection. Stream reconnection uses the same backoff policy as operation retry (REQ-ERR-3).

**Per-message errors:** Individual message errors within a stream invoke the per-message error callback without terminating the stream. The stream continues processing subsequent messages.

**In-flight message handling:** When a stream breaks, in-flight messages (sent but not acknowledged) are reported via error callback with a `StreamBrokenError` that includes the list of unacknowledged message IDs.

**Cross-reference:** A broken stream does NOT change connection state from `READY` unless the underlying connection is also broken. See Category 2, REQ-CONN-2 for connection state semantics.

**Acceptance criteria:**
- [ ] Stream-level errors trigger stream reconnection with backoff, not connection reconnection
- [ ] Per-message errors do not terminate the stream
- [ ] `StreamBrokenError` reports unacknowledged message IDs when the stream breaks
- [ ] Stream state is independent of connection state

### REQ-ERR-9: Async Error Propagation

Subscription and consumer operations are inherently asynchronous. Errors in these contexts must have a defined propagation path to prevent silent failures.

**Error callback requirement:** Subscription and consumer operations MUST accept an error callback/handler parameter.

**Error type distinction:** Transport errors (stream broken, auth expired) and handler errors (user code panic/exception) MUST be distinguished via separate error types or separate callbacks.

**Handler error isolation:** Handler errors (exceptions in user-provided message processing code) MUST NOT terminate the subscription. The subscription continues processing subsequent messages.

**Default behavior:** If no error callback is registered, errors MUST be logged at ERROR level via the SDK's logger.

**Acceptance criteria:**
- [ ] Subscription and consumer operations accept an error callback/handler parameter
- [ ] Transport errors and handler errors are distinguishable
- [ ] Handler errors do not terminate the subscription
- [ ] Unhandled async errors are logged at ERROR level
- [ ] Async errors (subscription/consumer) are propagated to user-registered error handlers

---

## Future Enhancements

**Partial batch failure handling:** KubeMQ's batch queue send protocol is all-or-nothing per the Go server assessment. The SDK returns a single error for the entire batch. A `PartialFailureError` type SHOULD be added to the error hierarchy now (even if unused) so the type exists if per-message batch status is added in a future server version.

**Server-side idempotency keys:** The immediate approach is client-side safety rules (REQ-ERR-3 Retry Safety by Operation Type). Long-term, an `IdempotencyKey` field should be added to message types, with server-side dedup support in a future KubeMQ server version. SDKs should design their message types with room for this field.

**Assessment framework traceability:** The graceful degradation scenarios from assessment framework 4.4.3 are collectively addressed by: REQ-ERR-7 (retry throttling for batch failures), REQ-ERR-8 (streaming errors for network drops mid-stream), and REQ-ERR-9 (async error propagation for independent subscription failures).

---

## What 4.0+ Looks Like

- Complete typed error hierarchy with all fields populated, including `RequestID` for production correlation
- Error classification is 100% accurate — no misclassified errors, including `Cancellation` and `Backpressure` categories
- Auto-retry works transparently for transient failures with configurable policy, respecting operation-type safety rules for non-idempotent operations
- Retry throttling prevents retry storms during server brownouts
- Every operation has a configurable timeout with sensible defaults
- Error messages are actionable — a developer can resolve most errors without searching docs
- gRPC errors are fully wrapped — users never see raw `status.Error` types, all 17 status codes are mapped
- Streaming errors are handled distinctly from unary errors, with proper stream reconnection and in-flight message reporting
- Async errors from subscriptions and consumers propagate to user-registered handlers, never silently swallowed
- Error handling is documented with examples for every error category
