# Category 2: Connection & Transport

**Tier:** 1 (Critical — gate blocker)
**Current Average Score:** 2.70 / 5.0
**Target Score:** 4.0+
**Weight:** 11%

> **Scope:** This specification covers gRPC transport only. REST and WebSocket transports exist in some current SDKs and are addressed separately. Browser environments (JavaScript/TypeScript) may require gRPC-Web or WebSocket transport; a separate specification will cover those requirements.

## Purpose

Connections must be resilient, self-healing, and transparent. Users should not need to write reconnection logic. The SDK handles transient network failures, server restarts, and load balancer events automatically.

> **Note:** This specification is transport-agnostic. The SDK relies on Kubernetes infrastructure (kube-proxy, DNS) for service discovery and does not import Kubernetes-specific libraries.

> **Note:** Publisher and consumer flow control (backpressure signaling, send windows, consumer prefetch) is tracked as a separate work item and may be addressed in a future specification.

---

## Requirements

### REQ-CONN-1: Auto-Reconnection with Buffering

The SDK must automatically reconnect when the gRPC connection drops, without user intervention.

**Reconnection behavior:**

| Parameter | Default | Configurable |
|-----------|---------|-------------|
| Max reconnect attempts | Unlimited (-1) | Yes |
| Initial reconnect delay | 500ms | Yes |
| Max reconnect delay | 30s | Yes |
| Reconnect backoff multiplier | 2.0 | Yes |
| Reconnect jitter | Full jitter | Yes |
| Reconnect buffer size | 8 MB | Yes |

**During reconnection:**
- Published messages are buffered in memory up to `ReconnectBufferSize`
- When buffer is full, publish calls return a `BufferFullError` (non-blocking) or block (configurable)
- Subscriptions are automatically re-established after reconnection
- Queue consumers resume from their last acknowledged position

**DNS re-resolution:** On each reconnection attempt, the SDK must re-resolve the configured address via DNS. Cached IP addresses from the previous connection must not be reused.

**Backoff reset:** After a successful reconnection, the backoff delay and attempt counter reset to their initial values.

**Retry Layer Interaction:**
- In `RECONNECTING` state, operation-level retries (REQ-ERR-3) are suspended. If `WaitForReady=true`, operations block until `READY` or operation timeout. If `WaitForReady=false`, operations fail immediately with `ConnectionNotReadyError` (non-retryable, no operation retry).
- In `READY` state, operation-level retries handle server-side transient errors only.
- Cross-reference: Operation retries (REQ-ERR-3) do not apply to transport-level connection loss. See this requirement for connection-level recovery.

**Subscription Recovery Semantics:**

| Pattern | Recovery Behavior |
|---------|-------------------|
| Events | Re-subscribe to same channel. Messages during outage are lost (fire-and-forget semantics). |
| Events Store | Track last received sequence number locally. Re-subscribe with `StartFromSequence(lastSeq + 1)`. The original `StartFrom*` parameter reflects initial intent, not reconnection intent. |
| Queue stream (downstream) | Re-establish stream. Unacked messages return to queue via visibility timeout. |
| RPC Commands/Queries | Re-subscribe handlers to same channels. |

> **Note:** During reconnection, server-side visibility timers continue. If reconnection exceeds the visibility timeout, unacknowledged messages may be delivered to another consumer. This is consistent with at-least-once delivery semantics.

**Stream vs. connection errors:** Server-initiated stream closure (e.g., channel deletion, server restart) triggers subscription-level recovery (re-subscribe on the existing connection), not connection-level recovery (reconnect). The SDK must distinguish between stream errors and connection errors.

**Buffer lifecycle:**
- Buffered messages are sent in FIFO order after reconnection. The SDK does not perform deduplication; applications requiring exactly-once semantics must implement idempotency at the application layer.
- When the client transitions to `CLOSED` (either via `Close()` or max reconnect attempts exhausted), all buffered messages are discarded. If an `OnBufferDrain` callback is registered, it fires with the count of discarded messages.

**Acceptance criteria:**
- [ ] Connection drops are detected within keepalive timeout (default 15s, per REQ-CONN-3: keepalive_time 10s + keepalive_timeout 5s)
- [ ] Reconnection starts automatically with exponential backoff
- [ ] Messages published during reconnection are buffered and sent on reconnect
- [ ] Subscriptions are restored transparently after reconnection, per the recovery semantics table
- [ ] Buffer overflow behavior is configurable (error vs block)
- [ ] Reconnection attempts are logged at INFO level
- [ ] Successful reconnection is logged at INFO level
- [ ] DNS is re-resolved on each reconnection attempt
- [ ] Backoff delay and attempt counter reset after successful reconnection
- [ ] Operation retries are suspended during RECONNECTING state
- [ ] Stream errors are distinguished from connection errors and handled at the subscription level
- [ ] Buffered messages are sent in FIFO order after reconnection
- [ ] Buffered messages are discarded on transition to CLOSED, with callback notification

### REQ-CONN-2: Connection State Machine

The SDK must track connection state and expose it to users.

**States:**

```
IDLE ──> CONNECTING ──> READY
  ^          |             |
  |          v             v
  |    RECONNECTING ──> READY
  |          |
  v          v
       CLOSED (terminal)
```

| State | Description |
|-------|-------------|
| `IDLE` | Created but not yet connected |
| `CONNECTING` | Initial connection in progress |
| `READY` | Connected and operational |
| `RECONNECTING` | Connection lost, attempting to reconnect |
| `CLOSED` | Permanently closed (terminal state) |

**Acceptance criteria:**
- [ ] Current state is queryable via a method (e.g., `client.State()`)
- [ ] State transitions fire callbacks/events
- [ ] Users can register handlers for: `OnConnected`, `OnDisconnected`, `OnReconnecting`, `OnReconnected`, `OnClosed`
- [ ] Handlers are invoked asynchronously (never block the connection)
- [ ] State is included in log messages during transitions

### REQ-CONN-3: gRPC Keepalive Configuration

> **Note:** A gRPC health check endpoint (`grpc.health.v1.Health`) will be added to the KubeMQ server in a future version. SDKs should use keepalive-based connection detection for now and add health check support when available, with graceful fallback for older servers that don't expose the endpoint.

The SDK must configure gRPC keepalive to detect dead connections proactively.

**Default configuration:**

| Parameter | Default |
|-----------|---------|
| Keepalive time | 10s |
| Keepalive timeout | 5s |
| Permit without stream | true |

> **Cloud load balancer advisory:** The 10s keepalive default is optimized for direct and in-cluster Kubernetes connections. When connecting through cloud load balancers (AWS ALB/NLB, GCP CLB, Azure LB), verify that the load balancer's idle timeout exceeds the keepalive interval. The default values are compatible with all major cloud providers' default settings.

> **Server idle timeout:** KubeMQ server closes connections idle for more than 5 minutes. With keepalive enabled (default), pings prevent idle disconnection. If keepalive is disabled, idle connections will be closed by the server.

**Acceptance criteria:**
- [ ] Keepalive is enabled by default
- [ ] All three keepalive parameters are configurable
- [ ] Dead connections are detected within `keepalive_time + keepalive_timeout` (default: 15s)
- [ ] Keepalive parameters are compatible with KubeMQ server's enforcement policy

### REQ-CONN-4: Graceful Shutdown / Drain

The SDK must support graceful shutdown that completes in-flight work before closing.

**Drain behavior:**
1. Stop accepting new operations (publish, subscribe, queue operations)
2. Flush all buffered messages
3. Wait for in-flight operations to complete, including messages received from the server but not yet delivered to the application's callback/handler (with timeout)
4. Close the gRPC connection
5. Fire the `OnClosed` callback

**Close() during RECONNECTING state:** If `Close()` is called while the client is in `RECONNECTING` state, the SDK must cancel reconnection immediately, discard all buffered messages (firing `OnBufferDrain` with the count of discarded messages if registered), and transition directly to `CLOSED`. The drain timeout does not apply in this case since there is no active connection to drain.

**Acceptance criteria:**
- [ ] `Close()` or `Shutdown()` method initiates graceful shutdown
- [ ] Optional timeout parameter for maximum drain duration (default: 5s). Note: this is the drain timeout for flushing in-flight operations. REQ-CONC-5 specifies a separate callback completion timeout (default: 30s) for waiting on in-flight subscription callbacks.
- [ ] In-flight operations complete before connection closes
- [ ] Buffered messages are flushed before connection closes
- [ ] New operations after `Close()` is called return `ErrClientClosed`
- [ ] `Close()` is idempotent — calling it multiple times is safe
- [ ] `Close()` during RECONNECTING cancels reconnection and discards buffered messages

### REQ-CONN-5: Connection Configuration

Standard connection parameters with sensible defaults.

**Configuration options:**

| Option | Default | Description |
|--------|---------|-------------|
| Address | `localhost:50000` | Server address (host:port) |
| Connection timeout | 10s | Maximum time to establish initial connection |
| Max receive message size | 100 MB | Maximum inbound gRPC message size |
| Max send message size | 100 MB | Maximum outbound gRPC message size |
| Wait for ready | true | Block operations until connection is READY |

**WaitForReady scope:** When `WaitForReady` is true, operations block during both `CONNECTING` and `RECONNECTING` states until the connection enters `READY` state or the operation timeout expires. When false, operations fail immediately if the state is not `READY`. This is an SDK-level behavior layered on top of gRPC's native `WaitForReady` call option.

> **Proxy support:** Proxy support (HTTP CONNECT, SOCKS5) is inherited from the underlying gRPC library. Configure via standard environment variables (`HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY`).

**Acceptance criteria:**
- [ ] All connection parameters are configurable via SDK options/builder
- [ ] Defaults match the values in the table above
- [ ] Connection timeout applies to the initial connection only (reconnection has its own policy)
- [ ] Invalid configuration (empty address, negative timeout) is rejected at construction time (fail-fast)
- [ ] WaitForReady applies to both CONNECTING and RECONNECTING states

### REQ-CONN-6: Connection Reuse

The SDK must use a single long-lived gRPC channel for all operations.

**Acceptance criteria:**
- [ ] A single `Client` instance uses one gRPC channel (connection)
- [ ] Multiple concurrent operations (publish, subscribe, queue) multiplex over the same channel
- [ ] Documentation advises users to create one `Client` and share it across goroutines/threads
- [ ] Creating a new gRPC channel per operation is explicitly prohibited in the implementation

---

## What 4.0+ Looks Like

- Connection drops are invisible to the user — auto-reconnect handles everything
- Messages published during brief outages are buffered and delivered on reconnect
- Subscriptions survive reconnection without user intervention, with pattern-specific recovery (Events Store resumes from last sequence, queue streams respect visibility timeouts)
- Connection state is observable via callbacks and queryable programmatically
- Graceful shutdown completes all in-flight work, including pending callback delivery
- Close() during reconnection is well-defined — cancels immediately, notifies about discarded buffers
- Keepalive detects dead connections within 15 seconds
- Connection configuration uses sensible defaults — zero config works for local development
- Operation retries and connection retries are cleanly separated — no dual retry loop conflicts
- DNS is re-resolved on every reconnection attempt, ensuring correct behavior after pod restarts
- Stream errors and connection errors are handled at the appropriate layer
- Buffered messages maintain FIFO ordering and have defined discard semantics
