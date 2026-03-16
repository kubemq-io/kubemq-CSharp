# Category 5: Observability

**Tier:** 1 (Critical — gate blocker)
**Current Average Score:** 1.46 / 5.0
**Target Score:** 4.0+
**Weight:** 5%

## Purpose

Users must be able to trace messages end-to-end, monitor SDK health via metrics, and correlate logs with traces. OpenTelemetry is the standard. The SDK must support it as an optional dependency with near-zero overhead when not configured.

---

## Requirements

### REQ-OBS-1: OpenTelemetry Trace Instrumentation

The SDK must create OTel spans for all messaging operations.

Based on OTel messaging semconv v1.27.0. All semconv attribute names MUST be defined as constants in one file per SDK.

**Instrumentation scope:** Tracer and Meter must be created with the SDK's module/package identifier as the instrumentation scope name (e.g., `github.com/kubemq-io/kubemq-go`). Instrumentation version must match the SDK version.

**Span configuration:**

| Operation | Span Kind | Span Name Format |
|-----------|-----------|-----------------|
| Publish / Send (Events, Events Store, Queues) | PRODUCER | `publish {channel}` |
| Subscribe callback processing | CONSUMER | `process {channel}` |
| Queue Receive (pull-based) | CONSUMER | `receive {channel}` |
| Queue Ack / Reject / Requeue | CONSUMER | `settle {channel}` |
| Command / Query send | CLIENT | `send {channel}` |
| Command / Query response | SERVER | `process {channel}` |

> **Rationale for Command/Query span kinds:** CLIENT is used because Commands/Queries are synchronous request-response operations where the sender blocks for a reply. SERVER is the correct counterpart to CLIENT for synchronous RPC semantics.

**Required span attributes (per OTel messaging semconv):**

| Attribute | Value |
|-----------|-------|
| `messaging.system` | `"kubemq"` |
| `messaging.operation.name` | `"publish"`, `"process"`, `"receive"`, `"settle"`, `"send"` |
| `messaging.operation.type` | `publish`, `receive`, `process`, `settle`, `send` (enumerated — see mapping table below) |
| `messaging.destination.name` | Channel/queue name |
| `messaging.message.id` | Message ID (when available) |
| `messaging.client.id` | Client ID |
| `messaging.consumer.group.name` | Consumer group name (for Events Store group subscriptions) |
| `server.address` | Server hostname |
| `server.port` | Server port |
| `error.type` | Error type (when operation fails) |

**`messaging.operation.type` mapping:**

| Operation | `messaging.operation.name` | `messaging.operation.type` |
|-----------|---------------------------|---------------------------|
| Publish / Send (Events, Events Store, Queues) | `publish` | `publish` |
| Subscribe callback processing | `process` | `process` |
| Queue Receive (pull-based) | `receive` | `receive` |
| Queue Ack / Reject / Requeue | `settle` | `settle` |
| Command / Query send | `send` | `send` |
| Command / Query response | `process` | `process` |

**Recommended span attributes:**

| Attribute | Value |
|-----------|-------|
| `messaging.message.body.size` | Size of the message body in bytes |

**Producer-consumer correlation:**
- Use **span links** (not parent-child) to correlate producer and consumer spans
- Each consumer `process` span links to the producer `publish` span via extracted trace context

**Retry span events:**
Each retry attempt adds a span event named `retry` with attributes:
- `retry.attempt` (int): current attempt number
- `retry.delay_seconds` (float): delay before this attempt
- `error.type` (string): the error that triggered the retry

**Batch consume trace pattern:**
1. One `receive` span for the batch operation with `messaging.batch.message_count` attribute.
2. Per-message `process` spans linked to each message's producer span.
3. Links on the `receive` span to all producer spans (cap at 128 links).

**Acceptance criteria:**
- [ ] Spans are created for all messaging operations listed above
- [ ] All required attributes are set on every span
- [ ] Failed operations set span status to ERROR with error description
- [ ] Batch operations set `messaging.batch.message_count` attribute
- [ ] Span names follow the `{operation} {channel}` format
- [ ] Retry attempts are recorded as span events with required attributes
- [ ] Batch consume operations follow the receive/process span pattern with links

### REQ-OBS-2: W3C Trace Context Propagation

The SDK must inject and extract W3C Trace Context (traceparent/tracestate) via message metadata.

**Producer (inject):**
- Before publishing, inject the current trace context into the message's `tags` field using `TextMapPropagator.Inject()`
- The SDK must implement a `TextMapCarrier` adapter over KubeMQ message `tags` (map[string]string)
- Inject `traceparent` and `tracestate` as tag entries

**Consumer (extract):**
- On message receipt, extract trace context from message `tags` using `TextMapPropagator.Extract()`
- Create a new span linked to the extracted context

**Queue stream downstream (long-lived streams):**
1. One stream-level span for the overall receive operation.
2. Per-message `process` spans linked (not parented) to the original producer span.
3. Settle spans as children of the process span.
4. Each message's trace context must be extracted independently from its tags — never inherited from the stream span.

**RPC round-trip trace shape (Commands/Queries):**
- The sender injects trace context into the command/query message.
- The responder extracts it and creates a linked span.
- The response carries the responder's trace context back.
- Trace shape: `sender.request -> [broker] -> responder.process -> [broker] -> sender.response`

**Requeue and DLQ trace context preservation:**
- Requeued messages preserve `traceparent`/`tracestate` in tags.
- DLQ transitions preserve trace context.
- Use a generic span event name (e.g., `message.dead_lettered`) and standard attributes where possible.

**Acceptance criteria:**
- [ ] `traceparent` and `tracestate` headers are injected into published messages
- [ ] Consumers extract trace context and create linked spans
- [ ] Trace context survives round-trip: publish → server → consume
- [ ] Batch publishes inject per-message trace context
- [ ] Missing trace context in consumed messages is handled gracefully (no error)
- [ ] Trace context is preserved through requeue and DLQ operations

### REQ-OBS-3: OpenTelemetry Metrics

The SDK must emit OTel metrics for key operations.

**Instrumentation scope:** Meter must be created with the SDK's module/package identifier as the instrumentation scope name (e.g., `github.com/kubemq-io/kubemq-go`). Instrumentation version must match the SDK version.

**Required metrics:**

| Metric | Instrument | Unit | Description |
|--------|-----------|------|-------------|
| `messaging.client.operation.duration` | Histogram | seconds | Duration of each operation |
| `messaging.client.sent.messages` | Counter | `{message}` | Total messages sent |
| `messaging.client.consumed.messages` | Counter | `{message}` | Total messages consumed |
| `messaging.client.connection.count` | UpDownCounter | `{connection}` | Active connections |
| `messaging.client.reconnections` | Counter | `{attempt}` | Reconnection attempts (increments on each reconnection attempt — entry to RECONNECTING state — not on successful reconnection) |
| `kubemq.client.retry.attempts` | Counter | `{attempt}` | Retry attempts (attributes: `messaging.operation.name`, `error.type`) |
| `kubemq.client.retry.exhausted` | Counter | `{attempt}` | Retries exhausted (attributes: `messaging.operation.name`, `error.type`) |

**Histogram bucket boundaries:** `[0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, 30, 60]` seconds

**Required metric attributes:** `messaging.system`, `messaging.operation.name`, `messaging.destination.name` (when below cardinality threshold — see Cardinality Management), `error.type` (on failures)

**`error.type` value mapping (from REQ-ERR-2 categories):**

| Error Category | `error.type` Value |
|---------------|-------------------|
| Transient / connectivity | `transient` |
| Timeout | `timeout` |
| Throttling / rate limit | `throttling` |
| Authentication failure | `authentication` |
| Authorization failure | `authorization` |
| Validation error | `validation` |
| Resource not found | `not_found` |
| Fatal / unrecoverable | `fatal` |
| Cancellation | `cancellation` |
| Backpressure | `backpressure` |

**Cardinality Management:**

Unbounded `messaging.destination.name` values can cause metric cardinality explosion. The SDK must implement cardinality controls:

1. **Configurable threshold:** Maximum unique values for `messaging.destination.name` (default: 100).
2. **Omit attribute above threshold:** When the number of unique channel names exceeds the threshold, omit `messaging.destination.name` from new metric series.
3. **Allowlist:** Support an explicit allowlist of channel names that are always included regardless of threshold.
4. **OTel Metric Views:** Document how users can use OTel Metric Views for additional user-side cardinality control.
5. **WARN log:** Emit a WARN-level log when the cardinality threshold is exceeded.

**Acceptance criteria:**
- [ ] All required metrics are emitted
- [ ] Metrics use correct instrument types (histogram for durations, counter for totals)
- [ ] Metric names follow OTel messaging semantic conventions
- [ ] Metrics have required attributes
- [ ] Duration histograms use the specified bucket boundaries
- [ ] Cardinality management controls are implemented and configurable

### REQ-OBS-4: Near-Zero Cost When Not Configured

OTel must be an optional dependency. When no OTel SDK is registered, instrumentation must have near-zero overhead.

**Architecture:**
- SDK depends only on the OTel **API** package (not the SDK)
- OTel API provides no-op implementations by default
- When user registers an OTel SDK provider, instrumentation activates automatically
- SDK accepts optional `TracerProvider` and `MeterProvider` via client options
- If not provided, falls back to `otel.GetTracerProvider()` / `otel.GetMeterProvider()` (global, which is no-op if unconfigured)

Each SDK must document its minimum supported OTel API version in its README and treat OTel API major version bumps as breaking changes.

Per-channel trace filtering can be achieved using a custom OTel Sampler configured on the TracerProvider.

**Acceptance criteria:**
- [ ] OTel API is the only observability dependency (no SDK dependency)
- [ ] No-op provider is used when OTel SDK is not registered — near-zero overhead
- [ ] `TracerProvider` and `MeterProvider` can be injected via client options
- [ ] Guard expensive attribute computation with `span.IsRecording()` check
- [ ] OTel integration is documented with a complete setup example
- [ ] OTel instrumentation with no-op provider adds less than 1% latency overhead at p99

### REQ-OBS-5: Structured Logging Hooks

The SDK must support structured logging without forcing a specific logging framework.

**Approach:**
- Define a minimal logging interface (e.g., `Logger` with `Debug`, `Info`, `Warn`, `Error` methods accepting a message string and structured key-value fields)
- Provide a default no-op logger
- Accept user-provided logger via client options
- When OTel trace context is active, include `trace_id` and `span_id` in log entries

**Logger interface example (structured key-value fields):**

```
// Go
type Logger interface {
    Debug(msg string, keysAndValues ...interface{})
    Info(msg string, keysAndValues ...interface{})
    Warn(msg string, keysAndValues ...interface{})
    Error(msg string, keysAndValues ...interface{})
}

// C# / .NET
public interface ILogger {
    void Debug(string message, params KeyValuePair<string, object>[] fields);
    void Info(string message, params KeyValuePair<string, object>[] fields);
    void Warn(string message, params KeyValuePair<string, object>[] fields);
    void Error(string message, params KeyValuePair<string, object>[] fields);
}

// Java
public interface Logger {
    void debug(String msg, Object... keysAndValues);
    void info(String msg, Object... keysAndValues);
    void warn(String msg, Object... keysAndValues);
    void error(String msg, Object... keysAndValues);
}

// Python
class Logger(Protocol):
    def debug(self, msg: str, **kwargs: Any) -> None: ...
    def info(self, msg: str, **kwargs: Any) -> None: ...
    def warn(self, msg: str, **kwargs: Any) -> None: ...
    def error(self, msg: str, **kwargs: Any) -> None: ...
```

**What to log:**

| Level | Events |
|-------|--------|
| DEBUG | Retry attempts, keepalive pings, state transitions, individual publish/receive events |
| INFO | Connection established, reconnection, subscription created, graceful shutdown |
| WARN | Insecure configuration (skip_verify), buffer near capacity, deprecated API usage |
| ERROR | Connection failed (after retries exhausted), auth failure, unrecoverable error |

**Acceptance criteria:**
- [ ] Logger interface is defined with structured key-value fields and documented
- [ ] Default logger is no-op (no output unless user configures one)
- [ ] User can inject their preferred logger (zap, slog, logback, NLog, etc.)
- [ ] Log entries include `trace_id` and `span_id` when OTel context is available
- [ ] Sensitive data (tokens, credentials) is never logged at any level
- [ ] Log levels are appropriate (not spamming INFO with per-message entries)
- [ ] Per-message logging (individual publish/receive events) must be DEBUG or TRACE level only, never INFO

---

## What 4.0+ Looks Like

- Every message publish/consume creates an OTel span with full attributes including `messaging.operation.type`
- Span kinds correctly reflect operation semantics (PRODUCER/CONSUMER for messaging, CLIENT/SERVER for RPC)
- Traces flow end-to-end: producer → server → consumer, linked via W3C Trace Context
- Queue stream downstream, RPC round-trips, requeue, and DLQ operations all preserve trace context
- Batch consume operations produce properly linked receive/process span hierarchies
- Retry attempts are recorded as span events with attempt count and delay
- Metrics dashboards show throughput, latency percentiles, error rates, connection state
- Histogram buckets are tuned for messaging latency distributions (sub-ms to 60s)
- Metric cardinality is managed with configurable thresholds and allowlists
- Retry metrics track attempt counts and exhaustion rates
- Near-zero overhead when OTel is not configured — less than 1% p99 latency impact with no-op provider
- Logs are structured with key-value fields and correlated with traces via trace_id/span_id
- Per-message logging stays at DEBUG/TRACE level to avoid log volume issues
- All semconv attribute names defined as constants; instrumentation scope properly configured
- Complete setup examples showing OTel integration with Jaeger/Grafana/etc.
