# Performance Characteristics

## Throughput & Latency

> Baseline numbers from BenchmarkDotNet, loopback (localhost), .NET 8.0, single KubeMQ node.
> See `benchmarks/README.md` for full methodology and how to reproduce.

| Scenario | Payload | Metric | Baseline |
|----------|---------|--------|----------|
| Event publish throughput | 1KB | msg/sec | TBD |
| Event publish latency | 1KB | p50 / p99 | TBD |
| Queue roundtrip (send→poll→ack) | 1KB | p50 / p99 | TBD |
| Connection setup (cold start) | N/A | ms | TBD |

> **Note:** Update these numbers after running benchmarks on your target hardware.

## Tuning Guidance

### When to Use Batching

Queue `SendQueueMessagesAsync` accepts multiple messages and sends them in a
single gRPC call. Use batching when:

- You have multiple messages destined for the same queue
- Throughput is more important than per-message latency
- Messages can tolerate batch-level failure granularity

**Optimal batch sizes:**

| Batch Size | Use Case |
|-----------|----------|
| 1 | Low-latency, per-message acknowledgment required |
| 10–100 | Balanced throughput/latency for most workloads |
| 100–1000 | High-throughput ingestion, bulk processing |

### Connection Sharing

A single `KubeMQClient` instance multiplexes all operations over one gRPC
channel. Always reuse the client:

```csharp
// Register as singleton in DI
builder.Services.AddKubeMQ(opts => opts.Address = "server:50000");

// Or create once in application lifetime
await using var client = new KubeMQClient(options);
```

For high-concurrency scenarios, the gRPC channel automatically supports
HTTP/2 multiplexing, allowing many concurrent streams over a single
TCP connection.

### Stream Management

- Close subscription enumerators when no longer needed (exiting `await foreach`
  cancels the stream)
- Use `CancellationToken` to control subscription lifetime
- Do not block inside `await foreach` — process messages asynchronously or
  offload to a background channel

## Known Limitations

| Limit | Default | Configurable |
|-------|---------|-------------|
| Max send message size | 100 MB | `KubeMQClientOptions.MaxSendSize` |
| Max receive message size | 100 MB | `KubeMQClientOptions.MaxReceiveSize` |
| Max concurrent HTTP/2 streams per connection | ~100 (gRPC default) | gRPC auto-manages |
| Default operation timeout | 5s | `KubeMQClientOptions.DefaultTimeout` |
| Connection timeout | 10s | `KubeMQClientOptions.ConnectionTimeout` |

### Memory Considerations

- Message bodies use `ReadOnlyMemory<byte>` — no defensive copies on construction
- Proto serialization copies the body once into the gRPC send buffer via
  `ByteString.CopyFrom`
- Large messages (> 1 MB) should be sent individually, not batched, to avoid
  large contiguous allocations
- The SDK does NOT pool buffers by default; buffer pooling is added only when
  benchmarks demonstrate allocation pressure (per GS guidance)
- Record types (`EventMessage`, `QueueMessage`, etc.) are immutable — the same
  instance can be safely reused across multiple sends with no risk of mutation bugs

## Performance Tips

### 1. Reuse the Client Instance

Create one `KubeMQClient` and share it across your application. Each client
holds a persistent gRPC channel — creating a new client per operation wastes
time on TCP/TLS/HTTP2 setup.

```csharp
// ✅ Create once
await using var client = new KubeMQClient(options);

// ✅ In ASP.NET Core, register as singleton
builder.Services.AddKubeMQ(opts => opts.Address = "server:50000");

// ❌ Don't create per request
app.MapPost("/send", async () =>
{
    await using var client = new KubeMQClient(options); // Expensive!
    await client.PublishEventAsync(msg);
});
```

### 2. Use Batching for High-Throughput Queue Sends

When sending multiple queue messages, use `SendQueueMessagesAsync` to send
them in a single gRPC call instead of individual `SendQueueMessageAsync` calls.

```csharp
// ✅ Single gRPC call for all messages
var messages = items.Select(item => new QueueMessage
{
    Channel = "orders",
    Body = Serialize(item),
});
await client.SendQueueMessagesAsync(messages);

// ❌ N separate gRPC calls
foreach (var item in items)
{
    await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = "orders",
        Body = Serialize(item),
    });
}
```

### 3. Do Not Block Subscription Callbacks

When consuming messages via `await foreach`, avoid blocking the iteration
thread with synchronous I/O or long-running computation. This blocks the
subscription stream and prevents the SDK from receiving new messages.

```csharp
// ✅ Process asynchronously
await foreach (var msg in client.SubscribeToEventsAsync(sub, ct))
{
    await ProcessAsync(msg);  // Async, non-blocking
}

// ❌ Blocking the stream
await foreach (var msg in client.SubscribeToEventsAsync(sub, ct))
{
    Thread.Sleep(5000);           // Blocks subscription stream
    File.WriteAllBytes("f", ...); // Sync I/O blocks stream
}
```

For CPU-intensive processing, offload to a background channel:

```csharp
var channel = Channel.CreateBounded<EventReceived>(100);

// Producer: fast, non-blocking
_ = Task.Run(async () =>
{
    await foreach (var msg in client.SubscribeToEventsAsync(sub, ct))
        await channel.Writer.WriteAsync(msg, ct);
    channel.Writer.Complete();
});

// Consumer: can be slow
await foreach (var msg in channel.Reader.ReadAllAsync(ct))
{
    await HeavyProcessingAsync(msg);
}
```

### 4. Close Streams When Done

Subscription streams hold server-side resources. Always use `CancellationToken`
to control lifetime, and ensure streams are properly terminated.

```csharp
// ✅ CancellationToken controls lifetime
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
await foreach (var msg in client.SubscribeToEventsAsync(sub, cts.Token))
{
    await ProcessAsync(msg);
}

// ✅ Break exits cleanly
await foreach (var msg in client.SubscribeToEventsAsync(sub, ct))
{
    if (ShouldStop(msg))
        break;  // Stream cancelled, server resources released
    await ProcessAsync(msg);
}
```

Disposing the client also cancels all active subscriptions.
