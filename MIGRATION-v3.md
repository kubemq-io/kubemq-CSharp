# Migration Guide: v2 → v3

This guide covers all breaking changes in the KubeMQ .NET SDK v3 and provides
step-by-step instructions for upgrading from v2.

## Step 1: Update the Package

```bash
dotnet remove package KubeMQ.SDK.csharp
dotnet add package KubeMQ.SDK.CSharp
```

The package name changed from `KubeMQ.SDK.csharp` to `KubeMQ.SDK.CSharp`.

## Step 2: Update Namespaces

Find and replace all namespace references:

| v2 Namespace | v3 Namespace |
|-------------|-------------|
| `KubeMQ.SDK.csharp.PubSub.Events` | `KubeMQ.Sdk.Events` |
| `KubeMQ.SDK.csharp.PubSub.EventsStore` | `KubeMQ.Sdk.EventsStore` |
| `KubeMQ.SDK.csharp.Queues` | `KubeMQ.Sdk.Queues` |
| `KubeMQ.SDK.csharp.CQ.Commands` | `KubeMQ.Sdk.Commands` |
| `KubeMQ.SDK.csharp.CQ.Queries` | `KubeMQ.Sdk.Queries` |
| `KubeMQ.SDK.csharp.Common` | `KubeMQ.Sdk.Client` |
| `KubeMQ.SDK.csharp.Config` | `KubeMQ.Sdk.Config` |

## Step 3: Update Client Creation

### Before (v2):

```csharp
using KubeMQ.SDK.csharp.PubSub.Events;

var client = new EventsClient();
var connectResult = client.Connect(new Configuration
{
    Address = "localhost:50000",
    ClientId = "my-client"
}, new CancellationTokenSource());

if (!connectResult.IsSuccess)
{
    Console.WriteLine($"Connect failed: {connectResult.ErrorMessage}");
    return;
}
```

### After (v3):

```csharp
using KubeMQ.Sdk.Client;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",    // Default — can omit for localhost
    ClientId = "my-client"         // Optional — auto-generated if omitted
});
await client.ConnectAsync();
```

**Key changes:**
- Single `KubeMQClient` replaces `EventsClient`, `QueuesClient`, `CommandsClient`, `QueriesClient`
- `await using` for automatic resource cleanup (`IAsyncDisposable`)
- `ConnectAsync()` throws on failure instead of returning `Result`
- `Configuration` replaced by `KubeMQClientOptions`

## Step 4: Update Error Handling

### Before (v2):

```csharp
var result = await client.Send(myEvent);
if (!result.IsSuccess)
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
}
```

### After (v3):

```csharp
try
{
    await client.PublishEventAsync(myEvent);
}
catch (KubeMQTimeoutException ex)
{
    Console.WriteLine($"Timeout: {ex.Message}");
}
catch (KubeMQConnectionException ex)
{
    Console.WriteLine($"Not connected: {ex.Message}");
}
catch (KubeMQException ex)
{
    Console.WriteLine($"Error [{ex.ErrorCode}]: {ex.Message}");
}
```

**Key changes:**
- `Result` pattern removed entirely — use try/catch
- Typed exceptions enable granular error handling
- `IsRetryable` property indicates whether retry is appropriate
- `ErrorCode` and `Category` for programmatic error handling

## Step 5: Update Event Publishing

### Before (v2):

```csharp
var myEvent = new Event()
    .SetChannel("events")
    .SetBody(Encoding.UTF8.GetBytes("hello"))
    .SetMetadata("source=app");

var result = await client.Send(myEvent);
```

### After (v3):

```csharp
await client.PublishEventAsync(new EventMessage
{
    Channel = "events",
    Body = Encoding.UTF8.GetBytes("hello"),
    Tags = new Dictionary<string, string> { ["source"] = "app" }
});
```

**Key changes:**
- Fluent `SetX()` builders replaced by `init` property initializers
- `Metadata` string replaced by `Tags` dictionary
- `Body` is `ReadOnlyMemory<byte>` (accepts `byte[]` implicitly)
- `Send()` renamed to `PublishEventAsync()`

## Step 6: Update Subscriptions

### Before (v2):

```csharp
var subscription = new EventsSubscription()
    .SetChannel("events")
    .SetOnReceive(msg =>
    {
        Console.WriteLine($"Got: {Encoding.UTF8.GetString(msg.Body)}");
    })
    .SetOnError(err =>
    {
        Console.WriteLine($"Error: {err.Message}");
    });

var result = client.Subscribe(subscription);
Thread.Sleep(60000); // Wait for messages
```

### After (v3):

```csharp
await foreach (var msg in client.SubscribeToEventsAsync(
    new EventsSubscription { Channel = "events" }))
{
    Console.WriteLine($"Got: {Encoding.UTF8.GetString(msg.Body.Span)}");
}
```

**Key changes:**
- Callback-based `SetOnReceive` replaced by `await foreach` (`IAsyncEnumerable<T>`)
- `Thread.Sleep` no longer needed — `await foreach` blocks naturally
- Error handling via standard try/catch around the `await foreach`
- Cancellation via `CancellationToken` parameter

## Step 7: Update Queue Operations

### Before (v2):

```csharp
var msg = new Message()
    .SetChannel("tasks")
    .SetBody(Encoding.UTF8.GetBytes("task data"));

var response = await queuesClient.Send(msg);
```

### After (v3):

```csharp
var result = await client.SendQueueMessageAsync(new QueueMessage
{
    Channel = "tasks",
    Body = Encoding.UTF8.GetBytes("task data"),
    DelaySeconds = 5,            // Optional: delayed delivery
    MaxReceiveCount = 3,         // Optional: DLQ after 3 failures
    MaxReceiveQueue = "tasks-dlq" // Optional: DLQ channel
});
```

### Receiving Queue Messages (v3):

```csharp
var response = await client.PollQueueAsync(new QueuePollRequest
{
    Channel = "tasks",
    MaxMessages = 10,
    WaitTimeoutSeconds = 10,
    VisibilitySeconds = 30
});

foreach (var msg in response.Messages)
{
    await ProcessAsync(msg);
    await msg.AckAsync();         // Acknowledge
    // or: await msg.RejectAsync();    // Reject
    // or: await msg.RequeueAsync();   // Requeue
}
```

## Step 8: Update Configuration for Timeouts

### v3 Default Timeout

v3 introduces a **5-second default timeout** on all operations. If your server has
high latency or your command/query handlers take longer than 5 seconds, you must
increase the timeout:

```csharp
var client = new KubeMQClient(new KubeMQClientOptions
{
    DefaultTimeout = TimeSpan.FromSeconds(30)
});
```

## Breaking Changes Summary

| Change | v2 | v3 | Action Required |
|--------|-----|-----|-----------------|
| Package name | `KubeMQ.SDK.csharp` | `KubeMQ.SDK.CSharp` | Update package reference |
| Namespace root | `KubeMQ.SDK.csharp` | `KubeMQ.Sdk` | Find/replace |
| Client classes | 4 separate clients | Single `KubeMQClient` | Rewrite client creation |
| Error handling | `Result.IsSuccess` | try/catch exceptions | Replace all Result checks |
| Configuration | `Configuration` + fluent | `KubeMQClientOptions` + init | Rewrite config |
| Resource cleanup | Manual `Close()` | `await using` / `IAsyncDisposable` | Add `using` |
| Subscriptions | Callbacks + `Thread.Sleep` | `IAsyncEnumerable<T>` | Rewrite subscribe loops |
| Message body | `byte[]` | `ReadOnlyMemory<byte>` | Use `.Span` for access |
| Metadata | `string Metadata` | `Dictionary<string,string> Tags` | Update field name |
| Default timeout | None | 5 seconds | Set explicit timeout if needed |
| gRPC transport | `Grpc.Core` (deprecated) | `Grpc.Net.Client` | No action (internal) |
| Target framework | `net5.0+` / `netstandard2.0` | `net8.0+` | Update project TFM |

## Need Help?

- [Troubleshooting Guide](TROUBLESHOOTING.md) — solutions to common issues
- [API Reference](https://kubemq-io.github.io/kubemq-CSharp/) — full API documentation
- [Examples](examples/) — runnable code for all messaging patterns
- [GitHub Issues](https://github.com/kubemq-io/kubemq-CSharp/issues) — report bugs or request features
