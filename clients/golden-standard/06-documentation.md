# Category 6: Documentation

**Tier:** 1 (Critical — gate blocker)
**Current Average Score:** 2.68 / 5.0
**Target Score:** 4.0+
**Weight:** 7%

## Purpose

A developer should be able to install the SDK, send their first message in under 5 minutes, and find answers to their questions without reading source code. Documentation lives in the SDK repo and API reference is auto-generated from code comments.

---

## Requirements

### REQ-DOC-1: Auto-Generated API Reference

Every public type, method, and parameter must have documentation comments. API reference is auto-generated from these comments.

**Tools per language:**

| Language | Doc Comment Format | Generator | Host |
|----------|-------------------|-----------|------|
| Go | `// Comment` (GoDoc format) | pkg.go.dev (automatic) | pkg.go.dev |
| Java | `/** Javadoc */` | Maven Javadoc plugin | GitHub Pages or Maven site |
| C# | `/// <summary>` XML comments | DocFX | GitHub Pages |
| Python | `"""Google-style docstrings"""` | Sphinx + autodoc or mkdocstrings | GitHub Pages or ReadTheDocs |
| JS/TS | `/** TSDoc */` | TypeDoc | GitHub Pages |

**Doc comment requirements:**
- First line: one-sentence summary (shown in IDE tooltips and package indexes)
- The summary line must not simply restate the method name
- Parameters: name, type, description, default value, valid range
- Return value: type and description
- Errors/Exceptions: which errors can be returned and when
- Example: at least one code example for non-trivial methods
- Deprecation: `@deprecated` / `// Deprecated:` with replacement guidance

> **Note on API reference hosting:** Go gets pkg.go.dev automatically, Java gets Maven Central javadoc. The requirement is that doc comments exist and pass the linter. API reference accessibility is a release process concern, not a documentation spec gate.

**Acceptance criteria:**
- [ ] 100% of public types, methods, and constants have doc comments
- [ ] Doc comment linter is configured and runs in CI (godoc-lint, Checkstyle, Roslyn, pydocstyle/ruff, eslint-plugin-tsdoc)
- [ ] API reference is published and accessible via a URL
- [ ] API reference is regenerated on every release

### REQ-DOC-2: README

The SDK README must follow a standard structure adapted from the [Azure SDK README template](https://github.com/Azure/azure-sdk/blob/main/docs/policies/README-TEMPLATE.md).

**Required sections:**

1. **Title and badges** — Package name, version badge, CI status badge, coverage badge
2. **Description** — 2-3 sentences: what KubeMQ is, what this SDK does
3. **Installation** — Single copy-paste command per package manager
4. **Quick Start** — Minimal working example (see REQ-DOC-3)
5. **Messaging Patterns** — Brief description of each pattern with links to examples, plus the comparison table below
6. **Configuration** — Key options table (address, auth, TLS, timeouts)
7. **Error Handling** — How errors work, retry policy defaults, example
8. **Troubleshooting** — Top 5 common issues with solutions
9. **Contributing** — Link to CONTRIBUTING.md
10. **License** — License type with link

**Messaging Pattern Comparison Table (Section 5):**

| Pattern | Delivery Guarantee | Use When | Example Use Case |
|---------|--------------------|----------|------------------|
| Events | At-most-once | You need fire-and-forget broadcasting to multiple subscribers | Real-time notifications, log streaming |
| Events Store | At-least-once (persistent) | Subscribers must not miss messages, even if offline | Audit trails, event sourcing, replay |
| Queues | At-least-once (with ack) | Work must be processed exactly by one consumer with acknowledgment | Job processing, task distribution |
| Commands | At-most-once (request/reply) | You need a response confirming the action was executed | Device control, configuration changes |
| Queries | At-most-once (request/reply) | You need to retrieve data from a responder | Data lookups, service-to-service reads |

**Acceptance criteria:**
- [ ] All 10 sections are present
- [ ] Installation instructions work for the current published version
- [ ] All code examples in the README compile/run without errors
- [ ] Links use absolute URLs (README is rendered on package manager sites where relative links break)

### REQ-DOC-3: Quick Start (First Message in 5 Minutes)

Each SDK must include a copy-paste-ready minimal example that achieves first message send/receive.

**Quick Start structure:**

1. **Prerequisites** (3-4 bullets max): language version, KubeMQ server running, install command
2. **Send a message** (≤10 lines of code)
3. **Receive a message** (≤10 lines of code)
4. **Expected output** (what the user should see)

**Acceptance criteria:**
- [ ] Quick start works with zero configuration against `localhost:50000`
- [ ] Code is copy-paste ready — no placeholders that need replacing
- [ ] Each messaging pattern (Events, Queues, RPC) has its own quick start example
- [ ] Total time from `git clone` to first message < 5 minutes

### REQ-DOC-4: Code Examples / Cookbook

Per-pattern, per-feature examples organized in a `examples/` directory in the SDK repo.

**Required examples:**

| Pattern | Examples |
|---------|----------|
| Events | Basic pub/sub, wildcard subscription, multiple subscribers |
| Events Store | Persistent pub/sub, replay from sequence, replay from time |
| Queues | Send/receive, ack/reject, DLQ, delayed messages, peek, batch |
| Queues (Stream) | Stream upstream (send via stream), stream downstream (receive with ack/reject/requeue), visibility timeout |
| RPC Commands | Send command, handle command |
| RPC Queries | Send query, handle query, cached response |
| Configuration | TLS setup, mTLS setup, token auth, custom timeouts |
| Observability | OpenTelemetry setup with Jaeger/OTLP export |

**Acceptance criteria:**
- [ ] Every example is self-contained and runnable
- [ ] Examples have inline comments explaining each step
- [ ] Examples directory has its own README listing all examples with descriptions
- [ ] Examples are tested in CI (compile check at minimum)
- [ ] Examples MUST compile in the main CI pipeline. Compilation failures in `examples/` block merge

### REQ-DOC-5: Troubleshooting Guide

A troubleshooting document covering the most common issues.

**Required entries (minimum 11):**

| Issue | Category |
|-------|----------|
| Connection refused / timeout | Connection |
| Authentication failed (invalid token) | Auth |
| Authorization denied (insufficient permissions) | Auth |
| Channel not found | Operations |
| Message too large | Operations |
| Timeout / deadline exceeded | Operations |
| Rate limiting / throttling | Operations |
| Internal server error | Operations |
| TLS handshake failure | Security |
| No messages received (subscriber not getting messages) | Patterns |
| Queue message not acknowledged | Queues |

**Entry format:**
```
## Problem: {symptom}
**Error message:** `{exact error text}`
**Cause:** {why this happens}
**Solution:** {step-by-step fix}
**Code example:** {if applicable}
```

**Acceptance criteria:**
- [ ] Minimum 11 troubleshooting entries covering the issues above
- [ ] Each entry includes the exact error message users will see
- [ ] Solutions are actionable — not just "check your configuration"
- [ ] Entries link to relevant sections of the README or API reference

### REQ-DOC-6: CHANGELOG

Every SDK must maintain a CHANGELOG following [Keep a Changelog](https://keepachangelog.com/) format.

**Acceptance criteria:**
- [ ] CHANGELOG.md exists in the repo root
- [ ] Entries are grouped by version and date
- [ ] Categories: Added, Changed, Deprecated, Removed, Fixed, Security
- [ ] Breaking changes are prominently marked
- [ ] Each entry links to the relevant PR or commit

### REQ-DOC-7: Migration Guide

Required for major version upgrades. Enables existing users to upgrade without support tickets.

**Required contents:**
- Breaking changes table: what changed, old behavior, new behavior
- Before/after code snippets for every renamed or removed method
- Step-by-step upgrade procedure

**Acceptance criteria:**
- [ ] Migration guide exists for every major version upgrade (e.g., v1 → v2)
- [ ] Every breaking change has a before/after code example
- [ ] Guide is linked from the CHANGELOG and README

---

## What 4.0+ Looks Like

- 100% of public APIs have doc comments with meaningful summaries (not tautological) — IDE autocomplete shows helpful descriptions
- README gets a developer from zero to first message in under 5 minutes
- README includes a messaging pattern comparison table so users pick the right pattern immediately
- Every messaging pattern has runnable examples in the examples/ directory, including queue stream operations
- Examples compile in CI — broken examples block merge
- Troubleshooting guide resolves the top 11 issues without needing to contact support
- CHANGELOG clearly communicates what changed in each release
- Migration guide exists for every major version upgrade with before/after code
- API reference is auto-generated, published, and always current
- Consistent terminology across all documentation (see Appendix A)

---

## Appendix A: Terminology Glossary

Consistent terminology across all SDK documentation, code comments, and examples. Use these terms exactly as defined.

| Term | Definition |
|------|------------|
| **Channel** | A named destination for messages. Analogous to a topic or queue name. All messaging patterns use channels. |
| **Event** | A fire-and-forget message sent to one or more subscribers via pub/sub. No persistence or delivery guarantee. |
| **Event Store** | A persistent event that is stored by the server. Subscribers can replay from a sequence number or timestamp. |
| **Queue** | A message stored for pull-based consumption. Exactly one consumer processes each message, with explicit acknowledgment. |
| **Command** | A request/reply message where the sender expects confirmation that an action was executed. No response payload. |
| **Query** | A request/reply message where the sender expects a data response from the handler. |
| **Subscription** | A client's registration to receive messages on a channel (or wildcard pattern). Applies to Events, Events Store, Commands, and Queries. |
| **Client** | An SDK instance connected to a KubeMQ server. A single client can send and receive across all messaging patterns. |
| **Message** | The unit of data transmitted through any messaging pattern. Contains a channel, body (bytes), metadata (string), and optional tags. |
| **Metadata** | A string field on a message for application-level context. Not indexed or searchable by the server. |
| **Tags** | Key-value string pairs on a message. Used for filtering, routing, and application-level metadata. |
| **Client ID** | A unique identifier for a connected client. Used for tracking, auditing, and group subscriptions. |
| **Group** | A load-balancing mechanism where multiple subscribers with the same group name share messages (only one receives each message). |
| **Visibility Timeout** | The duration a queue message is hidden from other consumers after being received, allowing time for processing before ack/reject. |
