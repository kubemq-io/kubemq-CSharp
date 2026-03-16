# KubeMQ C# SDK — Gap Analysis Report

**Date:** 2026-03-13
**SDK Version:** v3.x (src/KubeMQ.Sdk)
**Reference:** [sdk-api-feature-list.md](sdk-api-feature-list.md) | [sdk-compliance-checklist.md](sdk-compliance-checklist.md)
**Overall Compliance:** 88.2% fully implemented (164/186), 93.5% including partial (175/186)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Critical Gaps (Blocking)](#2-critical-gaps-blocking)
3. [Moderate Gaps (Non-Blocking)](#3-moderate-gaps-non-blocking)
4. [Minor Gaps (Polish)](#4-minor-gaps-polish)
5. [Design Deviations](#5-design-deviations)
6. [Gap-by-Category Matrix](#6-gap-by-category-matrix)
7. [Detailed Gap Descriptions](#7-detailed-gap-descriptions)
8. [Remediation Priority](#8-remediation-priority)

---

## 1. Executive Summary

The KubeMQ C# SDK is a mature, well-architected implementation that covers the vast majority of the spec. The core messaging patterns (Events, Events Store, Commands, Queries, Queues) are all implemented with solid production features including exponential-backoff reconnection, typed exceptions, telemetry, and modern `IAsyncEnumerable<T>` subscription patterns.

**Where the SDK excels beyond the spec:**
- Exponential backoff with jitter for reconnection (spec only requires fixed interval)
- Message buffering during reconnect with configurable buffer size and overflow policy
- Dynamic credential provider (`ICredentialProvider`) for token refresh
- Rich exception hierarchy with error categories and retryability flags
- Comprehensive OpenTelemetry integration via `Activity` (though not via the proto `Span` field)

**Where gaps exist:**
- **11 items fully missing** — primarily around queue stream advanced operations and validation
- **11 items partially implemented** — mostly around missing fields or incomplete exposure of features
- No gaps in: Connection lifecycle, Error handling, ID auto-generation, Deprecated API removal

---

## 2. Critical Gaps (Blocking)

These gaps affect correctness, data integrity, or spec compliance in ways that could impact production usage.

### GAP-001: Metadata/Body Presence Validation Missing

| Attribute | Value |
|-----------|-------|
| **Severity** | Critical |
| **Spec Reference** | §10.1 — "At least one of `Metadata` or `Body` must be non-empty" |
| **Affected Operations** | Events send, Events Store send, Commands send, Queries send, Queue messages send |
| **Current Behavior** | Messages with empty Body AND null/empty Metadata pass client-side validation and are sent to the server |
| **Expected Behavior** | SDK should reject such messages with a validation error before making any gRPC call |
| **Impact** | Server may silently accept empty messages or return cryptic errors instead of clear client-side validation |
| **File** | `src/KubeMQ.Sdk/Internal/Protocol/MessageValidator.cs` |
| **Fix Complexity** | Low — add a check to each `Validate*Message` method |

**Root cause:** `ValidateEventMessage`, `ValidateEventStoreMessage`, `ValidateQueueMessage`, `ValidateCommandMessage`, and `ValidateQueryMessage` all validate channel format and numeric ranges but do not check that at least one of Body or Metadata is populated.

**Remediation:**
```csharp
// Add to each Validate*Message method:
if ((message.Body.IsEmpty || message.Body.Length == 0) 
    && string.IsNullOrEmpty(message.Metadata))
{
    throw new KubeMQConfigurationException(
        "At least one of Body or Metadata must be provided.");
}
```

---

### GAP-002: CloseByServer Not Handled in Queue Downstream

| Attribute | Value |
|-----------|-------|
| **Severity** | Critical |
| **Spec Reference** | §4.3 — "SDKs must handle `CloseByServer` (11) responses" |
| **Current Behavior** | If the server sends a `CloseByServer` response on a downstream stream, the SDK does not detect or handle it. The stream remains in `activeDownstreamStreams` as a stale entry. |
| **Expected Behavior** | On receiving `RequestTypeData = CloseByServer`, the SDK should close the stream, remove it from `activeDownstreamStreams`, and optionally reconnect |
| **Impact** | After a server-initiated close, subsequent AckAll/NAckAll operations on the stale transaction will throw confusing gRPC errors instead of a clear "transaction closed by server" error |
| **File** | `src/KubeMQ.Sdk/Client/KubeMQClient.cs` (lines 858–954, 1805–1852) |
| **Fix Complexity** | Medium — requires monitoring the response stream for `CloseByServer` and cleaning up |

**Root cause:** The `ReceiveQueueDownstreamAsync` method reads exactly one response (the `Get` response) and then stores the stream for later settlement operations. There is no background reader monitoring for server-initiated close signals.

---

### GAP-003: Simple API ReceiveQueueMessages (Non-Peek) Not Exposed

| Attribute | Value |
|-----------|-------|
| **Severity** | Critical |
| **Spec Reference** | §5.3 — `ReceiveQueueMessages` with `IsPeak = false` |
| **Current Behavior** | The transport layer (`GrpcTransport.ReceiveQueueMessagesAsync`) and the result type (`QueueReceiveResult`) are implemented, but no public method on `IKubeMQClient` or `KubeMQClient` exposes the non-peek pull API. Only `PeekQueueAsync` (which sets `IsPeak = true`) is available. |
| **Expected Behavior** | A public method like `ReceiveQueueMessagesAsync(channel, maxMessages, waitTimeSeconds)` should be available for the simple pull pattern |
| **Impact** | Users must use the stream-based `PollQueueAsync` for consuming queue messages. The simpler unary-style receive is not available. |
| **File** | `src/KubeMQ.Sdk/Client/KubeMQClient.cs`, `src/KubeMQ.Sdk/Client/IKubeMQClient.cs` |
| **Fix Complexity** | Low — method signature exists at transport level; just needs a public wrapper |

**Root cause:** The `QueueReceiveResult` type at `src/KubeMQ.Sdk/Queues/QueueReceiveResult.cs` is fully defined but orphaned — it is never returned by any public API method. The transport layer has the implementation; only the public surface is missing.

---

## 3. Moderate Gaps (Non-Blocking)

These gaps affect feature completeness but have workarounds or affect extended (non-core) functionality.

### GAP-004: Publish Stream Auto-Reconnect Missing

| Attribute | Value |
|-----------|-------|
| **Severity** | Moderate |
| **Spec Reference** | §2.2, §3.2 — "SDK should handle stream reconnection on disconnect" |
| **Affected Types** | `EventStream`, `EventStoreStream` |
| **Current Behavior** | `EventStream` and `EventStoreStream` are raw stream wrappers with no reconnection logic. If the stream breaks (server disconnect, network failure), the `ReceiveLoopAsync` completes with an error, and the caller must create a new stream. |
| **Expected Behavior** | The stream should automatically reconnect and resume operation, similar to how `WithReconnect` works for subscription streams |
| **Workaround** | Callers wrap the stream in a try/catch and re-call `CreateEventStreamAsync()` / `CreateEventStoreStreamAsync()` on failure |
| **File** | `src/KubeMQ.Sdk/Events/EventStream.cs`, `src/KubeMQ.Sdk/EventsStore/EventStoreStream.cs` |
| **Fix Complexity** | High — requires integration with `ConnectionManager.WaitForReadyAsync()` and stream re-creation logic |

**Contrast with subscriptions:** The `WithReconnect<T>()` wrapper in `KubeMQClient.cs` (lines 1549–1623) provides robust auto-reconnect for all subscription streams. The same pattern is not applied to publish streams.

---

### GAP-005: Queue Stream Auto-Reconnect Missing

| Attribute | Value |
|-----------|-------|
| **Severity** | Moderate |
| **Spec Reference** | §4.1, §4.2 — stream lifecycle and reconnection |
| **Current Behavior** | `SendQueueMessagesUpstreamAsync` opens a new ephemeral stream per call (not persistent). `ReceiveQueueDownstreamAsync` opens a stream per transaction that is stored in `activeDownstreamStreams` but has no reconnect logic. If the connection drops mid-transaction, stale entries remain. |
| **Expected Behavior** | Queue streams should either be persistent with auto-reconnect, or stale entries should be detected and cleaned up on connection loss |
| **Workaround** | Use the simple API for queue sends; for downstream, handle exceptions and retry the entire transaction |
| **File** | `src/KubeMQ.Sdk/Client/KubeMQClient.cs` (lines 780–954) |
| **Fix Complexity** | High — requires `ConnectionManager` integration and stale stream cleanup |

---

### GAP-006: ActiveOffsets and TransactionStatus Not Exposed

| Attribute | Value |
|-----------|-------|
| **Severity** | Moderate |
| **Spec Reference** | §4.2 — Downstream request types 8 (`ActiveOffsets`) and 9 (`TransactionStatus`) |
| **Current Behavior** | The gRPC proto defines these request types but no public API exposes them. `ActiveOffsets` data IS mapped in the response (line 949: `ActiveOffsets = resp.ActiveOffsets.ToList()`), but there's no method to request them. |
| **Expected Behavior** | Two public methods: `GetActiveOffsetsAsync(transactionId)` and `GetTransactionStatusAsync(transactionId)` |
| **Impact** | Users cannot query which messages in a transaction are still unacknowledged, nor check if a transaction is still active. These are "Extended" priority features per the spec. |
| **File** | `src/KubeMQ.Sdk/Client/IKubeMQClient.cs`, `src/KubeMQ.Sdk/Client/KubeMQClient.cs` |
| **Fix Complexity** | Low — the internal `SendDownstreamRequestAsync` method already supports arbitrary request types |

---

### GAP-007: Span (OpenTelemetry Trace Context) Field Not Set

| Attribute | Value |
|-----------|-------|
| **Severity** | Moderate |
| **Spec Reference** | §6.1, §6.3, §7.1 — `Span` field on `Request` and `Response` |
| **Current Behavior** | The SDK implements rich OpenTelemetry integration via `KubeMQActivitySource` (Activity-based tracing with `System.Diagnostics`), but the proto `Span` bytes field on `Request` and `Response` messages is never populated. This means the trace context is not transmitted over the wire to the server or receiver. |
| **Expected Behavior** | The SDK should serialize the current `Activity` trace context into the `Span` field on outgoing `Request`s and copy it from incoming `Request`s to outgoing `Response`s |
| **Impact** | End-to-end distributed tracing across sender → server → handler is not possible via the KubeMQ protocol's built-in span mechanism. Activity-based tracing only covers local client-side spans. |
| **File** | `src/KubeMQ.Sdk/Client/KubeMQClient.cs` (SendCommandAsync, SendQueryAsync, SendCommandResponseAsync, SendQueryResponseAsync) |
| **Fix Complexity** | Medium — requires W3C TraceContext serialization/deserialization to/from bytes |

---

### GAP-008: Batch Send Does Not Expose Per-Message Results

| Attribute | Value |
|-----------|-------|
| **Severity** | Moderate |
| **Spec Reference** | §5.2 — `QueueMessagesBatchResponse` includes `Results[]` per message |
| **Current Behavior** | `SendQueueMessagesAsync` returns a single `QueueSendResult` where `MessageId` is set to the `BatchID`. The individual `SendQueueMessageResult` entries from the batch response are not surfaced. |
| **Expected Behavior** | The response should expose the per-message `Results[]` array so callers know which messages succeeded/failed |
| **Impact** | When one message in a batch fails, the caller cannot determine which one failed |
| **File** | `src/KubeMQ.Sdk/Client/KubeMQClient.cs` (lines 606–666), `src/KubeMQ.Sdk/Queues/QueueSendResult.cs` |
| **Fix Complexity** | Low-Medium — add a `Results` collection to the batch response type |

---

### GAP-009: List Channels Timeout/Snapshot Handling

| Attribute | Value |
|-----------|-------|
| **Severity** | Moderate |
| **Spec Reference** | §8.2.3 — List channels may timeout if non-master node; "cluster snapshot not ready yet" error |
| **Current Behavior** | `ListChannelsAsync` uses the standard RPC timeout. If the request routes to a non-master node, it times out with a generic `KubeMQTimeoutException`. The `"cluster snapshot not ready yet"` error is not explicitly detected or retried. |
| **Expected Behavior** | SDK should handle timeouts gracefully (perhaps with automatic retry) and provide a clear message when the cluster snapshot is not ready |
| **Workaround** | Callers can retry on timeout or error |
| **File** | `src/KubeMQ.Sdk/Client/KubeMQClient.cs` (line 1222) |
| **Fix Complexity** | Low — add retry with backoff specific to this operation, detect the error message |

---

## 4. Minor Gaps (Polish)

### GAP-010: maxReceiveSize Default Differs from Spec

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §1.1 — default 4MB (4194304) |
| **Current Behavior** | Default is 100MB (`100 * 1024 * 1024`) |
| **Rationale** | The spec notes: "programmatic default without config file is 100MB, but deployed servers default to 4MB via TOML config." The SDK follows the programmatic default. |
| **Recommendation** | Document the discrepancy; no code change needed if the intent is to match the programmatic default |

---

### GAP-011: ClientID Optional vs Spec Required

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §1.1 — `clientId` is Required |
| **Current Behavior** | `ClientId` is optional; auto-generated from `"{hostname}-{PID}-{random4}"` if not provided |
| **Rationale** | Pragmatic design choice: reduces boilerplate for simple usage. The generated ID is still unique per client instance. |
| **Recommendation** | Acceptable deviation — document the auto-generation behavior |

---

### GAP-012: RequestID/MessageID Not User-Overridable

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §6.1, §7.1, §4.1 — "Auto-generates UUID if empty" (implies user can provide) |
| **Current Behavior** | `RequestID` and `MessageID` are always auto-generated; there is no field on `CommandMessage`, `QueryMessage`, or `QueueMessage` for the user to supply their own ID. `EventID` does allow user override. |
| **Recommendation** | Add optional `RequestId`/`MessageId` properties to message types for consistency with `EventID` behavior |

---

### GAP-013: Downstream Request Metadata Field Not Exposed

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §4.2 — `QueuesDownstreamRequest.Metadata` and `QueuesDownstreamResponse.Metadata` |
| **Current Behavior** | The `Metadata` map on downstream request/response is not exposed in the SDK's public API. The proto field exists but the SDK does not populate it on requests or surface it from responses. |
| **Recommendation** | Add optional `Metadata` parameter to downstream operations |

---

### GAP-014: ~~Upstream Result Reference Fields Not Exposed~~ (Resolved)

**Status:** Resolved in v1.4.0 proto update. The `RefChannel` field was removed from `SendQueueMessageResult` in the v1.4.0 proto. The `QueuesInfo` RPC was also removed. This gap is no longer applicable.

---

### GAP-015: CloseByClient Not Explicitly Sent

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §4.2 — `CloseByClient` (10) request type |
| **Current Behavior** | Stream disposal does not explicitly send a `CloseByClient` request before closing the gRPC stream. The gRPC framework handles stream termination. |
| **Recommendation** | Send `CloseByClient` before disposing to give the server a clean signal |

---

### GAP-016: Missing Example — StartFromFirst Dedicated Example

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §13 — Code Examples |
| **Current Behavior** | `StartFromFirst` is covered within `EventsStore.PersistentPubSub` but has no standalone example like the other start positions |
| **Recommendation** | Add `EventsStore.StartFromFirst` example for consistency |

---

### GAP-017: Missing Example — Command Timeout Handling

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §13 — Code Examples — "Handle command timeout" |
| **Current Behavior** | Timeout is configured as a parameter in `Commands.SendCommand`, but there's no example showing how to catch and handle a timeout gracefully |
| **Recommendation** | Add `Commands.HandleTimeout` example with try/catch for `KubeMQTimeoutException` |

---

### GAP-018: Missing Example — List Channels with Search Filter

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §13 — Code Examples — "List channels with search filter" |
| **Current Behavior** | The `channel_search` tag is supported in the API but no example demonstrates its usage with a regex pattern |
| **Recommendation** | Add a filter example to `Config.ChannelManagement` or create `Config.ChannelSearch` |

---

### GAP-019: Poll-Received Messages Cannot Settle

| Attribute | Value |
|-----------|-------|
| **Severity** | Minor |
| **Spec Reference** | §4.4 — Poll mode |
| **Current Behavior** | Messages received via `PollQueueAsync` have their settlement delegates (`ackFunc`, `rejectFunc`, `requeueFunc`) set to `null`. Calling `AckAsync()`, `RejectAsync()`, or `RequeueAsync()` on these messages throws `InvalidOperationException`. |
| **Expected Behavior** | Poll-mode messages should either support settlement (by keeping a downstream stream open) or the SDK should document that poll messages are auto-acked/cannot be individually settled |
| **Workaround** | Use `autoAck: true` in the poll request, or use `ReceiveQueueDownstreamAsync` instead |
| **File** | `src/KubeMQ.Sdk/Client/KubeMQClient.cs` (lines 1770–1803) |

---

## 5. Design Deviations

These are intentional architectural choices that differ from the spec but are well-reasoned.

| Deviation | Spec Says | SDK Does | Justification |
|-----------|-----------|----------|---------------|
| `ClientId` required | Required parameter | Optional with auto-generation | Reduces boilerplate; generated ID is unique per instance |
| `maxReceiveSize` default | 4MB | 100MB | Matches the gRPC programmatic default (spec notes this) |
| Timeout unit | Timeout in milliseconds | User provides in seconds, SDK converts to ms | Better DX — seconds are more natural for human-facing APIs |
| `RequestID` user-settable | Auto-generated "if empty" | Always auto-generated | Simplifies the API surface; no need for user to manage request IDs |
| Subscription pattern | Callback/handler | `IAsyncEnumerable<T>` | Idiomatic modern C# — supports `await foreach`, LINQ, and `CancellationToken` |
| Auth token | Static string | `ICredentialProvider` with refresh | More robust — supports token expiry, rotation, and dynamic providers |
| Reconnect | Fixed interval | Exponential backoff + jitter + buffer | Production-grade reconnection that avoids thundering herd |

---

## 6. Gap-by-Category Matrix

| # | Gap ID | Category | Severity | Spec Section | Feature |
|---|--------|----------|----------|:------------:|---------|
| 1 | GAP-001 | Validation | Critical | §10.1 | Metadata/Body presence check |
| 2 | GAP-002 | Queues-Stream | Critical | §4.3 | CloseByServer handling |
| 3 | GAP-003 | Queues-Simple | Critical | §5.3 | ReceiveQueueMessages public API |
| 4 | GAP-004 | Events | Moderate | §2.2, §3.2 | Publish stream auto-reconnect |
| 5 | GAP-005 | Queues-Stream | Moderate | §4.1, §4.2 | Queue stream auto-reconnect |
| 6 | GAP-006 | Queues-Stream | Moderate | §4.2 | ActiveOffsets/TransactionStatus |
| 7 | GAP-007 | RPC | Moderate | §6.1, §7.1 | Span field for trace propagation |
| 8 | GAP-008 | Queues-Simple | Moderate | §5.2 | Batch per-message results |
| 9 | GAP-009 | Management | Moderate | §8.2.3 | List channels timeout/snapshot |
| 10 | GAP-010 | Connection | Minor | §1.1 | maxReceiveSize default |
| 11 | GAP-011 | Connection | Minor | §1.1 | ClientID optional |
| 12 | GAP-012 | ID Generation | Minor | §6.1, §4.1 | RequestID/MessageID override |
| 13 | GAP-013 | Queues-Stream | Minor | §4.2 | Downstream metadata field |
| 14 | GAP-014 | Queues-Stream | ~~Minor~~ | §4.1 | ~~Upstream result ref fields~~ (Resolved — removed in v1.4.0 proto) |
| 15 | GAP-015 | Queues-Stream | Minor | §4.2 | CloseByClient not sent |
| 16 | GAP-016 | Examples | Minor | §13 | StartFromFirst example |
| 17 | GAP-017 | Examples | Minor | §13 | Command timeout example |
| 18 | GAP-018 | Examples | Minor | §13 | List channels filter example |
| 19 | GAP-019 | Queues-Stream | Minor | §4.4 | Poll-received message settlement |

---

## 7. Detailed Gap Descriptions

### By Category

#### Connection & Client Lifecycle
**Status: 12/12 fully implemented (100%)**

No functional gaps. Two minor deviations (ClientID optional, maxReceiveSize default) are documented design choices.

#### Events (Pub/Sub)
**Status: 15/16 implemented (94%)**

| Gap | Item | Description |
|-----|------|-------------|
| GAP-004 | Event stream auto-reconnect | `EventStream` does not auto-reconnect. The `WithReconnect` pattern is only used for subscriptions, not for publish streams. |

#### Events Store
**Status: 14/15 implemented (93%)**

| Gap | Item | Description |
|-----|------|-------------|
| GAP-004 | EventStore stream auto-reconnect | `EventStoreStream` does not auto-reconnect. The correlation dictionary (`ConcurrentDictionary<eventId, TCS>`) would need special handling on reconnect to fault pending operations. |

#### Queues — Stream API
**Status: 24/33 fully + 3 partial (73% full, 82% including partial)**

This is the area with the most gaps:

| Gap | Item | Description |
|-----|------|-------------|
| GAP-002 | CloseByServer handling | No detection of server-initiated close on downstream streams |
| GAP-005 | Auto-reconnect | Neither upstream nor downstream queue streams reconnect automatically |
| GAP-006 | ActiveOffsets/TransactionStatus | Not exposed as public API methods |
| GAP-013 | Metadata field | Not mapped on downstream request/response |
| ~~GAP-014~~ | ~~Ref fields on upstream result~~ | Resolved — `RefChannel` removed in v1.4.0 proto |
| GAP-015 | CloseByClient | Not explicitly sent before stream disposal |
| GAP-019 | Poll message settlement | Messages from PollQueueAsync cannot be individually settled |

#### Queues — Simple API
**Status: 7/10 fully + 3 partial (70% full, 100% including partial)**

| Gap | Item | Description |
|-----|------|-------------|
| GAP-003 | ReceiveQueueMessages public API | Transport exists, no public method |
| GAP-008 | Batch per-message results | Aggregate result only, individual per-message results lost |

#### RPC — Commands
**Status: 10/12 implemented (83%)**

| Gap | Item | Description |
|-----|------|-------------|
| GAP-007 | Span field | gRPC `Request.Span` not populated; Activity-based tracing only |

#### RPC — Queries
**Status: 11/11 implemented (100%)**

No gaps. Cache support is fully implemented with `CacheKey`, `CacheTTL`, and `CacheHit`.

#### Channel Management
**Status: 11/12 fully + 1 partial (92% full, 96% partial)**

| Gap | Item | Description |
|-----|------|-------------|
| GAP-009 | ListChannels timeout/snapshot | No special handling for non-master node timeout or "cluster snapshot not ready yet" |

#### Validation
**Status: 13/14 implemented (93%)**

| Gap | Item | Description |
|-----|------|-------------|
| GAP-001 | Metadata/Body presence | Not validated in any message type's validator |

#### Error Handling
**Status: 6/6 implemented (100%)**

Comprehensive implementation with typed exception hierarchy and full gRPC status code mapping.

#### ID Auto-Generation
**Status: 3/3 implemented (100%)**

All IDs auto-generated. EventID allows user override; RequestID/MessageID always auto-generated.

#### Deprecated API Removal
**Status: 5/5 clean (100%)**

No traces of legacy `StreamQueueMessage` API found in the codebase.

---

## 8. Remediation Priority

### Phase 1: Critical (Blocking — Fix Before Release)

| Priority | Gap ID | Effort | Description |
|:--------:|--------|:------:|-------------|
| P0 | GAP-001 | Low | Add Metadata/Body presence validation to all validators |
| P0 | GAP-002 | Medium | Handle CloseByServer in downstream streams |
| P0 | GAP-003 | Low | Expose `ReceiveQueueMessagesAsync` as public API |

**Estimated effort: 1–2 days**

### Phase 2: Important (Track for Next Release)

| Priority | Gap ID | Effort | Description |
|:--------:|--------|:------:|-------------|
| P1 | GAP-006 | Low | Expose ActiveOffsets and TransactionStatus methods |
| P1 | GAP-008 | Low-Med | Expose per-message results in batch send |
| P1 | GAP-009 | Low | Handle list-channels timeout with retry |
| P1 | GAP-012 | Low | Add optional RequestId/MessageId to message types |
| P1 | GAP-015 | Low | Send CloseByClient before stream disposal |

**Estimated effort: 2–3 days**

### Phase 3: Enhancements (Backlog)

| Priority | Gap ID | Effort | Description |
|:--------:|--------|:------:|-------------|
| P2 | GAP-004 | High | Publish stream auto-reconnect (Events/EventsStore) |
| P2 | GAP-005 | High | Queue stream auto-reconnect |
| P2 | GAP-007 | Medium | Span field W3C TraceContext serialization |
| P2 | GAP-013 | Low | Expose downstream metadata field |
| ~~P2~~ | ~~GAP-014~~ | ~~Low~~ | ~~Map upstream result reference fields~~ (Resolved — removed in v1.4.0 proto) |
| P2 | GAP-019 | Medium | Poll-received message settlement support |

**Estimated effort: 5–7 days**

### Phase 4: Polish (Examples & Documentation)

| Priority | Gap ID | Effort | Description |
|:--------:|--------|:------:|-------------|
| P3 | GAP-016 | Low | Add StartFromFirst dedicated example |
| P3 | GAP-017 | Low | Add command timeout handling example |
| P3 | GAP-018 | Low | Add list channels with filter example |

**Estimated effort: 0.5 day**

---

### Total Remediation Estimate

| Phase | Effort | Gap Count |
|-------|:------:|:---------:|
| Phase 1 (Critical) | 1–2 days | 3 |
| Phase 2 (Important) | 2–3 days | 5 |
| Phase 3 (Enhancements) | 5–7 days | 6 |
| Phase 4 (Polish) | 0.5 day | 3 |
| **Total** | **8.5–12.5 days** | **17** |

Note: GAP-010 (maxReceiveSize default) and GAP-011 (ClientID optional) are documented design deviations, not action items.
