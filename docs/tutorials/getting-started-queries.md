# Getting Started with Queries in KubeMQ .NET SDK

In this tutorial, you'll build a request-reply system using KubeMQ's Queries. Queries are synchronous: the sender waits for a handler to process the request and return data. Use them when you need a response — for example, looking up a user by ID or fetching configuration.

## What You'll Build

A user-lookup service where a client sends a query (e.g., `get-user:42`) and a handler responds with JSON user data. The client blocks until the response arrives or the timeout expires.

## Prerequisites

- **.NET 8+** installed (`dotnet --version`)
- **KubeMQ server** running on `localhost:50000` ([quickstart guide](https://docs.kubemq.io/getting-started/quick-start))

Create two console projects — one for the handler, one for the sender:

```bash
dotnet new console -n UserLookupHandler
cd UserLookupHandler
dotnet add package KubeMQ.Sdk

# In another terminal:
dotnet new console -n UserLookupClient
cd UserLookupClient
dotnet add package KubeMQ.Sdk
```

## Step 1 — Connect to the KubeMQ Server

Both the handler and the sender use `KubeMQClient`. The `await using` pattern ensures the gRPC connection is properly torn down.

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "user-lookup-service",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");
```

## Step 2 — Subscribe to Queries (Handler)

The handler subscribes to a channel and processes incoming queries. `SubscribeToQueriesAsync` returns an `IAsyncEnumerable` — the idiomatic C# way to consume a stream of requests.

```csharp
var channel = "user.queries";
var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await foreach (var query in client.SubscribeToQueriesAsync(
    new QueriesSubscription { Channel = channel }, cts.Token))
{
    var body = Encoding.UTF8.GetString(query.Body.Span);
    Console.WriteLine($"[Handler] Received query: {body}");

    // Parse query and build response (e.g., lookup user by ID)
    var responseBody = Encoding.UTF8.GetBytes(
        "{\"id\":42,\"name\":\"Alice\",\"email\":\"alice@example.com\"}");

    await client.SendQueryResponseAsync(new QueryResponse
    {
        RequestId = query.RequestId,
        ReplyChannel = query.ReplyChannel!,
        Body = responseBody,
        Executed = true,
    });

    Console.WriteLine("  -> Responded with user data");
}
```

Each `QueryReceived` includes `RequestId` and `ReplyChannel` — you must pass these in a `QueryResponse` object to `SendQueryResponseAsync` so the response reaches the correct sender. Set `Executed = true` on success, or `Executed = false` with an `Error` on failure.

## Step 3 — Send a Query (Client)

The client sends a query and waits for the response. `SendQueryAsync` blocks until the handler responds or the timeout expires.

```csharp
var channel = "user.queries";

try
{
    var response = await client.SendQueryAsync(new QueryMessage
    {
        Channel = channel,
        Body = Encoding.UTF8.GetBytes("get-user:42"),
        TimeoutInSeconds = 10
    });

    Console.WriteLine($"Query executed: {response.Executed}");
    Console.WriteLine($"Response: {Encoding.UTF8.GetString(response.Body.Span)}");
}
catch (KubeMQTimeoutException)
{
    Console.WriteLine("Query timed out — no handler responded within 10 seconds");
}
catch (KubeMQOperationException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Query timed out — server reported: {ex.Message}");
}
```

**Important:** Start the handler *before* the client. If no handler is listening, the query will time out.

## Complete Programs

### Handler (run first)

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "user-lookup-handler",
});
await client.ConnectAsync();

Console.WriteLine("Waiting for queries on 'user.queries'...");

var channel = "user.queries";
var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await foreach (var query in client.SubscribeToQueriesAsync(
    new QueriesSubscription { Channel = channel }, cts.Token))
{
    var body = Encoding.UTF8.GetString(query.Body.Span);
    Console.WriteLine($"Received query: {body}");

    var responseBody = Encoding.UTF8.GetBytes(
        "{\"id\":42,\"name\":\"Alice\",\"email\":\"alice@example.com\"}");

    await client.SendQueryResponseAsync(new QueryResponse
    {
        RequestId = query.RequestId,
        ReplyChannel = query.ReplyChannel!,
        Body = responseBody,
        Executed = true,
    });

    Console.WriteLine("  -> Responded with user data");
}

Console.WriteLine("Done.");
```

### Client (run in a separate terminal)

```csharp
using KubeMQ.Sdk.Client;
using KubeMQ.Sdk.Queries;
using KubeMQ.Sdk.Exceptions;
using System.Text;

await using var client = new KubeMQClient(new KubeMQClientOptions
{
    ClientId = "user-lookup-client",
});
await client.ConnectAsync();

Console.WriteLine("Connected to KubeMQ server");

try
{
    var response = await client.SendQueryAsync(new QueryMessage
    {
        Channel = "user.queries",
        Body = Encoding.UTF8.GetBytes("get-user:42"),
        TimeoutInSeconds = 10
    });

    Console.WriteLine($"Query executed: {response.Executed}");
    Console.WriteLine($"Response: {Encoding.UTF8.GetString(response.Body.Span)}");
}
catch (KubeMQTimeoutException)
{
    Console.WriteLine("Query timed out — no handler responded within 10 seconds");
}
catch (KubeMQOperationException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"Query timed out — server reported: {ex.Message}");
}

Console.WriteLine("Done.");
```

## Expected Output

**Handler:**
```
Waiting for queries on 'user.queries'...
Received query: get-user:42
  -> Responded with user data
```

**Client:**
```
Connected to KubeMQ server
Query executed: True
Response: {"id":42,"name":"Alice","email":"alice@example.com"}
Done.
```

## Error Handling

Common issues and how to handle them:

| Error | Cause | Fix |
|-------|-------|-----|
| `Connection refused` | KubeMQ server not running | Start the server: `docker run -p 50000:50000 kubemq/kubemq` |
| `KubeMQTimeoutException` | No handler listening or handler too slow | Start the handler first; increase `TimeoutInSeconds` |
| `NullReferenceException` on `ReplyChannel` | Handler received malformed request | Validate `query.ReplyChannel` before building `QueryResponse` |
| `RpcException` | Network interruption | Catch and reconnect with `ConnectAsync()` |

To report failure from the handler, pass `executed: false` and an optional `errorMessage`:

```csharp
await client.SendQueryResponseAsync(new QueryResponse
{
    RequestId = query.RequestId,
    ReplyChannel = query.ReplyChannel!,
    Executed = false,
    Error = "User not found",
});
```

## Next Steps

- **[Request-Reply with Commands](request-reply-with-commands.md)** — fire-and-ack without response data
- **[Getting Started with Events](getting-started-events.md)** — fire-and-forget messaging
- **[Implementing CQRS with KubeMQ](../scenarios/microservice-cqrs.md)** — Queries in a CQRS architecture
