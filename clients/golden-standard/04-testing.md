# Category 4: Testing

**Tier:** 1 (Critical — gate blocker)
**Current Average Score:** 1.93 / 5.0
**Target Score:** 4.0+
**Weight:** 9%

## Purpose

Every SDK must have comprehensive automated tests that catch regressions, validate error handling, and verify integration with a real KubeMQ server. Tests must run in CI on every PR.

---

## Requirements

### REQ-TEST-1: Unit Tests with Mocked Transport

Unit tests must cover all SDK logic without requiring a running server.

**What to unit test:**
- Error classification logic (retryable vs non-retryable for every gRPC code)
- Error classification is tested for all 17 gRPC status codes from REQ-ERR-6 via mocked transport. Each code maps to the correct SDK error category and `IsRetryable` flag.
- Retry policy behavior (backoff calculation, jitter, max retries, exhaustion)
- Configuration validation (fail-fast on invalid inputs)
- Message serialization / deserialization roundtrips
- Connection state machine transitions
- Timeout behavior
- Auth credential handling (token injection, provider calls)
- Auth token injection into gRPC metadata (verify interceptor/middleware adds correct `Authorization` header via mocked transport).
- OTel span creation and trace context propagation (when observability is implemented, per REQ-OBS requirements). *(Phase 2+ — not a gate-blocker.)*
- Concurrent publish from multiple goroutines/threads does not corrupt state (at minimum, one concurrent test per SDK).

**Mock approach per language:**

| Language | Mock Pattern |
|----------|-------------|
| Go | `bufconn` — in-memory gRPC listener (no real ports) |
| Java | `InProcessServer` — gRPC in-process transport |
| C# | Moq + virtual members on gRPC client types. Requires interface extraction (see C# remediation plan). Until interfaces exist, mock at the gRPC channel level. |
| Python | `grpc_testing` — test server from dict of implementations |
| JS/TS | Prefer transport interface mock over spy-based mocking. Spies on internal stub methods break on refactors. |

**Coverage targets (phased):**
- **Phase 1** (testable architecture in place): **≥40%** line coverage — core logic, error classification, config validation.
- **Phase 2** (mock infrastructure complete): **≥60%** line coverage — all error paths, retry scenarios, state machine.
- **Phase 3** (mature / production-ready): **≥80%** line coverage — full unit test suite.

> **Note:** SDKs at Phase 1 must not be scored against Phase 3 criteria.

**Acceptance criteria:**
- [ ] Coverage meets the phased target for the SDK's current phase (Phase 1: ≥40%, Phase 2: ≥60%, Phase 3: ≥80%), excluding generated protobuf code
- [ ] All error classification paths have dedicated tests
- [ ] All retry scenarios are tested (success on first try, success on retry, exhaustion)
- [ ] Configuration validation is tested (valid inputs, invalid inputs, edge cases)
- [ ] Coverage threshold is enforced in CI — build fails if below the current phase target
- [ ] Client close followed by resource leak check (Go: `goleak.VerifyNone`, Java: thread count assertion, JS: `--detect-open-handles`, C#: no undisposed resources, Python: no unclosed coroutines)
- [ ] Operations on a closed client return `ErrClientClosed`
- [ ] Oversized messages exceeding `MaxSendMessageSize` produce a validation error
- [ ] Empty/nil payloads are handled correctly
- [ ] Per-test timeout is enforced (30s unit, 60s integration) to detect hangs

### REQ-TEST-2: Integration Tests Against Real Server

Integration tests must run against a real KubeMQ server to validate end-to-end behavior.

**What to integration test:**
- Connect, send, receive for all 4 messaging patterns (Events, Events Store, Queues, RPC)
- Subscription establishment and message delivery
- Queue operations (send, receive, ack, reject, DLQ, delay, peek)
- Auth token validation (valid token, invalid token, expired token)
- TLS connection (if server supports it in test environment)
- Reconnection after server restart
- Graceful shutdown / drain

**Reconnection test assertions:**
- Assert on state transitions (READY -> RECONNECTING -> READY), not wall-clock timing.
- Test message buffering: publish N during RECONNECTING, verify N delivered after reconnect.
- Test buffer overflow produces `BufferFullError`.
- Test subscription re-establishment without re-subscribing.

**Server provisioning:**

| Language | Approach |
|----------|---------|
| Go | Start KubeMQ binary as subprocess in `TestMain()` |
| Java | Docker container via Testcontainers or binary subprocess |
| C# | Docker container via Testcontainers or binary subprocess |
| Python | Docker container or binary subprocess via `subprocess` |
| JS/TS | Docker container or binary subprocess via `child_process` |

Integration tests run against `kubemq/kubemq:latest`. Pin to a specific digest in CI workflow; update the pin intentionally.

**Acceptance criteria:**
- [ ] Integration tests exist for all 4 messaging patterns
- [ ] Integration tests are clearly separated from unit tests (build tags, directories, or markers)
- [ ] Integration tests can be skipped when no server is available (`-short` flag or env var)
- [ ] Each test is independent — no shared state between tests
- [ ] Tests clean up resources (subscriptions, queue messages) after completion
- [ ] Unsubscribe while messages are in flight completes without resource leaks
- [ ] Each test uses a unique channel name (e.g., `test-{pattern}-{uuid}`) to enable parallel execution without shared state

### REQ-TEST-3: CI Pipeline

Every SDK must have a GitHub Actions CI pipeline that runs on every PR and push to main.

**Pipeline structure:**

```yaml
jobs:
  lint:        # Lint: see REQ-CQ-3 for tool and configuration requirements.
  unit-tests:  # Matrix: 2-3 latest language versions × linux
  integration: # Real KubeMQ server, linux only
  coverage:    # Upload to Codecov, enforce phased threshold (Phase 1: 40%, Phase 2: 60%, Phase 3: 80%)
```

**Go-specific:** Unit and integration tests MUST run with `-race` flag.

**Recommended:** CI step that verifies SDK proto files match the server's canonical proto definition (e.g., `buf breaking` or file diff). This catches API drift before integration tests.

**Acceptance criteria:**
- [ ] CI runs on every PR and push to main/master
- [ ] Unit tests run across 2-3 latest language runtime versions
- [ ] Integration tests run against a real KubeMQ server in CI
- [ ] Linter runs and blocks merge on violations
- [ ] Coverage is reported to Codecov (or equivalent)
- [ ] Coverage threshold is enforced per the SDK's current phase — PR fails if below target

### REQ-TEST-4: Test Organization

**Directory structure per language:**

| Language | Unit Tests | Integration Tests |
|----------|-----------|-------------------|
| Go | `*_test.go` in same package, `//go:build !integration` | `*_test.go` with `//go:build integration` |
| Java | `src/test/java/**/*Test.java` | `src/test/java/**/*IT.java` (Maven Failsafe) |
| C# | `KubeMQ.Tests.Unit/` project | `KubeMQ.Tests.Integration/` project |
| Python | `tests/unit/` | `tests/integration/` with `@pytest.mark.integration` |
| JS/TS | `__tests__/unit/` or `*.test.ts` | `__tests__/integration/` or `*.integration.test.ts` |

**Acceptance criteria:**
- [ ] Unit and integration tests are in separate directories or clearly tagged
- [ ] `make test` / `go test ./...` / `npm test` runs only unit tests by default
- [ ] Integration tests require an explicit flag or environment variable
- [ ] Test helpers and fixtures are shared via a `testutil` or `fixtures` package

### REQ-TEST-5: Coverage Tools

| Language | Tool | Phase 1 Threshold | Phase 2 Threshold | Phase 3 Threshold |
|----------|------|-------------------|-------------------|-------------------|
| Go | `go test -coverprofile` | Script check ≥40% | Script check ≥60% | Script check ≥80% |
| Java | JaCoCo | `<minimum>0.40</minimum>` | `<minimum>0.60</minimum>` | `<minimum>0.80</minimum>` |
| C# | Coverlet | `--threshold 40` | `--threshold 60` | `--threshold 80` |
| Python | pytest-cov | `--cov-fail-under=40` | `--cov-fail-under=60` | `--cov-fail-under=80` |
| JS/TS | c8 or istanbul | `--check-coverage --lines 40` | `--check-coverage --lines 60` | `--check-coverage --lines 80` |

The Phase 3 threshold (80%) is the gate for production readiness.

**Acceptance criteria:**
- [ ] Coverage tool is configured and runs in CI
- [ ] Coverage report is generated in a standard format (Cobertura XML, LCOV, or JSON)
- [ ] Coverage is uploaded to Codecov or equivalent dashboard
- [ ] Generated/vendored code is excluded from coverage measurement

---

## What 4.0+ Looks Like

- Phased coverage targets met: Phase 1 (≥40%) for SDKs establishing testable architecture, Phase 2 (≥60%) with mock infrastructure, Phase 3 (≥80%) for production-ready SDKs — all error paths exercised
- Integration tests verify all 4 messaging patterns against a real server with deterministic reconnection assertions
- CI pipeline catches regressions on every PR — lint, test, coverage enforced at the appropriate phase threshold
- Tests are well-organized, independent, and enforce per-test timeouts (30s unit, 60s integration)
- Each test uses unique channel names for parallel execution without shared state
- Resource leak detection is in place for every SDK
- Coverage dashboard shows trends and blocks PRs that reduce coverage below the current phase target
