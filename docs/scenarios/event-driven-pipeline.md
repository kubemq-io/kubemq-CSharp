# Building an Event-Driven Processing Pipeline

This guide walks through a multi-stage processing pipeline that combines KubeMQ events for real-time fan-out with queues for reliable, exactly-once delivery.

## Architecture

```
┌──────────┐    events     ┌─────────────┐    queue     ┌──────────┐
│ Producer │──────────────▶│  Processor  │─────────────▶│  Output  │
│ (ingest) │  (fan-out)    │  (transform)│  (reliable)  │ (persist)│
└──────────┘               └─────────────┘              └──────────┘
```

1. **Producer** publishes raw data as events on a pub/sub channel.
2. **Processor** subscribes to events, transforms each payload, and enqueues results into a queue.
3. **Output** worker pulls from the queue with ack/nack semantics to guarantee delivery.

This separation lets you scale each stage independently. Events handle real-time fan-out while queues provide backpressure and delivery guarantees.

## Prerequisites

- KubeMQ server on `localhost:50000`
- `dotnet add package KubeMQ.Sdk`

## Stage 1 — Event Producer

The producer ingests raw data and publishes it as events. Multiple subscribers can receive each event.

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using System.Text;

async Task RunProducer(KubeMQClient client)
{
    string[] orders = {
        """{"id":"ORD-1","item":"widget","qty":5}""",
        """{"id":"ORD-2","item":"gadget","qty":2}""",
        """{"id":"ORD-3","item":"gizmo","qty":10}""",
    };

    await using var stream = await client.CreateEventStreamAsync(
        onError: ex => Console.WriteLine($"[Producer] Stream error: {ex.Message}"));

    foreach (var order in orders)
    {
        await stream.SendAsync(new EventMessage
        {
            Channel = "pipeline.ingest",
            Body = Encoding.UTF8.GetBytes(order),
        }, "pipeline-producer");
        Console.WriteLine($"[Producer] Published: {order}");
    }
    await stream.CloseAsync();
}
```

## Stage 2 — Event Processor

The processor subscribes to events, transforms payloads, and enqueues enriched results for reliable downstream consumption.

```csharp
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Queues;
using System.Text.Json;

async Task RunProcessor(KubeMQClient client, CancellationToken ct)
{
    await foreach (var ev in client.SubscribeToEventsAsync(
        new EventsSubscription { Channel = "pipeline.ingest" }, ct))
    {
        var body = Encoding.UTF8.GetString(ev.Body.Span);
        Console.WriteLine($"[Processor] Received: {body}");

        var enriched = JsonSerializer.Serialize(new
        {
            original = JsonDocument.Parse(body).RootElement,
            processed_at = DateTime.UtcNow.ToString("o"),
        });

        await client.SendQueueMessageAsync(new QueueMessage
        {
            Channel = "pipeline.output",
            Body = Encoding.UTF8.GetBytes(enriched),
        });
        Console.WriteLine("[Processor] Enqueued for output");
    }
}
```

## Stage 3 — Output Worker

The output worker pulls from the queue with exactly-once semantics. Failed messages remain on the queue for retry.

```csharp
async Task RunOutputWorker(KubeMQClient client)
{
    var downstream = await client.ReceiveQueueDownstreamAsync(
        channel: "pipeline.output",
        maxItems: 10,
        waitTimeoutMs: 5000,
        autoAck: false);

    Console.WriteLine($"[Output] Received {downstream.Messages.Count} messages:");
    foreach (var msg in downstream.Messages)
    {
        Console.WriteLine($"  → {Encoding.UTF8.GetString(msg.Body.Span)}");
        await msg.AckAsync();
    }
}
```

## Putting It Together

```csharp
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "pipeline-demo",
});
await client.ConnectAsync();

var cts = new CancellationTokenSource();
var processorTask = Task.Run(() => RunProcessor(client, cts.Token));

await Task.Delay(500);
await RunProducer(client);
await Task.Delay(2000);

await RunOutputWorker(client);

cts.Cancel();
```

## Error Handling

- **Producer failures**: Log and skip; events are fire-and-forget by design.
- **Processor failures**: If the queue send fails, the event is lost. Use events-store instead of events if you need replay capability.
- **Output failures**: Messages stay in the queue. Use dead-letter queues for messages that fail repeatedly.

## When to Use This Pattern

- Stream processing with decoupled stages
- Ingestion pipelines where throughput matters more than ordering
- Systems that need both real-time notifications and guaranteed delivery
