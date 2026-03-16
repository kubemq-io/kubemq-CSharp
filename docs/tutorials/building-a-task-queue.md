# Building a Task Queue with KubeMQ .NET SDK

In this tutorial, you'll build a reliable task queue using KubeMQ's `KubeMQClient`. Unlike events, queued messages persist until a consumer explicitly acknowledges them — making queues ideal for work that must not be lost.

## What You'll Build

An image-processing pipeline where a producer enqueues resize jobs and a worker pulls them, processes each, and acknowledges or rejects based on the outcome.

## Prerequisites

- **.NET 8+** installed (`dotnet --version`)
- **KubeMQ server** running on `localhost:50000` ([quickstart guide](https://docs.kubemq.io/getting-started/quick-start))

Create a new console project and add the SDK:

```bash
dotnet new console -n ImageProcessor
cd ImageProcessor
dotnet add package KubeMQ.Sdk
```

## Step 1 — Connect and Prepare

The `KubeMQClient` handles all messaging patterns. The `await using` pattern ensures the gRPC connection and any background streams are properly disposed, even if an exception occurs.

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "image-processor",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = "jobs.image-resize";
```

## Step 2 — Enqueue Tasks

Each queue message carries a byte array body and optional key-value tags. Tags are useful for routing or filtering without deserializing the body. We'll use `SendQueueMessageAsync` for individual messages.

```csharp
string[] images = { "photo-001.jpg", "photo-002.png", "photo-003.jpg", "INVALID_FILE", "photo-005.jpg" };

Console.WriteLine($"\n--- Enqueuing {images.Length} resize jobs ---");

foreach (var (image, index) in images.Select((img, i) => (img, i)))
{
    var result = await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes($"resize:{image}"),
        Tags = new Dictionary<string, string>
        {
            ["width"] = "800",
            ["format"] = "webp",
            ["priority"] = index == 0 ? "high" : "normal"
        }
    });

    Console.WriteLine($"  Enqueued: {image} (id={result.MessageId})");
}
```

We include `INVALID_FILE` deliberately — this lets us demonstrate rejection handling in the next step. In real systems, workers still need to handle malformed input gracefully.

## Step 3 — Receive and Process Messages

The `ReceiveQueueDownstreamAsync` method performs a transactional pull: it fetches messages and holds them in a "visibility lock" until you explicitly acknowledge or reject each one.

```csharp
Console.WriteLine("\n--- Processing jobs ---");

var downstream = await client.ReceiveQueueDownstreamAsync(
    channel: channel,
    maxItems: 10,
    waitTimeoutMs: 10000,
    autoAck: false);

var processed = 0;
var failed = 0;

foreach (var msg in downstream.Messages)
{
    var body = Encoding.UTF8.GetString(msg.Body.Span);
    var fileName = body.Replace("resize:", "");
    Console.WriteLine($"\n  Processing: {fileName}");

    if (msg.Tags.Count > 0)
    {
        Console.WriteLine($"    Tags: {string.Join(", ", msg.Tags.Select(t => $"{t.Key}={t.Value}"))}");
    }

    if (fileName.StartsWith("INVALID"))
    {
        await msg.RejectAsync();
        Console.WriteLine("    -> REJECTED (invalid file name)");
        failed++;
    }
    else
    {
        await SimulateResizeAsync(fileName);
        await msg.AckAsync();
        Console.WriteLine("    -> ACKNOWLEDGED (resize complete)");
        processed++;
    }
}
```

The `AckAsync()` / `RejectAsync()` pattern is the backbone of reliable messaging. An acknowledged message is permanently removed from the queue. A rejected message becomes available again for redelivery — or routes to a dead-letter queue if configured.

## Step 4 — Summary and Cleanup

```csharp
Console.WriteLine("\n--- Summary ---");
Console.WriteLine($"  Processed: {processed}");
Console.WriteLine($"  Rejected:  {failed}");

Console.WriteLine("\nImage processing pipeline shut down.");

async Task SimulateResizeAsync(string fileName)
{
    await Task.Delay(100);
}
```

## Complete Program

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queues;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "image-processor",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

var channel = "jobs.image-resize";

string[] images = { "photo-001.jpg", "photo-002.png", "photo-003.jpg", "INVALID_FILE", "photo-005.jpg" };

Console.WriteLine($"\n--- Enqueuing {images.Length} resize jobs ---");

foreach (var (image, index) in images.Select((img, i) => (img, i)))
{
    var result = await client.SendQueueMessageAsync(new QueueMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes($"resize:{image}"),
        Tags = new Dictionary<string, string>
        {
            ["width"] = "800",
            ["format"] = "webp",
            ["priority"] = index == 0 ? "high" : "normal"
        }
    });

    Console.WriteLine($"  Enqueued: {image} (id={result.MessageId})");
}

Console.WriteLine("\n--- Processing jobs ---");

var downstream = await client.ReceiveQueueDownstreamAsync(
    channel: channel,
    maxItems: 10,
    waitTimeoutMs: 10000,
    autoAck: false);

var processed = 0;
var failed = 0;

foreach (var msg in downstream.Messages)
{
    var body = Encoding.UTF8.GetString(msg.Body.Span);
    var fileName = body.Replace("resize:", "");
    Console.WriteLine($"\n  Processing: {fileName}");

    if (msg.Tags.Count > 0)
    {
        Console.WriteLine($"    Tags: {string.Join(", ", msg.Tags.Select(t => $"{t.Key}={t.Value}"))}");
    }

    if (fileName.StartsWith("INVALID"))
    {
        await msg.RejectAsync();
        Console.WriteLine("    -> REJECTED (invalid file name)");
        failed++;
    }
    else
    {
        await SimulateResizeAsync(fileName);
        await msg.AckAsync();
        Console.WriteLine("    -> ACKNOWLEDGED (resize complete)");
        processed++;
    }
}

Console.WriteLine("\n--- Summary ---");
Console.WriteLine($"  Processed: {processed}");
Console.WriteLine($"  Rejected:  {failed}");

Console.WriteLine("\nImage processing pipeline shut down.");

async Task SimulateResizeAsync(string fileName)
{
    await Task.Delay(100);
}
```

## Expected Output

```
Connected to KubeMQ server

--- Enqueuing 5 resize jobs ---
  Enqueued: photo-001.jpg (id=a1b2c3d4-...)
  Enqueued: photo-002.png (id=e5f6g7h8-...)
  Enqueued: photo-003.jpg (id=i9j0k1l2-...)
  Enqueued: INVALID_FILE (id=m3n4o5p6-...)
  Enqueued: photo-005.jpg (id=q7r8s9t0-...)

--- Processing jobs ---

  Processing: photo-001.jpg
    Tags: width=800, format=webp, priority=high
    -> ACKNOWLEDGED (resize complete)

  Processing: photo-002.png
    Tags: width=800, format=webp, priority=normal
    -> ACKNOWLEDGED (resize complete)

  Processing: photo-003.jpg
    Tags: width=800, format=webp, priority=normal
    -> ACKNOWLEDGED (resize complete)

  Processing: INVALID_FILE
    Tags: width=800, format=webp, priority=normal
    -> REJECTED (invalid file name)

  Processing: photo-005.jpg
    Tags: width=800, format=webp, priority=normal
    -> ACKNOWLEDGED (resize complete)

--- Summary ---
  Processed: 4
  Rejected:  1

Image processing pipeline shut down.
```

## Error Handling

| Error | Cause | Fix |
|-------|-------|-----|
| `No messages received` | Queue is empty or timeout too short | Increase `waitTimeoutMs` or check that messages were enqueued |
| `SendQueueMessageAsync fails` | Channel doesn't exist or server issue | Verify server is running and channel name is correct |
| `Message redelivered` | Message was rejected, not acknowledged | Configure a dead-letter queue to capture repeated failures |

For production workers, use a continuous polling loop with cancellation:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    try
    {
        var batch = await client.ReceiveQueueDownstreamAsync(
            channel: channel,
            maxItems: 10,
            waitTimeoutMs: 5000,
            autoAck: false);

        foreach (var msg in batch.Messages)
        {
            try
            {
                await ProcessMessageAsync(msg);
                await msg.AckAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Processing failed: {ex.Message}");
                await msg.RejectAsync();
            }
        }
    }
    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
    {
        Console.WriteLine($"Poll error: {ex.Message}, retrying in 3s...");
        await Task.Delay(3000, stoppingToken);
    }
}
```

## Next Steps

- **[Getting Started with Events](getting-started-events.md)** — fire-and-forget real-time messaging
- **[Request-Reply with Commands](request-reply-with-commands.md)** — synchronous command execution
- **Delayed Messages** — schedule tasks for future delivery
- **Dead-Letter Queues** — automatically capture messages that fail repeatedly
