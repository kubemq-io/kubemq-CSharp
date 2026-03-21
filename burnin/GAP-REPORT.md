# C# SDK Burn-In REST API — Implementation Gap Report

> **Date**: 2026-03-17
> **Spec Version**: 2.2
> **Implementation Status**: Complete

---

## Implementation Summary

| Category | Done | Partial | Not Done | Total |
|----------|:----:|:-------:|:--------:|:-----:|
| Boot & Lifecycle | 12 | 0 | 0 | 12 |
| Endpoints | 11 | 0 | 0 | 11 |
| HTTP & Error Handling | 8 | 0 | 0 | 8 |
| Config Handling | 12 | 0 | 0 | 12 |
| Run Data & Metrics | 14 | 0 | 0 | 14 |
| Report & Verdict | 19 | 0 | 0 | 19 |
| Startup Config & CLI | 13 | 0 | 0 | 13 |
| **Total** | **89** | **0** | **0** | **89** |
| **% Complete** | **100%** | **0%** | **0%** | |

---

## 13.1 Boot & Lifecycle

| # | Requirement | Spec Ref | C# |
|---|------------|----------|:--:|
| L1 | Boot into `idle` state (no auto-start) | §2 | [x] |
| L2 | HTTP server starts on boot before broker connection | §2 | [x] |
| L3 | `/health` returns `{"status":"alive"}` with 200 from boot | §3.1 | [x] |
| L4 | `/ready` per-state response: 200 for idle/running/stopped/error, 503 for starting/stopping | §3.1 | [x] |
| L5 | Pre-initialize all Prometheus metrics to 0 on startup | §2, §8.3 | [x] |
| L6 | Run state machine: `idle`→`starting`→`running`→`stopping`→`stopped`/`error` | §4.1 | [x] |
| L7 | Atomic state transitions (C#: Interlocked/lock) | §4.2 | [x] |
| L8 | `starting_timeout_seconds` (default 60s) — exceeds → `error` | §4.1, §4.2 | [x] |
| L9 | Per-pattern states: `starting`, `running`, `recovering`, `error`, `stopped` | §4.3 | [x] |
| L10 | Stop during `starting` — cancel, cleanup, → `stopped` | §4.4 | [x] |
| L11 | SIGTERM/SIGINT: stop active run, generate report, exit | §9 | [x] |
| L12 | Exit codes: 0=PASSED/PASSED_WITH_WARNINGS, 1=FAILED, 2=config error | §9 | [x] |

---

## 13.2 Endpoints

| # | Endpoint | Spec Ref | C# |
|---|----------|----------|:--:|
| E1 | `GET /info` — sdk, version, runtime, os, arch, cpus, memory, pid, uptime, state, broker_address | §5.1 | [x] |
| E2 | `GET /broker/status` — gRPC Ping() with 3s timeout | §5.2 | [x] |
| E3 | `POST /run/start` — full config body, validate, return 202 | §5.3 | [x] |
| E4 | `POST /run/stop` — graceful stop, 202. 409 for wrong states | §5.4 | [x] |
| E5 | `GET /run` — full state with pattern+worker metrics | §5.5 | [x] |
| E6 | `GET /run/status` — lightweight status + totals | §5.6 | [x] |
| E7 | `GET /run/config` — resolved config with channel names, 404 when no run | §5.7 | [x] |
| E8 | `GET /run/report` — verdict checks map, `startup` check, 404 when none | §5.8 | [x] |
| E9 | `POST /cleanup` — delete `csharp_burnin_*` channels, 409 during active | §5.9 | [x] |
| E10 | Legacy alias: `/status` → `/run/status` with deprecation warning | §3 | [x] |
| E11 | Legacy alias: `/summary` → `/run/report` with deprecation warning | §3 | [x] |

---

## 13.3 HTTP & Error Handling

| # | Requirement | Spec Ref | C# |
|---|------------|----------|:--:|
| H1 | CORS headers on all responses with configurable `BURNIN_CORS_ORIGINS` | §7 | [x] |
| H2 | `OPTIONS` preflight → 204 No Content | §7 | [x] |
| H3 | Error response format: `{"message": "...", "errors": [...]}` | §6 | [x] |
| H4 | `400` for invalid JSON body with parse error | §5.3.4, §6 | [x] |
| H5 | `400` for validation errors — collect ALL, return together | §5.3.4 | [x] |
| H6 | `409` for state conflicts — include `run_id` and `state` | §5.3, §5.4, §5.9 | [x] |
| H7 | `Content-Type: application/json` on all JSON responses | §3 | [x] |
| H8 | Silently ignore unknown JSON fields (System.Text.Json) | §1, §5.3.4 | [x] |

---

## 13.4 Config Handling

| # | Requirement | Spec Ref | C# |
|---|------------|----------|:--:|
| C1 | Parse nested per-pattern API config schema | §5.3.1 | [x] |
| C2 | Translate API config → internal flat config per mapping table | §5.3.3 | [x] |
| C3 | Per-pattern `enabled` flag — `{"enabled":false}` in responses | §5.3.2, §5.5 | [x] |
| C4 | Per-pattern threshold overrides: loss_pct, p99, p999 | §5.3.3 | [x] |
| C5 | Default rate values: events=100, events_store=100, queues=50, rpc=20 | §5.3.2 | [x] |
| C6 | Default loss thresholds: events=5.0%, others=0.0% | §5.3.2 | [x] |
| C7 | `warmup_duration` mode-dependent default (60s benchmark, 0s soak) | §5.3.2 | [x] |
| C8 | `run_id` auto-generation (8-char UUID prefix) | §5.3.2 | [x] |
| C9 | Full validation per §5.3.4 | §5.3.4 | [x] |
| C10 | `visibility_seconds` omitted from API queue config | §5.3.2, §2.1 | N/A |
| C12 | `poll_wait_timeout_seconds` → ms for Queue Stream, seconds for Queue Simple | §5.3.2 | [x] |
| C13 | `max_duration` safety cap (default 168h) | §5.3.2 | [x] |

---

## 13.5 Run Data & Metrics (REST API)

| # | Requirement | Spec Ref | C# |
|---|------------|----------|:--:|
| M1 | Per-run REST counters (reset on new run) | §8.2 | [x] |
| M2 | Pattern-level aggregates: sent, received, lost, duplicated, etc. | §5.5 | [x] |
| M3 | Per-producer metrics: id, sent, errors, actual_rate, latency | §5.5 | [x] |
| M4 | Per-consumer metrics: id, received, lost, duplicated, corrupted, errors, latency | §5.5 | [x] |
| M5 | Per-sender RPC metrics: id, sent, responses_success/timeout/error, actual_rate, latency | §5.5 | [x] |
| M6 | Per-responder RPC metrics: id, responded, errors | §5.5 | [x] |
| M7 | `actual_rate` = 30-second sliding average | §5.5.1 | [x] |
| M8 | `peak_rate` = highest 10-second window | §5.5.1 | [x] |
| M9 | `bytes_sent` / `bytes_received` per pattern | §5.5.1 | [x] |
| M10 | `unconfirmed` count: Events Store only | §5.5.1 | [x] |
| M11 | Live resource metrics: rss_mb, baseline_rss_mb, memory_growth_factor, active_workers | §5.5 | [x] |
| M12 | Totals aggregation: RPC success→received, timeout+error→lost | §5.6 | [x] |
| M13 | `out_of_order` included in totals | §5.6 | [x] |
| M14 | `resources` naming: live=rss_mb/active_workers, report=peak_rss_mb/peak_workers | §5.5 | [x] |

---

## 13.6 Report & Verdict

| # | Requirement | Spec Ref | C# |
|---|------------|----------|:--:|
| R1 | Report available after stopped/error, until next run. 404 otherwise. | §5.8 | [x] |
| R2 | Error-from-startup report: verdict=FAILED, `startup` check | §5.8.3 | [x] |
| R3 | `all_patterns_enabled` boolean flag | §5.8.2 | [x] |
| R4 | `warnings` array: "Not all patterns enabled" when patterns disabled | §5.8.1 | [x] |
| R5 | `peak_rate` per pattern in report | §5.8.2 | [x] |
| R6 | `avg_rate` per pattern in report (lifetime) | §5.8.2 | [x] |
| R7 | Worker-level breakdown in report with avg_rate + latency | §5.8.2 | [x] |
| R8 | Verdict checks as map: keys `"name:pattern"` for per-pattern | §5.8.1 | [x] |
| R9 | Check fields: `passed`, `threshold`, `actual`, `advisory` (default false) | §5.8.1 | [x] |
| R10 | Normative check names: message_loss, duplication, corruption, p99_latency, p999_latency, throughput, error_rate, memory_stability, memory_trend, downtime, startup | §5.8.1 | [x] |
| R11 | `duplication` checks: pub/sub+queue only (not RPC) | §5.8.1 | [x] |
| R12 | `error_rate` checks per pattern (errors/(sent+received)*100) | §5.8.1 | [x] |
| R13 | `throughput` check: global min across patterns, avg_rate vs target. Soak only. | §5.8.1 | [x] |
| R14 | `memory_trend` advisory: formula `1.0 + (max_factor-1.0)*0.5`, advisory=true | §5.8.1 | [x] |
| R15 | `PASSED_WITH_WARNINGS` logic | §5.8.1 | [x] |
| R16 | Memory baseline: 5min/1min/<1min with advisory for short runs | §5.8.1 | [x] |
| R17 | Per-pattern loss checks using pattern-specific thresholds | §5.8 | [x] |
| R18 | Per-pattern latency checks (p99, p999) using pattern thresholds | §5.8 | [x] |
| R19 | Verdict result: PASSED / PASSED_WITH_WARNINGS / FAILED | §5.8.1 | [x] |

---

## 13.7 Startup Config & CLI

| # | Requirement | Spec Ref | C# |
|---|------------|----------|:--:|
| S1 | `BURNIN_METRICS_PORT` / `metrics.port` (default 8888) | §2.1 | [x] |
| S2 | `BURNIN_LOG_FORMAT` / `logging.format` | §2.1 | [x] |
| S3 | `BURNIN_LOG_LEVEL` / `logging.level` | §2.1 | [x] |
| S4 | `BURNIN_CORS_ORIGINS` / `cors.origins` (default `*`) | §2.1, §7 | [x] |
| S5 | `BURNIN_BROKER_ADDRESS` / `broker.address` (default localhost:50000) | §2.1 | [x] |
| S6 | `BURNIN_CLIENT_ID_PREFIX` / `broker.client_id_prefix` | §2.1 | [x] |
| S7 | `BURNIN_RECONNECT_INTERVAL` (with 0-25% jitter) | §2.1 | [x] |
| S8 | `BURNIN_RECONNECT_MAX_INTERVAL` | §2.1 | [x] |
| S9 | `BURNIN_RECONNECT_MULTIPLIER` | §2.1 | [x] |
| S10 | `BURNIN_REPORT_OUTPUT_FILE` | §2.1 | [x] |
| S11 | `BURNIN_SDK_VERSION` (auto-detect fallback) | §2.1 | [x] |
| S13 | `--cleanup-only` CLI mode | §2.2 | [x] |
| S14 | `--validate-config` CLI mode | §2.2 | [x] |

---

## Architecture Changes Made

| File | Change Type | Description |
|------|-------------|-------------|
| `RunState.cs` | **New** | RunState + PatternState enums with transition helpers |
| `ApiModels.cs` | **New** | API config POJOs, validation, translation to internal config |
| `HttpServer.cs` | **Rewritten** | All spec endpoints, CORS middleware, POST handling, legacy aliases |
| `Program.cs` | **Rewritten** | Boot into idle, HTTP server first, SIGTERM/SIGINT handling |
| `Engine.cs` | **Rewritten** | State machine, startable/stoppable via API, all getter methods |
| `Config.cs` | **Modified** | Added CORS config, starting_timeout_seconds, enabled patterns |
| `Report.cs` | **Modified** | Per-pattern verdict checks with map keys, memory_trend advisory, startup check |
| `Metrics.cs` | **Modified** | PreInitialize() for zero-value seeding, per-worker labels |
| `Workers/BaseWorker.cs` | **Modified** | Per-worker stat tracking, bytes counters |
| `PeakRate.cs` | **Modified** | SlidingRateTracker for 30s average |
