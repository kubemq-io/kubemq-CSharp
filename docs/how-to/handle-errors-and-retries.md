# How To: Handle Errors and Retries

Understand the exception hierarchy, configure automatic retries, and handle connection failures.

## Exception Hierarchy

All SDK exceptions extend `KubeMQException`. Key types:

| Exception | Retryable | When |
|---|---|---|
| `KubeMQConnectionException` | Yes | Network failure, server unreachable |
| `KubeMQTimeoutException` | Yes | Operation exceeded deadline |
| `KubeMQOperationException` | Depends | Server returned an error |
| `KubeMQAuthenticationException` | No | Invalid/expired token |
| `KubeMQConfigurationException` | No | Invalid client options |
| `ObjectDisposedException` | No | Client already disposed |

## Catching and Inspecting Errors

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Exceptions;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "error-demo",
});
await client.ConnectAsync();

try
{
    await client.PublishEventAsync(new EventMessage
    {
        Channel = "demo.errors",
        Body = Encoding.UTF8.GetBytes("hello"),
    });
}
catch (KubeMQTimeoutException ex)
{
    Console.WriteLine($"Timeout: {ex.Message}");
}
catch (KubeMQConnectionException ex)
{
    Console.WriteLine($"Connection lost: {ex.Message}");
}
catch (KubeMQOperationException ex)
{
    Console.WriteLine($"Operation failed: {ex.Message}");
}
catch (KubeMQException ex)
{
    Console.WriteLine($"KubeMQ error [{ex.Category}]: {ex.Message}");
}
```

## Built-in Retry Policy

Configure automatic retries with exponential backoff:

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "retry-demo",
    Retry = new RetryPolicy
    {
        Enabled = true,
        MaxRetries = 5,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(30),
        BackoffMultiplier = 2.0,
    }
});
await client.ConnectAsync();

// All operations automatically retry on transient failures
await client.PublishEventAsync(new EventMessage
{
    Channel = "demo.retry",
    Body = Encoding.UTF8.GetBytes("auto-retried message"),
});
```

## Manual Retry Logic

For custom retry behavior beyond the built-in policy:

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Events;
using KubeMQ.Sdk.Exceptions;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "manual-retry-demo",
});
await client.ConnectAsync();

var message = new EventMessage
{
    Channel = "demo.important",
    Body = Encoding.UTF8.GetBytes("critical payload"),
};

const int maxAttempts = 3;
for (int attempt = 0; attempt < maxAttempts; attempt++)
{
    try
    {
        await client.PublishEventAsync(message);
        Console.WriteLine($"Sent on attempt {attempt + 1}");
        break;
    }
    catch (KubeMQConnectionException) when (attempt < maxAttempts - 1)
    {
        var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
        Console.WriteLine($"Retrying in {delay.TotalMilliseconds}ms...");
        await Task.Delay(delay);
    }
    catch (KubeMQTimeoutException) when (attempt < maxAttempts - 1)
    {
        var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
        Console.WriteLine($"Timeout — retrying in {delay.TotalMilliseconds}ms...");
        await Task.Delay(delay);
    }
}
```

## Auto-Reconnection

Configure reconnection behavior for dropped connections:

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Config;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",
    ClientId = "reconnect-demo",
    Reconnect = new ReconnectOptions
    {
        Enabled = true,
        MaxAttempts = 10,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(15),
        BackoffMultiplier = 2.0,
    }
});

client.StateChanged += (_, args) =>
{
    Console.WriteLine($"{args.PreviousState} → {args.CurrentState}");
};

await client.ConnectAsync();
```

## Graceful Shutdown

Ensure in-flight operations complete before disposal:

```csharp
await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "shutdown-demo",
    CallbackDrainTimeout = TimeSpan.FromSeconds(10),
});
await client.ConnectAsync();

// DisposeAsync waits for in-flight callbacks before closing
// The "await using" pattern handles this automatically
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `ObjectDisposedException` | Using client after `DisposeAsync` | Create a new client instance |
| Retry loops never succeed | Non-transient error (auth, validation) | Non-retryable errors are not retried by the SDK |
| `InvalidOperationException: Cannot connect` | Calling `ConnectAsync` twice | Check `client.State` before connecting |
| Callbacks stop firing | Subscription stream broke | SDK auto-reconnects; check `StateChanged` events |
