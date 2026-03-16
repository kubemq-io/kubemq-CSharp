---
name: test-runner
description: Run full unit and integration tests for the KubeMQ C# SDK against a live broker, collect code coverage, and produce a comprehensive test report. Use when the user asks to run tests, check test health, verify SDK against a live broker, or wants a test report.
---

# Test Runner

Run all unit and integration tests for the KubeMQ C# SDK against a live KubeMQ broker and produce a detailed test report.

## Prerequisites

- .NET 8.0 SDK installed
- KubeMQ broker running (default: `localhost:50000`)

## Instructions

Follow these steps in order. Use the Shell tool for all commands.

### Step 1: Verify Broker Connectivity

Before running tests, confirm the broker is reachable:

```bash
nc -z -w 3 localhost 50000 && echo "Broker reachable" || echo "Broker NOT reachable"
```

If the broker is unreachable, warn the user and ask whether to:
- Proceed with unit tests only (skip integration)
- Abort and wait for broker

### Step 2: Restore & Build

Build the SDK and test project from the workspace root:

```bash
dotnet restore src/KubeMQ.Sdk/KubeMQ.Sdk.csproj
dotnet build src/KubeMQ.Sdk/KubeMQ.Sdk.csproj --configuration Release --no-restore
dotnet restore tests/KubeMQ.Sdk.Tests.Unit/KubeMQ.Sdk.Tests.Unit.csproj
dotnet build tests/KubeMQ.Sdk.Tests.Unit/KubeMQ.Sdk.Tests.Unit.csproj --configuration Release --no-restore
```

If the build fails, report the errors and stop.

### Step 3: Run Unit Tests

Run all tests tagged `Category=Unit` (the assembly default) with code coverage:

```bash
dotnet test tests/KubeMQ.Sdk.Tests.Unit/KubeMQ.Sdk.Tests.Unit.csproj \
  --configuration Release --no-build \
  --filter "Category!=Integration" \
  --collect:"XPlat Code Coverage" \
  --settings tests/coverlet.runsettings \
  --results-directory ./test-results/unit \
  --logger "trx;LogFileName=unit-results.trx" \
  --logger "console;verbosity=detailed" \
  --verbosity normal \
  2>&1
```

Capture and save:
- Total tests / passed / failed / skipped counts
- Any failure details (test name, error message, stack trace)
- Duration

### Step 4: Run Integration Tests

Run tests tagged `Category=Integration` against the live broker:

```bash
dotnet test tests/KubeMQ.Sdk.Tests.Unit/KubeMQ.Sdk.Tests.Unit.csproj \
  --configuration Release --no-build \
  --filter "Category=Integration" \
  --collect:"XPlat Code Coverage" \
  --settings tests/coverlet.runsettings \
  --results-directory ./test-results/integration \
  --logger "trx;LogFileName=integration-results.trx" \
  --logger "console;verbosity=detailed" \
  --verbosity normal \
  2>&1
```

Capture and save the same metrics as unit tests.

Integration tests connect to `localhost:50000` and exercise:
- gRPC transport connect/disconnect
- Ping (server info)
- Event publish
- Event store publish
- Queue send/receive/poll
- Channel create/delete/list
- Event subscription (pub/sub round-trip)
- Command request/response (RPC round-trip)
- Query request/response (RPC round-trip)
- Reconnection lifecycle

### Step 5: Collect Coverage Report

Parse the Cobertura XML coverage file to extract line and branch coverage:

```bash
find ./test-results -name "coverage.cobertura.xml" -exec grep -m1 'coverage ' {} \;
```

Extract `line-rate` and `branch-rate` attributes from all coverage files.

### Step 6: Generate Report

Produce the report as a structured message to the user, following this template:

---

```
# KubeMQ C# SDK — Test Report

**Date:** {YYYY-MM-DD HH:MM}
**Broker:** localhost:50000
**SDK Version:** 3.0.0
**Target Framework:** net8.0
**Test Framework:** xUnit + FluentAssertions + Moq

---

## Build Status

| Component | Status |
|-----------|--------|
| SDK Build | ✅ Pass / ❌ Fail |
| Test Build | ✅ Pass / ❌ Fail |

---

## Unit Tests

| Metric | Value |
|--------|-------|
| Total | {n} |
| Passed | {n} |
| Failed | {n} |
| Skipped | {n} |
| Duration | {time} |

### Test Areas Covered

| Area | Tests | Status |
|------|-------|--------|
| Client (connect, publish, subscribe, queues, commands, lifecycle, drain, channels, edge cases) | {n} | ✅/❌ |
| Config (options, TLS, retry, reconnect, keepalive, subscription) | {n} | ✅/❌ |
| Models (all DTOs: event, command, query, queue, channel, subscription, server info) | {n} | ✅/❌ |
| Transport (connection manager, streams, TLS, state machine, buffer, callbacks) | {n} | ✅/❌ |
| Protocol (auth interceptor, telemetry interceptor, retry handler, error mapper, defaults, stopwatch) | {n} | ✅/❌ |
| Telemetry (activity source, metrics, trace context, text map carrier) | {n} | ✅/❌ |
| DependencyInjection (service collection, hosted service) | {n} | ✅/❌ |
| Exceptions (hierarchy, error codes) | {n} | ✅/❌ |
| Validation (message validator) | {n} | ✅/❌ |
| ErrorClassification (gRPC error mapper) | {n} | ✅/❌ |
| SDK Info | {n} | ✅/❌ |

### Failed Tests (if any)

| Test | Error | Stack Trace (key line) |
|------|-------|-----------------------|
| {FullyQualifiedName} | {message} | {relevant line} |

---

## Integration Tests (Live Broker)

| Metric | Value |
|--------|-------|
| Total | {n} |
| Passed | {n} |
| Failed | {n} |
| Skipped | {n} |
| Duration | {time} |

### Test Scenarios

| Scenario | Status | Notes |
|----------|--------|-------|
| gRPC Transport Connect | ✅/❌ | |
| gRPC Transport Ping | ✅/❌ | |
| gRPC Transport Send Event | ✅/❌ | |
| gRPC Transport Send Queue | ✅/❌ | |
| gRPC Transport Poll Queue | ✅/❌ | |
| gRPC Transport Close/Dispose | ✅/❌ | |
| gRPC Transport Error Paths | ✅/❌ | |
| Client Connect & Ping | ✅/❌ | |
| Client Publish Event | ✅/❌ | |
| Client Publish Event Store | ✅/❌ | |
| Client Send Queue Message | ✅/❌ | |
| Client Send Queue Batch | ✅/❌ | |
| Client Poll Queue | ✅/❌ | |
| Client List Channels | ✅/❌ | |
| Client Create/Delete Channel | ✅/❌ | |
| Client Subscribe Events (pub/sub round-trip) | ✅/❌ | |
| Client Send Command (RPC round-trip) | ✅/❌ | |
| Client Send Query (RPC round-trip) | ✅/❌ | |
| Client Disconnect & Reconnect | ✅/❌ | |

### Failed Tests (if any)

| Test | Error | Stack Trace (key line) |
|------|-------|-----------------------|
| {FullyQualifiedName} | {message} | {relevant line} |

---

## Code Coverage

| Metric | Value |
|--------|-------|
| Line Coverage | {X.X%} |
| Branch Coverage | {X.X%} |

---

## Summary

| Category | Total | Passed | Failed | Skipped |
|----------|-------|--------|--------|---------|
| Unit | {n} | {n} | {n} | {n} |
| Integration | {n} | {n} | {n} | {n} |
| **Overall** | {n} | {n} | {n} | {n} |

### Overall Verdict: ✅ ALL PASS / ⚠️ PARTIAL / ❌ FAILURES

{Brief narrative: what passed, what failed, any patterns in failures, 
recommendations for fixing failures if any}
```

---

## Handling Edge Cases

- **Build failure:** Report errors, stop execution, do not attempt tests.
- **Broker unreachable:** Run unit tests only, mark integration as "Skipped (broker unavailable)".
- **Test timeout:** If `dotnet test` runs longer than 5 minutes, note it. Integration tests have individual 10-15s timeouts per test.
- **Partial integration failures:** Some integration tests use try/catch for server version differences. Distinguish expected catches from real failures.
- **Coverage file missing:** Report "Coverage data not available" rather than failing.
