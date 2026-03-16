# Category 13: Performance

**Tier:** 2 (Should-have)
**Current Average Score:** 1.84 / 5.0
**Target Score:** 4.0+
**Weight:** 4%

## Purpose

SDK performance must be measurable, documented, and optimized. Users must understand throughput characteristics and how to tune for their workload.

---

## Requirements

### REQ-PERF-1: Published Benchmarks

Every SDK must include reproducible benchmarks measuring key performance metrics.

**Required benchmarks:**

| Benchmark | Payload Size | What to Measure |
|-----------|-------------|-----------------|
| Publish throughput | 1KB | Messages/sec |
| Publish latency | 1KB | p50, p99 |
| Queue roundtrip latency | 1KB | p50, p99 |
| Connection setup time | N/A | Time to first message |

**SHOULD (recommended, not required):**
- Multi-payload-size matrix (64B, 1KB, 64KB) for publish throughput
- Concurrent publishers scaling (1/10/100 publishers)
- Subscribe throughput (messages/sec)

**Acceptance criteria:**
- [ ] Benchmarks exist in the SDK repo (language-native benchmark framework)
- [ ] Benchmarks are runnable with a single command
- [ ] Results are documented in the repo (baseline numbers)
- [ ] Benchmark methodology is documented (hardware, server config, message count)

**Benchmark tools per language:**

| Language | Framework |
|----------|----------|
| Go | `testing.B` (built-in) |
| Java | JMH preferred; simpler timing-based benchmark acceptable if reproducible |
| C# | BenchmarkDotNet |
| Python | pytest-benchmark or custom timing |
| JS/TS | Vitest bench or custom timing |

### REQ-PERF-2: Connection Reuse

The SDK must reuse gRPC connections efficiently.

**Acceptance criteria:**
- [ ] A single Client instance uses one long-lived gRPC channel
- [ ] Multiple concurrent operations multiplex over the same channel
- [ ] Documentation advises against creating a Client per operation
- [ ] No per-operation connection overhead

### REQ-PERF-3: Efficient Serialization

Message construction and serialization must minimize allocations.

**Acceptance criteria:**
- [ ] Protobuf serialization uses the standard runtime (no custom serialization)
- [ ] Avoid unnecessary memory copies of message bodies
- [ ] Buffer pooling is recommended only when benchmarks demonstrate allocation pressure

### REQ-PERF-4: Batch Operations

For queue operations, batch send/receive must be supported and optimized.

**Acceptance criteria:**
- [ ] Batch operations use a single gRPC call (not N individual calls)
- [ ] Batch size is configurable

> **Note:** Batch send/receive API existence is specified in Category 08 (API Completeness).

### REQ-PERF-5: Performance Documentation

**Acceptance criteria:**
- [ ] README or separate doc includes performance characteristics
- [ ] Tuning guidance: when to use batching, optimal batch sizes, connection sharing
- [ ] Known limitations documented (max message size, max concurrent streams)

### REQ-PERF-6: Performance Tips Documentation

SDK documentation must include practical performance guidance.

**Acceptance criteria:**
- [ ] SDK documentation includes a "Performance Tips" section covering at minimum:
  1. Reuse the client instance (do not create per-operation)
  2. Use batching for high-throughput queue sends
  3. Do not block subscription callbacks
  4. Close streams when done

---

## What 4.0+ Looks Like

- Published benchmarks with baseline numbers for 4 core scenarios (publish throughput, publish latency, queue roundtrip, connection setup)
- Single connection handles all operations efficiently (gRPC multiplexing)
- Batch operations for queue send/receive with configurable batch size, using single gRPC calls
- Performance Tips documentation prevents common mistakes (client reuse, callback blocking, stream cleanup)
- Memory copies minimized; buffer pooling used only when benchmarks justify it
