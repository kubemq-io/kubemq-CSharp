# Category 9: API Design & Developer Experience

**Tier:** 2 (Should-have)
**Current Average Score:** 3.28 / 5.0
**Target Score:** 4.0+
**Weight:** 9%

## Purpose

The SDK API must feel natural in each language. Configuration is simple, the happy path requires minimal code, and advanced features are progressively disclosed.

---

## Requirements

### REQ-DX-1: Language-Idiomatic Configuration

Each SDK must use the configuration pattern that is standard for its language.

| Language | Pattern | Example |
|----------|---------|---------|
| Go | Functional options | `NewClient(addr, WithTimeout(10*time.Second))` |
| Java | Builder pattern | `KubeMQClient.builder().address(addr).timeout(Duration.ofSeconds(10)).build()` |
| C# | Options object | `new KubeMQClient(new ClientOptions { Address = addr, Timeout = TimeSpan.FromSeconds(10) })` |
| Python | kwargs with defaults | `KubeMQClient(address=addr, timeout=10)` |
| JS/TS | Options object literal | `new KubeMQClient({ address: addr, timeout: 10000 })` |

**Acceptance criteria:**
- [ ] Configuration follows the language-idiomatic pattern (not translated from another language)
- [ ] Required parameters are enforced at compile time or construction time
- [ ] Optional parameters have documented default values
- [ ] Invalid configuration is rejected at construction (fail-fast)

### REQ-DX-2: Minimal Code Happy Path

Publish/send operations: 3 lines or fewer. Subscribe/receive with acknowledgment: 10 lines or fewer (after import and client creation).

> **Note:** The real metric is "first message in under 5 minutes."

**Example (Go Events):**
```go
client, _ := kubemq.NewClient("localhost:50000")
client.PublishEvent("my-channel", []byte("hello"))
client.Close()
```

**Acceptance criteria:**
- [ ] Events publish: ≤3 lines
- [ ] Queue send: ≤3 lines
- [ ] RPC command/query: ≤3 lines
- [ ] Subscribe/receive with ack: ≤10 lines
- [ ] Defaults (localhost:50000, no auth) work for local development

### REQ-DX-3: Consistent Verbs Across SDKs

All SDKs must use the same verb vocabulary (with language-appropriate casing).

| Operation | Verb | Example (Go) | Example (Java) |
|-----------|------|-------------|----------------|
| Publish event | `Publish` | `PublishEvent()` | `publishEvent()` |
| Subscribe | `Subscribe` | `SubscribeEvents()` | `subscribeToEvents()` |
| Send to queue | `Send` | `SendQueueMessage()` | `sendQueueMessage()` |
| Receive from queue | `Receive` | `ReceiveQueueMessages()` | `receiveQueueMessages()` |
| Acknowledge | `Ack` | `AckMessage()` | `ackMessage()` |
| Reject | `Reject` | `RejectMessage()` | `rejectMessage()` |
| Send command | `Send` | `SendCommand()` | `sendCommand()` |
| Send query | `Send` | `SendQuery()` | `sendQuery()` |

**Acceptance criteria:**
- [ ] All SDKs use the same verbs (casing adapted to language convention)
- [ ] Method names are predictable — a user who knows the Go SDK can guess the Java method name

### REQ-DX-4: Fail-Fast Validation

All user inputs must be validated at the point of entry.

**What to validate eagerly:**
- Address format (non-empty, valid host:port)
- Channel names (non-empty, valid characters)
- Timeout values (positive)
- TLS certificate paths (file exists, readable)
- Token (non-empty when auth is configured)
- Message body size (reject before sending if over gRPC max)
- ClientId format (non-empty)
- Subscription type validity (e.g., reject negative sequence numbers)

**Acceptance criteria:**
- [ ] Invalid inputs produce clear error messages at construction/call time
- [ ] Validation errors are classified as non-retryable
- [ ] Validation happens before any network call

### REQ-DX-5: Message Builder/Factory

Messages must be constructable with clear, validated builders or factory methods.

**Acceptance criteria:**
- [ ] Message types have builder/factory methods (not raw struct construction)
- [ ] Required fields are enforced at build time
- [ ] Optional fields have sensible defaults (empty metadata, no expiration)
- [ ] Messages are immutable after construction (where language supports it)

---

## What 4.0+ Looks Like

- API feels native to each language — Go developers say "this feels like Go"
- Zero-config local development works out of the box
- First message sent or received in under 5 minutes
- Publish/send in 3 lines, subscribe/receive with ack in 10 lines or fewer
- Method names are predictable across SDKs
- Bad inputs (invalid addresses, oversized messages, empty ClientIds, invalid subscription types) are caught immediately with clear error messages
- Message construction is guided and validated
