# KubeMQ C# SDK — Performance Benchmarks

## Prerequisites

- .NET 8.0 SDK
- KubeMQ server (for integration benchmarks; not required for serialization, retry, and validation benchmarks)

## Running

```bash
# All benchmarks (requires KubeMQ server for integration benchmarks)
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*"

# Serialization only (no server needed)
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*Serialization*"

# Retry policy only (no server needed)
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*RetryPolicy*"

# Message validation only (no server needed)
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*MessageValidation*"

# Integration benchmarks (requires server)
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*Throughput*"
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*Latency*"
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*Roundtrip*"
dotnet run -c Release --project KubeMQ.Sdk.Benchmarks -- --filter "*ConnectionSetup*"
```

## Environment

Set `KUBEMQ_BENCH_ADDRESS` to override the default server address (`localhost:50000`).

## Methodology

- **Framework:** BenchmarkDotNet 0.14.x
- **Runtime:** .NET 8.0, Release mode, no debugger attached
- **Warmup:** 3 iterations
- **Measurement:** 10 iterations
- **GC:** Server GC, not forced between iterations
- **Payload:** Random bytes at specified sizes (default 1KB)
- **Server:** Single KubeMQ node, default configuration (integration benchmarks only)
- **Network:** Loopback (localhost) — benchmarks measure SDK overhead, not network
- **Metrics reported:** Mean, Median (p50), p99, Allocated bytes

## Benchmark Descriptions

| Benchmark | Payload | Metric | Server Required | Notes |
|-----------|---------|--------|-----------------|-------|
| SerializationBenchmarks | 1KB, 64KB | ns, bytes allocated | No | Proto encode/decode allocations |
| RetryPolicyBenchmarks | N/A | ns | No | Backoff delay calculation overhead |
| MessageValidationBenchmarks | 1KB | ns, bytes allocated | No | Message creation and validation overhead |
| PublishThroughput | 1KB | Messages/sec | Yes | Batch of 1000 events (const, not [Params]) |
| PublishLatency | 1KB | p50, p99 (μs) | Yes | Single event publish |
| QueueRoundtrip | 1KB | p50, p99 (μs) | Yes | Send → Poll(autoAck) |
| ConnectionSetup | N/A | ms | Yes | Constructor → Connect → Ping |

## Baseline Results

> Results captured on: [DATE], [HARDWARE], KubeMQ server [VERSION]
>
> These are reference numbers. Your results will vary based on hardware and server configuration.

| Benchmark | Mean | p50 | p99 | Allocated |
|-----------|------|-----|-----|-----------|
| Serialization Encode1Kb | TBD | — | — | TBD |
| Serialization Encode64Kb | TBD | — | — | TBD |
| Serialization Encode1Kb_ToBytes | TBD | — | — | TBD |
| Serialization Decode1Kb | TBD | — | — | TBD |
| RetryPolicy CalculateDelay | TBD | — | — | TBD |
| MessageValidation CreateEvent | TBD | — | — | TBD |
| MessageValidation ValidateEvent | TBD | — | — | TBD |
| PublishThroughput (1KB) | TBD | TBD | TBD | TBD |
| PublishLatency (1KB) | TBD | TBD | TBD | TBD |
| QueueRoundtrip (1KB) | TBD | TBD | TBD | TBD |
| ConnectionSetup | TBD | TBD | TBD | TBD |

> **Note:** Baseline numbers MUST be filled in after the first benchmark run and committed to the repository. Update these when major SDK changes affect performance.
