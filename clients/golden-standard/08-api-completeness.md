# Category 8: API Completeness & Feature Parity

**Tier:** 2 (Should-have)
**Current Average Score:** 4.10 / 5.0
**Target Score:** 4.0+
**Weight:** 14%

## Purpose

All SDKs must support the same messaging features. Users switching between languages should find the same capabilities. Feature gaps are tracked via a published feature matrix.

---

## Requirements

### REQ-API-1: Core Feature Coverage

Every SDK must implement all core messaging patterns with all standard operations.

**Client Management:**
- [ ] Ping (Core)
- [ ] Channel List (Core)
- [ ] Server Info (Extended)
- [ ] Channel Create (Extended)
- [ ] Channel Delete (Extended)

**Events (Pub/Sub):**
- [ ] Publish event to channel
- [ ] Subscribe to channel with callback/handler
- [ ] Subscribe with wildcard/pattern matching
- [ ] Subscribe with group (load-balanced consumption)
- [ ] Unsubscribe

**Events Store (Persistent Pub/Sub):**
- [ ] Publish event to store channel
- [ ] Subscribe from beginning (sequence = 0)
- [ ] Subscribe from specific sequence number
- [ ] Subscribe from specific timestamp
- [ ] Subscribe from time delta (relative time offset)
- [ ] Subscribe from last message
- [ ] Subscribe new only (latest)
- [ ] Unsubscribe

**Queues (Stream-based — primary API):**
- [ ] Queue stream upstream (send via persistent stream)
- [ ] Queue stream downstream (receive via persistent stream with ack/reject/requeue)
- [ ] Visibility timeout on downstream messages
- [ ] Ack message
- [ ] Reject message
- [ ] Requeue message
- [ ] DLQ (dead letter queue) — send to, receive from
- [ ] Delayed messages
- [ ] Message expiration

**Queues (Simple — secondary API):**
- [ ] Send message to queue (single, non-stream)
- [ ] Send batch messages
- [ ] Receive message(s) (single pull)
- [ ] Peek (view without consuming)

**RPC — Commands:**
- [ ] Send command
- [ ] Subscribe to commands (handler)
- [ ] Subscribe to commands with group (load-balanced)
- [ ] Send response

**RPC — Queries:**
- [ ] Send query
- [ ] Subscribe to queries (handler)
- [ ] Subscribe to queries with group (load-balanced)
- [ ] Send response
- [ ] Cache-enabled queries (CacheKey, CacheTTL)

### REQ-API-2: Feature Matrix Document

A version-controlled feature matrix must be maintained in the `clients/` directory.

**Format:**

| Feature | Go | Java | C# | Python | JS/TS | Status |
|---------|-----|------|-----|--------|-------|--------|
| Events: Publish | ✅ | ✅ | ✅ | ✅ | ✅ | Core |
| Events: Wildcard Subscribe | ✅ | ✅ | ⚠️ | ✅ | ❌ | Core |
| ... | | | | | | |

**Acceptance criteria:**
- [ ] Feature matrix document exists and is current
- [ ] Feature matrix is reviewed and updated with each minor or major SDK release
- [ ] Features are categorized as Core (required) or Extended (optional)
- [ ] Gaps are documented with rationale (language limitation, planned, etc.)

### REQ-API-3: No Silent Feature Gaps

When an SDK doesn't support a Core feature, it must:
- [ ] Be documented in the feature matrix with rationale
- [ ] Return a clear `ErrNotImplemented` error (not a silent no-op)
- [ ] Have a tracking issue for implementation

---

## What 4.0+ Looks Like

- All Core features implemented in all 5 SDKs, including client management (Ping, Channel List), group subscriptions, and all Events Store start positions
- Feature matrix is current, published, and reviewed with each minor/major release
- No silent gaps — missing features are documented and tracked
- Queue stream upstream/downstream is the primary queue API in all SDKs
- Simple queue send/receive available as convenience secondary API
