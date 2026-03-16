# KubeMQ Client SDK Assessment Framework (V2)

## Purpose

This document is a comprehensive assessment framework designed to be loaded into a Claude Code AI agent that will perform a deep assessment of each KubeMQ client SDK codebase. The agent will clone each SDK repository, analyze the source code, documentation, tests, packaging, and developer experience, then produce a scored report with gap analysis and prioritized remediation roadmap.

## Target SDKs

### Core SDKs (5 — assessed with full framework)

| SDK | Repository | Language |
|-----|-----------|----------|
| Go | `github.com/kubemq-io/kubemq-go` | Go |
| Java | `github.com/kubemq-io/kubemq-java-v2` | Java |
| C# / .NET | `github.com/kubemq-io/kubemq-CSharp` | C# |
| Python | `github.com/kubemq-io/kubemq-Python` | Python |
| Node.js / TypeScript | `github.com/kubemq-io/kubemq-js` | TypeScript |

### Spring Boot SDK (assessed as Java sub-report)

| SDK | Repository | Language |
|-----|-----------|----------|
| Spring Boot | `github.com/kubemq-io/kubemq-springboot` | Java |

The Spring Boot SDK is an integration layer over the Java SDK, not a standalone language SDK. It is assessed separately using a reduced rubric focused on: Spring auto-configuration, starter dependency quality, properties binding, Spring conventions compliance, and Spring-specific documentation. Its scores do **not** appear in the core cross-SDK parity matrix.

### SDK Cookbooks (assessed as part of parent SDK's Documentation category — Section 10.3)

| Cookbook | Repository |
|---------|-----------|
| Go Cookbook | `github.com/kubemq-io/go-sdk-cookbook` |
| Java Cookbook | `github.com/kubemq-io/java-sdk-cookbook` |
| C# Cookbook | `github.com/kubemq-io/csharp-sdk-cookbook` |
| Python Cookbook | `github.com/kubemq-io/python-sdk-cookbook` |
| Node Cookbook | `github.com/kubemq-io/node-sdk-cookbook` |

### Assessment Order

**Assess Java v2 first.** Since it is the most mature SDK, it serves as the internal benchmark. Use its scores as the reference point when calibrating scores for other SDKs. This grounds relative scoring and ensures consistency across assessments.

---

## Assessment Overview

### Scoring System

The framework uses **two scoring scales** depending on the criterion type:

#### Feature Criteria (Binary — does it exist?)

Used for API Completeness (Category 1) feature checks and other existence-based criteria.

| Score | Label | Definition |
|-------|-------|------------|
| **0** | Missing | Feature does not exist in the SDK |
| **1** | Partial | Feature exists but is incomplete, buggy, or non-idiomatic |
| **2** | Complete | Feature is fully implemented and works correctly |

**Score normalization for weighted rollup:** Category 1 tables display raw 0/1/2 scores (clearest for feature parity reading). For the category-level score and weighted overall calculation, map: **0→1, 1→3, 2→5** on the 1-5 scale. This ensures Category 1 is directly comparable to all other categories in the final report.

#### Quality Criteria (1-5 Scale — how well is it done?)

Used for all other categories evaluating implementation quality, maturity, and best-practice adherence.

| Score | Label | Definition |
|-------|-------|------------|
| **1** | Absent or broken | Feature missing entirely or fundamentally broken. Blocks production use. |
| **2** | Present but not production-safe | Feature exists but has serious deficiencies. Unsafe for production workloads. |
| **3** | Production-usable with clear gaps | Feature works for basic scenarios but has known limitations or inconsistencies. |
| **4** | Strong and consistent | Feature is well-implemented, follows best practices, minor gaps only. |
| **5** | Benchmarked and verified | Feature meets industry best practices, documented, verified against named references (e.g., NATS, Kafka, Azure SDK). |

#### N/A Handling

Some criteria genuinely don't apply to certain languages (e.g., GIL awareness for TypeScript, CompletableFuture for Go). Mark these **N/A** and **exclude them from the category average denominator**. The agent must justify every N/A with a reason.

#### "Not Assessable" Handling

"Not assessable" is **different from N/A**. N/A means the criterion does not apply to this language — it is excluded from the denominator. "Not assessable" means the criterion applies but the agent could not verify it — it is **excluded from the score average** (like N/A) but is **separately tracked and listed** in the report as requiring manual verification. The flagging mechanism is the forcing function for follow-up, not the score math. This avoids penalizing the SDK for the agent's evidence limitations while still creating a clear list of items needing human attention.

#### Confidence Field

Every scored criterion must include a **confidence level**:

| Confidence | Definition |
|------------|------------|
| **Verified by runtime** | Agent built/ran code, tests, or tools to confirm |
| **Verified by source** | Agent read the source code and confirmed by inspection |
| **Inferred** | Agent inferred from indirect evidence (e.g., dependency presence, code patterns) |
| **Not assessable** | Agent could not determine; excluded from score average but listed separately for manual verification (see "Not Assessable" handling above) |

### Evidence Requirements

For every scored criterion, the agent must provide:
1. **Specific file paths and line numbers** referenced
2. **Code snippet** showing the best or worst example found
3. **Comparison to expected pattern** — what the code does vs. what industry best practice expects

**Scores without evidence are not accepted.**

### Assessment Categories (Weighted)

The assessment uses **13 weighted categories**. The final SDK score is a weighted average.

**Gating Rules:**
- **Quality gate:** If any of the four Critical-tier categories (API Completeness, Connection & Transport, Error Handling, or Auth & Security) has a normalized score below **3.0**, the overall SDK score is **capped at 3.0** regardless of other category scores.
- **Feature parity gate (Category 1 only):** If more than 25% of applicable Category 1 feature criteria score **0 (Missing)**, the overall SDK score is **capped at 2.0** regardless of other scores. This prevents an SDK with major feature gaps from passing the quality gate through partial implementations alone.

These gates prevent cosmetic strengths from masking production-blocking gaps.

| # | Category | Weight | Tier | What It Covers |
|---|----------|--------|------|---------------|
| 1 | API Completeness & Feature Parity | 14% | Critical | All 4 messaging patterns, all operations, operational semantics |
| 2 | API Design & Developer Experience | 9% | High | Idiomaticity, ergonomics, boilerplate, developer journey |
| 3 | Connection & Transport | 11% | Critical | gRPC usage, connection lifecycle, TLS/mTLS, Kubernetes-native, flow control |
| 4 | Error Handling & Resilience | 11% | Critical | Error types, retry/backoff, error messages, throttle handling |
| 5 | Authentication & Security | 9% | Critical | JWT, mTLS, OIDC, secure defaults |
| 6 | Concurrency & Thread Safety | 7% | High | Goroutine/thread safety, async patterns |
| 7 | Observability | 5% | Standard | Logging, metrics, tracing hooks |
| 8 | Code Quality & Architecture | 6% | High | Internal structure, maintainability, technical debt, serialization |
| 9 | Testing | 9% | High | Unit tests, integration tests, CI pipeline |
| 10 | Documentation | 7% | High | API reference, guides, examples, troubleshooting |
| 11 | Packaging & Distribution | 4% | Standard | Package manager, versioning, releases, dependencies |
| 12 | Compatibility, Lifecycle & Supply Chain | 4% | Standard | Server version matrix, deprecation policy, SBOM, runtime support, maintainer health |
| 13 | Performance | 4% | Standard | Benchmark infrastructure, optimization patterns, resource efficiency |

Weights sum to 100%. The weighted score is calculated as: `Σ(weight_i × category_score_i)`.

**Dual Score Output:** Reports must show both the **weighted score** (production readiness) and the **unweighted average** (overall maturity). Stakeholders choose which to prioritize.

### Server Capabilities as Source of Truth

The KubeMQ server's protobuf definitions (`github.com/kubemq-io/protobuf`) are the source of truth for which features exist. Do not score SDK features that the server does not support — mark them N/A. The agent must verify the server's proto definitions before scoring Category 1.

---

## Category 1: API Completeness & Feature Parity

**Goal:** Verify the SDK implements all four KubeMQ messaging patterns with all required operations, plus correct operational semantics.

**Scoring:** Feature criteria use the **0/1/2 (Missing/Partial/Complete)** scale.

**Important:** Before scoring, check the server's protobuf definitions to confirm which operations are actually supported. Features not supported by the server are marked N/A.

### 1.1 Events (Pub/Sub)

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.1.1 | **Publish single event** — Can send a single event to a channel | | | |
| 1.1.2 | **Subscribe to events** — Can subscribe to a channel and receive events via callback/handler | | | |
| 1.1.3 | **Event metadata** — Supports setting Channel, ClientId, Metadata (string), Body (bytes), Tags (key-value map) | | | |
| 1.1.4 | **Wildcard subscriptions** — Supports channel wildcard patterns (if server supports) | | | |
| 1.1.5 | **Multiple subscriptions** — Client can subscribe to multiple channels simultaneously | | | |
| 1.1.6 | **Unsubscribe** — Can cleanly unsubscribe from a channel | | | |
| 1.1.7 | **Group-based subscriptions** — Supports consumer group / load-balanced subscriptions | | | |

### 1.2 Events Store (Persistent Pub/Sub)

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.2.1 | **Publish to events store** — Can send a persistent event to an events store channel | | | |
| 1.2.2 | **Subscribe to events store** — Can subscribe with delivery options | | | |
| 1.2.3 | **StartFromNew** — Subscribe to receive only new events from subscription time | | | |
| 1.2.4 | **StartFromFirst** — Subscribe to receive all events from the beginning | | | |
| 1.2.5 | **StartFromLast** — Subscribe starting from the last event | | | |
| 1.2.6 | **StartFromSequence** — Subscribe starting from a specific sequence number | | | |
| 1.2.7 | **StartFromTime** — Subscribe starting from a specific timestamp | | | |
| 1.2.8 | **StartFromTimeDelta** — Subscribe starting from a time offset (e.g., last 5 minutes) | | | |
| 1.2.9 | **Event store metadata** — Supports same metadata as events (Channel, ClientId, Metadata, Body, Tags) | | | |

### 1.3 Queues

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.3.1 | **Send single message** — Can send a message to a queue channel | | | |
| 1.3.2 | **Send batch messages** — Can send multiple messages in a single call | | | |
| 1.3.3 | **Receive/Pull messages** — Can pull messages from a queue (auto-ack) | | | |
| 1.3.4 | **Receive with visibility timeout** — Can pull messages with a visibility/wait timeout | | | |
| 1.3.5 | **Message acknowledgment** — Supports explicit ack/reject of individual messages | | | |
| 1.3.6 | **Queue stream / transaction** — Supports streaming queue operations (downstream pull with transaction control) | | | |
| 1.3.7 | **Delayed messages** — Supports sending messages with a delivery delay | | | |
| 1.3.8 | **Message expiration** — Supports setting message TTL/expiration | | | |
| 1.3.9 | **Dead letter queue** — Supports configuring max receive count and DLQ channel | | | |
| 1.3.10 | **Queue message metadata** — Channel, ClientId, Metadata, Body, Tags, Policy (MaxReceiveCount, MaxReceiveQueue, DelaySeconds, ExpirationSeconds) | | | |
| 1.3.11 | **Peek messages** — Can peek at messages without consuming them (if server supports) | | | |
| 1.3.12 | **Purge queue** — Can purge all messages from a queue (if server supports) | | | |

### 1.4 RPC (Commands & Queries)

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.4.1 | **Send command** — Can send a command (fire-and-wait for execution confirmation) | | | |
| 1.4.2 | **Subscribe to commands** — Can register as a command handler/responder | | | |
| 1.4.3 | **Command response** — Handler can send back an execution response (success/error) | | | |
| 1.4.4 | **Command timeout** — Supports configurable timeout for command execution | | | |
| 1.4.5 | **Send query** — Can send a query (request-response with data return) | | | |
| 1.4.6 | **Subscribe to queries** — Can register as a query handler/responder | | | |
| 1.4.7 | **Query response** — Handler can send back response data | | | |
| 1.4.8 | **Query timeout** — Supports configurable timeout for query response | | | |
| 1.4.9 | **RPC metadata** — Channel, ClientId, Metadata, Body, Tags, Timeout | | | |
| 1.4.10 | **Group-based RPC** — Supports load-balanced command/query handlers via groups | | | |
| 1.4.11 | **Cache support for queries** — Supports query response caching (CacheKey, CacheTTL) | | | |

### 1.5 Client Management

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.5.1 | **Ping** — Can ping the server to check connectivity | | | |
| 1.5.2 | **Server info** — Can retrieve server information (version, uptime, etc.) | | | |
| 1.5.3 | **Channel listing** — Can list available channels (by type: events, events store, queues, commands, queries) | | | |
| 1.5.4 | **Channel create** — Can create a channel | | | |
| 1.5.5 | **Channel delete** — Can delete a channel | | | |

> **Note:** Create/connect and Close/disconnect are evaluated under Category 3 (Connection Lifecycle) to avoid duplicate scoring.

### 1.6 Operational Semantics

**Goal:** Verify the SDK correctly handles the behavioral guarantees that enterprise users depend on.

| # | Criterion | Score (0-2) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 1.6.1 | **Message ordering** — SDK preserves message ordering guarantees as defined by the server (per-channel FIFO for queues) | | | |
| 1.6.2 | **Duplicate handling** — SDK behavior on duplicate messages is defined and documented (at-least-once vs. exactly-once semantics) | | | |
| 1.6.3 | **Large message handling** — SDK handles messages up to gRPC max size without corruption or silent truncation | | | |
| 1.6.4 | **Empty/null payload** — SDK correctly handles empty body, empty metadata, and empty tags | | | |
| 1.6.5 | **Special characters** — SDK correctly handles Unicode, binary data, and special characters in metadata and tags | | | |

### 1.7 Cross-SDK Feature Parity Matrix

After assessing all SDKs, produce a feature parity matrix:

```
| Feature              | Go | Java | C# | Python | Node/TS |
|----------------------|----|------|----|--------|---------|
| Events Publish       | ✅  | ✅    | ✅  | ✅      | ✅       |
| Events Subscribe     | ✅  | ✅    | ❌  | ✅      | ⚠️      |
| ...                  |    |      |    |        |         |
```

Legend: ✅ = Complete (2), ⚠️ = Partial (1), ❌ = Missing (0), N/A = Not applicable

---

## Category 2: API Design & Developer Experience

**Goal:** Evaluate how idiomatic, ergonomic, and developer-friendly the SDK API is for its target language.

**Benchmark:** Azure SDK Design Principles — Idiomatic, Consistent, Approachable, Diagnosable, Compatible.

### 2.1 Language Idiomaticity

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.1.1 | **Naming conventions** — Follows language-standard naming (camelCase for Java/JS, PascalCase for C#, snake_case for Python, exported PascalCase for Go) | | | |
| 2.1.2 | **Configuration pattern** — Uses language-idiomatic config: Builder (Java/C#), Functional Options (Go), kwargs/dataclasses (Python), Options object (TS) | | | |
| 2.1.3 | **Error handling pattern** — Uses language-standard error handling: exceptions (Java/C#/Python), error returns (Go), Promise rejection/try-catch (TS) | | | |
| 2.1.4 | **Async pattern** — Uses appropriate async model: CompletableFuture (Java), async/await (C#/Python/TS), goroutines+channels (Go) | | | |
| 2.1.5 | **Resource cleanup** — Supports language-standard cleanup: AutoCloseable/try-with-resources (Java), IDisposable/using (C#), context managers (Python), defer (Go), finally (TS) | | | |
| 2.1.6 | **Collection types** — Uses native collection types, not custom wrappers | | | |
| 2.1.7 | **Null/optional handling** — Handles nullability idiomatically: Optional (Java), Nullable references (C#), None (Python), undefined (TS) | | | |

### 2.2 Progressive Disclosure & Minimal Boilerplate

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.2.1 | **Quick start simplicity** — A basic publish-subscribe can be done in ≤10 lines of code (excluding imports) | | | |
| 2.2.2 | **Sensible defaults** — Client works with minimal config (just address and optionally auth token) | | | |
| 2.2.3 | **Opt-in complexity** — Advanced features (TLS, auth, retry, timeouts) are additive, not required | | | |
| 2.2.4 | **Consistent method signatures** — Similar operations across patterns have consistent signatures | | | |
| 2.2.5 | **Discoverability** — All public types and methods have doc comments; method names are predictable and consistent; type definitions are properly exported (e.g., TypeScript `.d.ts` files exist) | | | |

### 2.3 Type Safety & Generics

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.3.1 | **Strong typing** — Message types, options, results are all strongly typed (no `interface{}/any/Object` abuse) | | | |
| 2.3.2 | **Enum/constant usage** — Uses enums or typed constants for subscription types, error codes, etc. | | | |
| 2.3.3 | **Return types** — Methods return specific result types, not generic maps or untyped containers | | | |

### 2.4 API Consistency

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 2.4.1 | **Internal consistency** — All operations within the SDK follow the same patterns and conventions | | | |
| 2.4.2 | **Cross-SDK concept alignment** — Core concepts (Client, Event, QueueMessage, Command, Query, Subscription) have equivalent names/structures across SDKs | | | |
| 2.4.3 | **Method naming alignment** — Operations map to equivalent method names across SDKs (e.g., `sendEvent` in all SDKs, adapted for case conventions) | | | |
| 2.4.4 | **Option/config alignment** — Configuration fields use consistent names across SDKs | | | |

### 2.5 Developer Journey Walkthrough

**Goal:** Assess the full developer journey end-to-end, not just individual API criteria. Walk through each step and document friction points, blockers, and time-to-value.

| Step | Assessment | Friction Points |
|------|-----------|-----------------|
| **1. Install** — Add SDK as dependency via package manager | | |
| **2. Connect** — Create client and connect to KubeMQ server | | |
| **3. First Publish** — Send first event/message | | |
| **4. First Subscribe** — Receive first event/message | | |
| **5. Error Handling** — Handle a common error (bad address, auth failure) | | |
| **6. Production Config** — Configure for production (TLS, auth, timeouts, reconnection) | | |
| **7. Troubleshooting** — Debug a common issue using docs and error messages | | |

Rate the overall developer journey: Score (1-5), with notes on the most significant friction point.

---

## Category 3: Connection & Transport

**Goal:** Evaluate gRPC usage, connection management, TLS support, Kubernetes-native behavior, and flow control.

**Benchmark:** NATS client reconnection model; Kafka librdkafka transport management; gRPC reference implementations (`grpc-go`, `grpc-java`, `grpc-dotnet`, `grpc-js`, `grpc.aio`).

### 3.1 gRPC Implementation

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.1.1 | **gRPC client setup** — Properly creates gRPC channel/connection with configurable options | | | |
| 3.1.2 | **Protobuf alignment** — Uses correct protobuf definitions matching the server's proto files | | | |
| 3.1.3 | **Proto version** — Uses the latest version of kubemq protobuf definitions | | | |
| 3.1.4 | **Streaming support** — Properly uses gRPC bidirectional/server streaming for subscriptions and queue streams | | | |
| 3.1.5 | **Metadata passing** — Correctly passes auth tokens, client ID, and other metadata in gRPC headers | | | |
| 3.1.6 | **Keepalive** — Configures gRPC keepalive to detect dead connections | | | |
| 3.1.7 | **Max message size** — Configures appropriate max send/receive message sizes | | | |
| 3.1.8 | **Compression** — Supports gRPC compression (gzip) | | | |

### 3.2 Connection Lifecycle

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.2.1 | **Connect** — Clean connection establishment with proper error reporting | | | |
| 3.2.2 | **Disconnect/close** — Graceful shutdown: drains in-flight messages, closes streams, releases resources | | | |
| 3.2.3 | **Auto-reconnection** — Automatically reconnects on connection loss | | | |
| 3.2.4 | **Reconnection backoff** — Uses exponential backoff with jitter for reconnection attempts | | | |
| 3.2.5 | **Connection state events** — Exposes connection state changes (connected, disconnected, reconnecting) via callbacks/events | | | |
| 3.2.6 | **Subscription recovery** — Reestablishes subscriptions after reconnection | | | |
| 3.2.7 | **Message buffering during reconnect** — Buffers outgoing messages during brief disconnections | | | |
| 3.2.8 | **Connection timeout** — Configurable connection timeout with sensible default (10-30s) | | | |
| 3.2.9 | **Request timeout** — Configurable per-request/operation timeout | | | |

### 3.3 TLS / mTLS

> **Note:** mTLS is scored here (transport capability). Category 5 references this score for authentication use of client certificates. No duplicate scoring.

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.3.1 | **TLS support** — Can connect using TLS-encrypted gRPC channel | | | |
| 3.3.2 | **Custom CA certificate** — Supports providing custom CA certificate for self-signed/internal CAs | | | |
| 3.3.3 | **mTLS support** — Supports mutual TLS with client certificate and key | | | |
| 3.3.4 | **TLS configuration** — Configurable TLS version, cipher suites (where language supports it) | | | |
| 3.3.5 | **Insecure mode** — Supports insecure/plaintext connection for development (with clear warnings) | | | |

### 3.4 Kubernetes-Native Behavior

**Goal:** KubeMQ's headline is "native for Kubernetes." These criteria evaluate whether the SDK makes Kubernetes deployment seamless.

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.4.1 | **K8s DNS service discovery** — SDK works seamlessly with Kubernetes service DNS names; default address/port align with KubeMQ K8s deployment patterns (sidecar: `localhost:50000`, standalone: DNS); documentation covers K8s service discovery | | | |
| 3.4.2 | **Graceful shutdown APIs** — Client provides explicit close/drain/shutdown APIs with idempotent behavior; documentation and examples show how to integrate with SIGTERM and Kubernetes termination lifecycle | | | |
| 3.4.3 | **Health/readiness integration** — Client exposes connection state for use in Kubernetes health/readiness probes (e.g., `IsConnected()` method) | | | |
| 3.4.4 | **Rolling update resilience** — Client handles server pod restarts (rolling updates) via reconnection without message loss | | | |
| 3.4.5 | **Sidecar vs. standalone** — Documentation covers both sidecar (localhost) and standalone (DNS) deployment patterns | | | |

### 3.5 Flow Control & Backpressure

**Goal:** Evaluate how the SDK handles high-throughput publishing and slow consumers.

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 3.5.1 | **Publisher flow control** — SDK provides configurable behavior when outbound buffer is full (block, drop, or error) | | | |
| 3.5.2 | **Consumer flow control** — SDK supports configurable prefetch/buffer size for consumers | | | |
| 3.5.3 | **Throttle detection** — SDK detects server-side throttling/rate-limiting and backs off appropriately | | | |
| 3.5.4 | **Throttle error surfacing** — Throttling is surfaced to the user with clear error messages and suggestions | | | |

---

## Category 4: Error Handling & Resilience

**Goal:** Evaluate error classification, message quality, retry strategies, and resilience patterns.

**Benchmark:** AWS SDK error classification; Auth0 error message principles.

### 4.1 Error Classification & Types

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.1.1 | **Typed errors** — Uses specific error types/classes, not generic strings or error codes | | | |
| 4.1.2 | **Error hierarchy** — Errors organized into categories: ConnectionError, AuthenticationError, TimeoutError, ServerError, ValidationError | | | |
| 4.1.3 | **Retryable classification** — Errors are classified as retryable vs. non-retryable | | | |
| 4.1.4 | **gRPC status mapping** — Properly maps gRPC status codes to SDK-specific error types | | | |
| 4.1.5 | **Error wrapping/chaining** — Wraps underlying errors with context while preserving the original cause | | | |

### 4.2 Error Message Quality

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.2.1 | **Actionable messages** — Error messages explain what happened AND suggest how to fix it | | | |
| 4.2.2 | **Context inclusion** — Error messages include relevant context (channel name, operation, client ID) | | | |
| 4.2.3 | **No swallowed errors** — Errors are never silently swallowed; all failures are reported | | | |
| 4.2.4 | **Consistent format** — All error messages follow a consistent format/template | | | |

### 4.3 Retry & Backoff

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.3.1 | **Automatic retry** — Transient errors are automatically retried | | | |
| 4.3.2 | **Exponential backoff** — Retry uses exponential backoff with jitter: `min(base * 2^attempt, maxDelay) + jitter` | | | |
| 4.3.3 | **Configurable retry** — Max retries, base delay, max delay, and jitter are configurable | | | |
| 4.3.4 | **Retry exhaustion** — Clear error when retries are exhausted, including total attempt count and duration | | | |
| 4.3.5 | **Non-retryable bypass** — Non-retryable errors (auth failure, validation) skip retry immediately | | | |

### 4.4 Resilience Patterns

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 4.4.1 | **Timeout on all operations** — Every external call has a timeout (connection, request, stream) | | | |
| 4.4.2 | **Cancellation support** — Operations can be cancelled (Context in Go, CancellationToken in C#, AbortController in TS) | | | |
| 4.4.3 | **Graceful degradation** — SDK handles partial failures without crashing. Specifically: server returns error for one message in a batch (others still succeed), one subscription fails while others continue, network drops mid-stream (stream is recoverable) | | | |
| 4.4.4 | **Resource leak prevention** — All resources (connections, streams, goroutines/threads) are properly cleaned up on error paths, not just happy paths | | | |

---

## Category 5: Authentication & Security

**Goal:** Evaluate authentication method support, secure defaults, and security best practices.

**Benchmark:** Kafka SASL/mTLS; Azure AD RBAC; NATS NKey/JWT.

### 5.1 Authentication Methods

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 5.1.1 | **JWT token auth** — Supports passing JWT auth token for authentication | | | |
| 5.1.2 | **Token refresh** — Supports refreshing/rotating auth tokens without reconnection | | | |
| 5.1.3 | **OIDC integration** — Supports OpenID Connect token acquisition and refresh | | | |
| 5.1.4 | **Multiple auth methods** — Can be configured with different auth methods without code changes | | | |

> **Note:** mTLS as an authentication mechanism is scored under Category 3 (criterion 3.3.3). It is not scored again here to avoid double-counting. When assessing "Multiple auth methods" (5.1.4), include mTLS as one of the supported methods if applicable.

### 5.2 Security Best Practices

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 5.2.1 | **Secure defaults** — TLS preferred/default; insecure connections require explicit opt-in | | | |
| 5.2.2 | **No credential logging** — Auth tokens and certificates are never logged or included in error messages | | | |
| 5.2.3 | **Credential handling** — Auth tokens are passed via gRPC metadata and not persisted to disk; no hardcoded credentials in examples | | | |
| 5.2.4 | **Input validation** — Channel names, metadata, and user inputs are validated before sending to server | | | |
| 5.2.5 | **Dependency security** — No known vulnerabilities in current dependencies. Verify with: `npm audit` (TS), `go vet`/`govulncheck` (Go), `pip audit` (Python), `dotnet list package --vulnerable` (C#), OWASP dependency-check (Java) | | | |

---

## Category 6: Concurrency & Thread Safety

**Goal:** Evaluate thread safety guarantees, concurrency patterns, and async support appropriate to each language.

**Benchmark:** Pulsar/Kafka "all public methods are thread-safe"; Azure SDK async-first design.

### 6.1 Thread Safety

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.1.1 | **Client thread safety** — The main client object is safe for concurrent use by multiple threads/goroutines | | | |
| 6.1.2 | **Publisher thread safety** — Publishing from multiple threads/goroutines concurrently is safe | | | |
| 6.1.3 | **Subscriber thread safety** — Multiple subscriptions can run concurrently without interference | | | |
| 6.1.4 | **Documentation of guarantees** — Thread safety guarantees are explicitly documented | | | |
| 6.1.5 | **Concurrency correctness validation** — Concurrency correctness is validated by language-appropriate automated tests or tooling. Examples: `go test -race` (Go), concurrent stress tests with assertions (Java/C#), thread-safety unit tests (Python). Mark N/A for single-threaded runtimes (Node.js) | | | |

### 6.2 Language-Specific Async Patterns

> **N/A handling:** Each subsection applies only to its target language. Mark all criteria in non-applicable subsections as N/A.

#### Go-Specific

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.2.G1 | **Context support** — All operations accept `context.Context` for cancellation and timeout | | | |
| 6.2.G2 | **Goroutine management** — Internal goroutines are properly managed and cleaned up on Close() | | | |
| 6.2.G3 | **Channel-based callbacks** — Subscription results delivered via channels or callback functions | | | |
| 6.2.G4 | **No goroutine leaks** — Client close terminates all background goroutines | | | |

#### Java-Specific

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.2.J1 | **CompletableFuture support** — Async operations return CompletableFuture for composition | | | |
| 6.2.J2 | **Executor configuration** — Thread pool / executor is configurable | | | |
| 6.2.J3 | **Reactive support** — Supports reactive streams (Publisher/Subscriber) or at minimum callback-based async | | | |
| 6.2.J4 | **AutoCloseable** — Client implements AutoCloseable for try-with-resources | | | |

#### C#-Specific

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.2.C1 | **async/await** — All I/O operations have async variants returning Task/ValueTask | | | |
| 6.2.C2 | **CancellationToken** — Async operations accept CancellationToken | | | |
| 6.2.C3 | **IAsyncDisposable** — Client implements IAsyncDisposable for async cleanup | | | |
| 6.2.C4 | **No sync-over-async** — No blocking calls on async code paths (.Result, .Wait()) | | | |

#### Python-Specific

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.2.P1 | **asyncio support** — Provides asyncio-based async client | | | |
| 6.2.P2 | **Sync + async variants** — Offers both synchronous and asynchronous client APIs | | | |
| 6.2.P3 | **Context manager** — Client supports `async with` / `with` for resource management | | | |
| 6.2.P4 | **GIL awareness** — I/O operations properly release the GIL (or use asyncio to avoid blocking) | | | |

#### TypeScript/Node-Specific

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 6.2.T1 | **Promise-based API** — All operations return Promises, support async/await | | | |
| 6.2.T2 | **Event emitter pattern** — Subscriptions use EventEmitter or async iterator pattern | | | |
| 6.2.T3 | **Backpressure handling** — Handles Node.js stream backpressure properly (readable/writable stream protocol) | | | |
| 6.2.T4 | **Graceful shutdown** — Provides deterministic close/drain APIs; documentation shows how to integrate with process signals (SIGTERM/SIGINT) for clean shutdown | | | |

---

## Category 7: Observability

**Goal:** Evaluate logging, metrics, and tracing support.

**Benchmark:** Kafka OTel instrumentation; Azure SDK diagnosability principle.

### 7.1 Logging

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 7.1.1 | **Structured logging** — Uses structured logging (not printf-style string formatting) | | | |
| 7.1.2 | **Configurable log level** — Supports configurable log levels (debug, info, warn, error) | | | |
| 7.1.3 | **Pluggable logger** — Allows user to provide their own logger implementation | | | |
| 7.1.4 | **No stdout/stderr spam** — SDK does not print directly to stdout/stderr; all output goes through the logging interface | | | |
| 7.1.5 | **Sensitive data exclusion** — Logs never contain auth tokens, message payloads, or credentials | | | |
| 7.1.6 | **Context in logs** — Log entries include relevant context (client ID, channel, operation) | | | |

### 7.2 Metrics

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 7.2.1 | **Metrics hooks** — Provides hooks/callbacks for emitting metrics | | | |
| 7.2.2 | **Key metrics exposed** — Exposes: messages sent/received, operation latency, error counts, connection state changes | | | |
| 7.2.3 | **Prometheus/OTel compatible** — Metrics can be exported to Prometheus or OpenTelemetry | | | |
| 7.2.4 | **Opt-in** — Metrics collection is opt-in, no overhead when disabled | | | |

### 7.3 Tracing

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 7.3.1 | **Trace context propagation** — Supports W3C Trace Context propagation via message metadata/tags | | | |
| 7.3.2 | **Span creation** — Creates spans for publish, subscribe, and RPC operations | | | |
| 7.3.3 | **OTel integration** — Integrates with OpenTelemetry SDK for traces | | | |
| 7.3.4 | **Opt-in** — Tracing is opt-in, no overhead when disabled | | | |

---

## Category 8: Code Quality & Architecture

**Goal:** Deep assessment of internal code structure, maintainability, serialization support, and technical debt.

**Benchmark:** Clean Architecture principles; Auth0 SDK modular design.

### 8.1 Code Structure

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.1.1 | **Package/module organization** — Code is logically organized into packages/modules/namespaces | | | |
| 8.1.2 | **Separation of concerns** — Transport, business logic, serialization, and configuration are separated | | | |
| 8.1.3 | **Single responsibility** — Classes/structs/functions have clear, single responsibilities | | | |
| 8.1.4 | **Interface-based design** — Core components use interfaces/abstractions for testability and extensibility | | | |
| 8.1.5 | **No circular dependencies** — No circular package/module imports | | | |
| 8.1.6 | **Consistent file structure** — Files follow a consistent naming and organization pattern | | | |
| 8.1.7 | **Public API surface isolation** — Public API types are clearly separated from internal implementation via access modifiers, internal packages, or module exports | | | |

### 8.2 Code Quality

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.2.1 | **Linter compliance** — Code passes standard linters: `golangci-lint` (Go), `checkstyle`/`spotbugs` (Java), `.editorconfig`/`Roslyn` (C#), `ruff`/`mypy` (Python), `eslint`/`typescript` strict (TS) | | | |
| 8.2.2 | **No dead code** — No unused functions, variables, imports, or commented-out code | | | |
| 8.2.3 | **Consistent formatting** — Code follows consistent formatting (gofmt, google-java-format, dotnet-format, black, prettier) | | | |
| 8.2.4 | **Meaningful naming** — Variables, functions, types have clear, descriptive names | | | |
| 8.2.5 | **Error path completeness** — All error paths are handled, no ignored errors or empty catch blocks | | | |
| 8.2.6 | **Magic number/string avoidance** — Constants used instead of magic values | | | |
| 8.2.7 | **Code duplication** — Minimal code duplication; shared logic properly extracted | | | |

### 8.3 Serialization & Message Handling

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.3.1 | **JSON marshaling helpers** — SDK provides convenience methods for JSON serialization/deserialization of message bodies | | | |
| 8.3.2 | **Protobuf message wrapping** — SDK properly wraps/unwraps protobuf messages without leaking proto types to the user API | | | |
| 8.3.3 | **Typed payload support** — SDK makes it easy to work with typed payloads (generics, type parameters) rather than only raw `[]byte`/`Buffer` | | | |
| 8.3.4 | **Custom serialization hooks** — Supports plugging in custom serializers/deserializers | | | |
| 8.3.5 | **Content-type handling** — Supports setting content-type metadata so consumers know how to deserialize | | | |

### 8.4 Technical Debt

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.4.1 | **TODO/FIXME/HACK comments** — Count and categorize TODO/FIXME/HACK comments. Are they tracked? | | | |
| 8.4.2 | **Deprecated code** — Any deprecated methods/classes still in use internally? | | | |
| 8.4.3 | **Dependency freshness** — Dependencies are up-to-date (no known CVEs, not more than 2 major versions behind) | | | |
| 8.4.4 | **Language version** — Uses a current, supported version of the language runtime/compiler | | | |
| 8.4.5 | **gRPC/protobuf library version** — Uses current version of gRPC and protobuf libraries | | | |

### 8.5 Extensibility

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 8.5.1 | **Interceptor/middleware support** — Supports adding interceptors or middleware to the gRPC client | | | |
| 8.5.2 | **Event hooks** — Supports lifecycle hooks (onConnect, onDisconnect, onError, onMessage) | | | |
| 8.5.3 | **Transport abstraction** — Transport layer is abstracted enough to support alternative implementations or testing | | | |

---

## Category 9: Testing

**Goal:** Evaluate test coverage, test quality, and CI/CD integration.

**Benchmark:** Kafka MockProducer/MockConsumer; 80%+ coverage target; Confluent multi-tier CI.

### 9.1 Unit Tests

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 9.1.1 | **Unit test existence** — Unit tests exist for core logic | | | |
| 9.1.2 | **Coverage percentage** — Measure/estimate code coverage (target: 80%+). Use coverage tools if available; otherwise estimate by reviewing test file coverage of source files | | | |
| 9.1.3 | **Test quality** — Tests are meaningful (not just smoke tests), test edge cases and error paths | | | |
| 9.1.4 | **Mocking** — Transport/network layer is mocked for unit tests; tests don't require a running server | | | |
| 9.1.5 | **Table-driven / parameterized tests** — Uses data-driven test patterns where appropriate | | | |
| 9.1.6 | **Assertion quality** — Uses proper assertions, not just `println` or boolean checks | | | |

### 9.2 Integration Tests

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 9.2.1 | **Integration test existence** — Integration tests exist that test against a real KubeMQ server | | | |
| 9.2.2 | **All patterns covered** — Integration tests cover events, events store, queues, and RPC | | | |
| 9.2.3 | **Error scenario testing** — Tests cover error scenarios (auth failure, timeout, invalid channel) | | | |
| 9.2.4 | **Setup/teardown** — Tests properly set up and tear down test resources | | | |
| 9.2.5 | **Parallel safety** — Tests can run in parallel without interference | | | |

### 9.3 CI/CD Pipeline

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 9.3.1 | **CI pipeline exists** — GitHub Actions, Jenkins, or other CI system configured | | | |
| 9.3.2 | **Tests run on PR** — Tests automatically run on pull requests | | | |
| 9.3.3 | **Lint on CI** — Linters/formatters run as part of CI | | | |
| 9.3.4 | **Multi-version testing** — Tests run against multiple language runtime versions | | | |
| 9.3.5 | **Security scanning** — Dependency vulnerability scanning in CI (e.g., Dependabot, Snyk, `govulncheck`, `npm audit`) | | | |

---

## Category 10: Documentation

**Goal:** Evaluate documentation completeness, quality, and accuracy.

**Benchmark:** Kafka/Azure Tier 1 documentation; NATS by Example cross-language examples.

### 10.1 API Reference

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.1.1 | **API docs exist** — Auto-generated or hand-written API reference documentation exists | | | |
| 10.1.2 | **All public methods documented** — Every public method, class, type has documentation | | | |
| 10.1.3 | **Parameter documentation** — Parameters, return values, and exceptions/errors are documented | | | |
| 10.1.4 | **Code doc comments** — Source code has proper doc comments (GoDoc, Javadoc, XML doc, docstrings, TSDoc) | | | |
| 10.1.5 | **Published API docs** — API docs are published to a browsable website (pkg.go.dev, javadoc, etc.) | | | |

### 10.2 Guides & Tutorials

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.2.1 | **Getting started guide** — Step-by-step quickstart guide exists (install, connect, first message in < 5 min) | | | |
| 10.2.2 | **Per-pattern guide** — Separate guide for each messaging pattern (events, events store, queues, RPC) | | | |
| 10.2.3 | **Authentication guide** — Guide for configuring each auth method (token, TLS, mTLS, OIDC) | | | |
| 10.2.4 | **Migration guide** — Guide for migrating from previous SDK version (if applicable) | | | |
| 10.2.5 | **Performance tuning guide** — Guide for optimizing SDK performance (timeouts, batching, connection settings) | | | |
| 10.2.6 | **Troubleshooting guide** — Common errors and their solutions documented | | | |

### 10.3 Examples & Cookbook

**Note:** Cookbook repos are assessed here. Their quality directly affects these scores.

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.3.1 | **Example code exists** — Working code examples exist in the repo or cookbook | | | |
| 10.3.2 | **All patterns covered** — Examples cover all four messaging patterns | | | |
| 10.3.3 | **Examples compile/run** — Examples are verified to compile and run correctly (best-effort: at minimum do syntax/compilation check) | | | |
| 10.3.4 | **Real-world scenarios** — Examples cover realistic use cases, not just hello-world | | | |
| 10.3.5 | **Error handling shown** — Examples demonstrate proper error handling | | | |
| 10.3.6 | **Advanced features** — Examples for auth, TLS, delayed messages, DLQ, group subscriptions | | | |

### 10.4 README Quality

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 10.4.1 | **Installation instructions** — Clear instructions for adding the SDK as a dependency | | | |
| 10.4.2 | **Quick start code** — Copy-paste-ready code in the README | | | |
| 10.4.3 | **Prerequisites** — Required language version, KubeMQ server version clearly stated | | | |
| 10.4.4 | **License** — License file present and referenced in README | | | |
| 10.4.5 | **Changelog** — CHANGELOG.md or release notes maintained | | | |

---

## Category 11: Packaging & Distribution

**Goal:** Evaluate package manager presence, versioning, and distribution quality.

**Benchmark:** SemVer; automated release pipelines; Confluent/Azure publishing standards.

### 11.1 Package Manager

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.1.1 | **Published to canonical registry** — Go: Go Modules (pkg.go.dev), Java: Maven Central, C#: NuGet, Python: PyPI, TS: npm | | | |
| 11.1.2 | **Package metadata** — Description, homepage, repository URL, license in package metadata | | | |
| 11.1.3 | **Reasonable install** — `go get`, `mvn`, `dotnet add`, `pip install`, `npm install` works smoothly | | | |
| 11.1.4 | **Minimal dependency footprint** — Only essential dependencies included; no bloat | | | |

### 11.2 Versioning & Releases

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.2.1 | **Semantic versioning** — Follows semver (MAJOR.MINOR.PATCH) | | | |
| 11.2.2 | **Release tags** — Git tags exist for each release | | | |
| 11.2.3 | **Release notes** — GitHub Releases with meaningful descriptions | | | |
| 11.2.4 | **Current version** — Latest release is reasonably recent (within last 12 months) | | | |
| 11.2.5 | **Version consistency** — Package version, git tag, and changelog version match | | | |

### 11.3 Build & Development Setup

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.3.1 | **Build instructions** — How to build the SDK from source is documented | | | |
| 11.3.2 | **Build succeeds** — The SDK builds without errors from a clean clone | | | |
| 11.3.3 | **Development dependencies** — Dev dependencies are separate from runtime dependencies | | | |
| 11.3.4 | **Contributing guide** — CONTRIBUTING.md with development setup instructions | | | |

### 11.4 SDK Binary Size & Footprint

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 11.4.1 | **Dependency weight** — Total transitive dependency count and size are reasonable for the language ecosystem | | | |
| 11.4.2 | **No native compilation required** — SDK installs without requiring native build tools (unless inherent to gRPC for that language) | | | |

---

## Category 12: Compatibility, Lifecycle & Supply Chain

**Goal:** Evaluate enterprise lifecycle practices: version compatibility, deprecation policy, supply chain security, and maintainer health.

**Benchmark:** Azure SDK backward compatibility policy; SLSA supply chain framework; Kafka broker-client compatibility matrix.

### 12.1 Compatibility

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 12.1.1 | **Server version matrix** — SDK documents which KubeMQ server versions it is compatible with | | | |
| 12.1.2 | **Runtime support matrix** — SDK documents supported language runtime versions (e.g., Go 1.22+, Java 11+, .NET 6+, Python 3.9+, Node 18+) | | | |
| 12.1.3 | **Deprecation policy** — Deprecated methods/features have clear warnings, documented removal timeline, and migration path | | | |
| 12.1.4 | **Backward compatibility discipline** — API changes follow semver: breaking changes only in major versions; patch/minor releases are backward-compatible | | | |

### 12.2 Supply Chain & Release Integrity

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 12.2.1 | **Signed releases** — Releases are signed (GPG-signed tags, Sigstore, or package manager signing) | | | |
| 12.2.2 | **Reproducible builds** — Build process is deterministic (vendored deps for Go, lock files for others) | | | |
| 12.2.3 | **Dependency update process** — Automated dependency updates (Dependabot, Renovate) or documented manual process | | | |
| 12.2.4 | **Security response process** — Documented process for reporting and handling security vulnerabilities (SECURITY.md or equivalent) | | | |
| 12.2.5 | **SBOM** — Software Bill of Materials is generated and published with releases (SPDX or CycloneDX format) | | | |
| 12.2.6 | **Maintainer health** — Active contributors in last 6 months; open issues receive responses; no stale PRs older than 90 days | | | |

> **Aspirational criteria:** 12.2.1 (Signed releases) and 12.2.5 (SBOM) are aspirational at current KubeMQ SDK maturity. Score them objectively, but if all SDKs score equally low on these, deprioritize them in the remediation roadmap in favor of higher-impact items.

---

## Category 13: Performance

**Goal:** Evaluate SDK performance infrastructure, optimization patterns, and resource efficiency.

**Benchmark:** Kafka producer/consumer perf tests; librdkafka benchmarks.

**Important:** The agent cannot produce reliable throughput/latency measurements without a controlled benchmark environment. This category assesses whether benchmark **infrastructure exists** and whether the code follows **optimization best practices**, not actual measured performance numbers.

### 13.1 Benchmark Infrastructure

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 13.1.1 | **Benchmark tests exist** — SDK includes performance/benchmark tests (e.g., Go `Benchmark*` functions, JMH for Java, BenchmarkDotNet for C#) | | | |
| 13.1.2 | **Benchmark coverage** — Benchmarks cover publish, subscribe, and queue operations | | | |
| 13.1.3 | **Benchmark documentation** — How to run benchmarks and interpret results is documented | | | |
| 13.1.4 | **Published results** — Baseline performance numbers are published (README, docs, or separate report) | | | |

### 13.2 Optimization Patterns

| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| 13.2.1 | **Object/buffer pooling** — Reuses objects/buffers where appropriate (e.g., `sync.Pool` in Go, byte buffer reuse) | | | |
| 13.2.2 | **Batching support** — Supports message batching for throughput optimization | | | |
| 13.2.3 | **Lazy initialization** — Resources are initialized lazily when possible | | | |
| 13.2.4 | **Memory efficiency** — No excessive allocations on hot paths; allocations are proportional to message throughput, not cumulative | | | |
| 13.2.5 | **Resource leak risk** — No obvious memory leaks: retained references, unclosed streams, goroutine/thread leaks on error paths | | | |
| 13.2.6 | **Connection overhead** — Reasonable resource usage per connection/subscription (not creating new gRPC channels per operation) | | | |

---

## Competitor Comparison Benchmarks

After completing the per-SDK assessment, compare KubeMQ SDKs against equivalent SDKs from these competitors:

### Comparison Matrix

For each SDK language, compare against:

| Area | Compare Against |
|------|----------------|
| **Go** | `nats.go`, `confluent-kafka-go`, `pulsar-client-go`, `grpc-go` (transport baseline) |
| **Java** | `kafka-clients`, `pulsar-client`, `amqp-client` (RabbitMQ), `azure-messaging-servicebus`, `grpc-java` (transport baseline), `spring-kafka` / `spring-amqp` (for Spring Boot comparison) |
| **C# / .NET** | `NATS.Client`, `Confluent.Kafka`, `Azure.Messaging.ServiceBus`, `RabbitMQ.Client`, `grpc-dotnet` (transport baseline) |
| **Python** | `nats-py`, `confluent-kafka`, `aio-pika` (RabbitMQ, replaces deprecated `pika`), `azure-servicebus`, `grpcio` (transport baseline) |
| **Node.js / TypeScript** | `nats`, `kafkajs`, `amqplib`, `@azure/service-bus`, `@grpc/grpc-js` (transport baseline) |

### Comparison Criteria

For each competitor SDK, note:

1. **API Design** — How does the API ergonomics compare?
2. **Feature Richness** — Does the competitor offer features KubeMQ SDK lacks?
3. **Documentation Quality** — How does documentation compare?
4. **Community Adoption** — GitHub stars, npm downloads, Maven downloads as proxy for maturity
5. **Maintenance Activity** — Release frequency, issue response time, contributor count

---

## Report Output Template

For each SDK, produce a report with the following structure:

```markdown
# KubeMQ [Language] SDK Assessment Report

## Executive Summary
- **Weighted Score (Production Readiness):** X.X / 5.0
- **Unweighted Score (Overall Maturity):** X.X / 5.0
- **Gating Rule Applied:** Yes/No (if any Critical-tier category < 3.0, overall capped at 3.0)
- **Assessment Date:** YYYY-MM-DD
- **SDK Version Assessed:** vX.X.X
- **Repository:** github.com/kubemq-io/kubemq-xxx

### Category Scores
| Category | Weight | Score | Grade | Gating? |
|----------|--------|-------|-------|---------|
| API Completeness | 14% | X.X | Good/Partial/etc. | Critical |
| API Design & DX | 9% | X.X | | |
| Connection & Transport | 11% | X.X | | Critical |
| Error Handling | 11% | X.X | | Critical |
| Auth & Security | 9% | X.X | | Critical |
| Concurrency | 7% | X.X | | |
| Observability | 5% | X.X | | |
| Code Quality | 6% | X.X | | |
| Testing | 9% | X.X | | |
| Documentation | 7% | X.X | | |
| Packaging | 4% | X.X | | |
| Compatibility & Lifecycle | 4% | X.X | | |
| Performance | 4% | X.X | | |

### Top Strengths
1. ...
2. ...
3. ...

### Critical Gaps (Must Fix)
1. ...
2. ...
3. ...

## Detailed Findings
[Each category section with filled-in scoring tables and evidence]

## Developer Journey Assessment
[Narrative walkthrough of install → connect → publish → subscribe → error → production → troubleshoot]

## Competitor Comparison
[Matrix comparing this SDK against competitor equivalents]

## Remediation Roadmap

### Phase 0: Assessment Validation (1-2 days)
Validate the top 5 most impactful findings with targeted manual smoke tests before investing in remediation.

### Phase 1: Quick Wins (Effort: S-M)
| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|
| 1 | *Example:* Add typed error hierarchy | Error Handling | 1 | 4 | M | High | — | cross-SDK | All errors inherit from KubeMQError base; 5 error subtypes exist and are documented |
| 2 | *Example:* Add connection state callback | Connection | 2 | 4 | S | High | — | cross-SDK | `onStateChange` callback fires on connect/disconnect/reconnect; unit test verifies |
| 3 | *Example:* Document K8s deployment patterns | Documentation | 1 | 4 | S | Medium | — | cross-SDK | README includes sidecar and standalone K8s connection examples |

### Phase 2: Medium-Term Improvements (Effort: M-L)
| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|
| 4 | *Example:* Add reconnection with backoff | Connection | 2 | 4 | L | High | #1 | cross-SDK | Integration test verifies auto-reconnect after server restart within 30s |

### Phase 3: Major Rework (Effort: L-XL)
| # | Item | Category | Current | Target | Effort | Impact | Depends On | Scope | Validation Metric |
|---|------|----------|---------|--------|--------|--------|------------|-------|-------------------|
| 5 | *Example:* Rewrite queue stream API | API Design | 2 | 5 | XL | High | #1, #4 | language-specific | New API matches cross-SDK design spec; all queue stream integration tests pass |

### Effort Key
Estimates assume a **senior developer proficient in the SDK's target language**, working on a single SDK.
- **S (Small):** < 1 day of work
- **M (Medium):** 1-3 days of work
- **L (Large):** 1-2 weeks of work
- **XL (Extra Large):** 2+ weeks of work

### Column Definitions
- **Impact:** High / Medium / Low — how many score points this lifts and how critical it is for production readiness
- **Depends On:** References to other items that must be completed first (e.g., "#3")
- **Scope:** `cross-SDK` (same issue across multiple SDKs) or `language-specific`
- **Validation Metric:** How to verify the fix (e.g., "unit test passes", "benchmark shows X", "doc page exists")
```

---

## Cross-SDK Comparison Report

After all individual SDK assessments are complete, produce a cross-SDK comparison:

```markdown
# KubeMQ SDK Cross-SDK Comparison Report

## Score Comparison Matrix
| Category | Weight | Go | Java | C# | Python | Node/TS |
|----------|--------|-----|------|-----|--------|---------|
| API Completeness | 14% | X.X | X.X | X.X | X.X | X.X |
| ... | | | | | | |
| **Weighted Overall** | | X.X | X.X | X.X | X.X | X.X |
| **Unweighted Overall** | | X.X | X.X | X.X | X.X | X.X |

## Feature Parity Matrix
[Full feature parity matrix from section 1.7]

## Key Parity Gaps
[List of features that are inconsistent across SDKs]

## SDK Remediation Priority Order
Recommended order for SDK remediation, considering: current score gap, user base size, strategic importance, and effort to reach parity.

1. **[SDK Name]** — Rationale
2. ...

## Unified Remediation Priority
[Prioritized list of work across all SDKs, grouped by impact, with cross-SDK items flagged]
```

---

## Spring Boot Sub-Report Template

The Spring Boot SDK is assessed with a reduced rubric:

```markdown
# KubeMQ Spring Boot SDK Assessment Sub-Report

## Executive Summary
- **Score:** X.X / 5.0
- **Assessment Date:** YYYY-MM-DD
- **SDK Version Assessed:** vX.X.X

## Spring-Specific Criteria
| # | Criterion | Score (1-5) | Confidence | Evidence / Notes |
|---|-----------|-------------|------------|-----------------|
| S.1 | **Auto-configuration** — Provides Spring Boot auto-configuration (`@EnableAutoConfiguration` / `spring.factories` / `AutoConfiguration.imports`) | | | |
| S.2 | **Starter dependency** — Published as a Spring Boot starter with correct dependency management | | | |
| S.3 | **Properties binding** — Configuration via `application.yml` / `application.properties` with `@ConfigurationProperties` | | | |
| S.4 | **Spring conventions** — Follows Spring naming, packaging, and annotation conventions | | | |
| S.5 | **Health indicator** — Provides Spring Boot Actuator health indicator for KubeMQ connection | | | |
| S.6 | **Conditional beans** — Uses `@ConditionalOnProperty` / `@ConditionalOnClass` for optional features | | | |
| S.7 | **Spring dependency injection** — KubeMQ clients are injectable Spring beans | | | |
| S.8 | **Test support** — Provides test utilities (`@SpringBootTest` support, test configuration) | | | |
| S.9 | **Documentation** — Spring-specific getting started guide, properties reference, example application | | | |
| S.10 | **Java SDK alignment** — Built on top of kubemq-java-v2, uses same version/proto definitions | | | |
| S.11 | **Spring version compatibility** — Documents supported Spring Boot versions | | | |
| S.12 | **Micrometer integration** — Exposes KubeMQ client metrics via Micrometer for Spring Actuator `/metrics` endpoint | | | |
| S.13 | **Spring Security integration** — Integrates with Spring Security context for token/credential propagation to KubeMQ | | | |

> **Note:** S.12 and S.13 are advanced integration criteria. Score N/A if the SDK is a thin wrapper that doesn't provide its own observability or security integration layer.

## Findings & Remediation
[Findings and prioritized remediation list]
```

---

## Agent Instructions

When running this assessment as a Claude Code agent:

### Document Loading Strategy

This framework is large (~1050 lines). To manage context effectively, work through it in phases:

1. **Phase 1 — Setup & Inventory:** Load the Target SDKs section and Agent Instructions. Clone the repo, inventory the codebase.
2. **Phase 2 — Category-by-category:** Work through one category at a time. For each category, reference the relevant section of this document.
3. **Phase 3 — Report generation:** Load the Report Output Template and produce the final report.

### Setup Steps

1. Clone the SDK repository being assessed
2. Inventory the full repo structure: list all directories, identify main source dirs, test dirs, docs, config files, CI config
3. Read `README.md`, `CHANGELOG.md`, `CONTRIBUTING.md`, `SECURITY.md`, and any `docs/` directory
4. Read the package manager manifest: `go.mod` (Go), `pom.xml`/`build.gradle` (Java), `.csproj` (C#), `setup.py`/`pyproject.toml` (Python), `package.json` (TS)
5. Clone the corresponding cookbook repo
6. Check the server's protobuf definitions at `github.com/kubemq-io/protobuf` to establish the feature source of truth

### Assessment Process

For each category:

1. **Read relevant source files** — Fully read all public API, connection/stream/retry/auth code, tests, examples, and docs
2. **Sample internal code by pattern** — For large codebases, read representative files rather than every line; prioritize code in the hot path
3. **Run available tooling** — Build the project, run tests, run linters if possible
4. **Check protobuf alignment** — Compare the SDK's proto usage against the server's proto definitions
5. **Verify examples** — Try to build/run examples from the cookbook repo (best-effort)
6. **Score each criterion** — Provide the score, confidence level, and specific evidence
7. **Note discrepancies** — Flag any inconsistencies with other assessed SDKs

### Important Guidelines

- **Be objective** — Score based on evidence, not assumptions
- **Be specific** — Reference exact files, functions, line numbers
- **Be comparative** — Note how the SDK compares to competitor equivalents
- **Be actionable** — Every finding should have a clear remediation suggestion
- **Scores without evidence are not accepted** — Every criterion needs file paths, code snippets, and comparison to expected patterns
- **Check protobuf version** — Ensure the SDK uses the same proto definitions as the server
- **Test the build** — Actually try to build the SDK from source
- **Run existing tests** — Execute the test suite and report results
- **Check package registry** — Verify the package exists and is installable from the registry
- **Use N/A correctly** — Only for criteria that genuinely don't apply to this language; always justify
- **Mark confidence** — Every score gets a confidence level (verified by runtime > verified by source > inferred > not assessable)
- **Assess Java v2 first** — Use it as the baseline for calibrating other SDK scores

### Expected Scope Per SDK

A thorough assessment typically requires examining 30-80 source files, the full test suite, all documentation, and the cookbook repo. Budget the equivalent of a full session per SDK. If time-constrained, prioritize Critical-tier categories (1, 3, 4, 5) first, then High-tier, then Standard-tier.

---

## References

This assessment framework draws from:

- [Azure SDK Design Guidelines](https://azure.github.io/azure-sdk/) — Gold standard for cross-language SDK design
- [Auth0 SDK Principles](https://auth0.com/blog/guiding-principles-for-building-sdks/) — 7 years, 45+ SDKs, 12 languages
- [Apache Pulsar Client Feature Matrix](https://pulsar.apache.org/client-feature-matrix/) — Feature parity tracking model
- [NATS by Example](https://natsbyexample.com/) — Cross-language example patterns
- [AWS SDK Retry Behavior](https://docs.aws.amazon.com/sdkref/latest/guide/feature-retry-behavior.html) — Industry-leading retry strategy
- [Confluent Kafka Testing](https://developer.confluent.io/learn/testing-kafka/) — Multi-tier testing approach
- [IBM/Watson SDK Guidelines](https://github.com/watson-developer-cloud/api-guidelines) — ISO/IEC 9126-1 based quality model
- [liblab Enterprise SDK Checklist](https://liblab.com/blog/ultimate-sdk-evaluation-checklist-for-enterprises) — Enterprise evaluation criteria
- [ISO/IEC 25010](https://www.iso.org/standard/35733.html) — Software product quality model
- [gRPC Best Practices](https://grpc.io/docs/guides/performance/) — Transport baseline reference
- [Google Cloud Pub/Sub Client Libraries](https://cloud.google.com/pubsub/docs/reference/libraries) — Cloud-native SDK patterns
- [KubeMQ SDK Best Practices Research](./sdk-best-practices-research.md) — Companion research document
