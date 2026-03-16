# Troubleshooting Guide

Common issues and solutions for the KubeMQ .NET SDK.

> **Note:** Error messages shown below are representative examples. Actual messages
> may vary slightly depending on the server version, network conditions, and .NET runtime.
> Match on the exception type (e.g., `KubeMQConnectionException`) rather than the exact text.

---

## Problem: Connection refused / timeout

**Error message:**
```
KubeMQConnectionException: Failed to connect to localhost:50000: Connection refused
```

**Cause:** The KubeMQ server is not running, not reachable at the configured address,
or a firewall is blocking port 50000.

**Solution:**
1. Verify the server is running:
   ```bash
   docker ps | grep kubemq
   ```
2. If not running, start it:
   ```bash
   docker run -d -p 50000:50000 kubemq/kubemq-community:latest
   ```
3. If running but not reachable, check the address in your client options:
   ```csharp
   var client = new KubeMQClient(new KubeMQClientOptions
   {
       Address = "your-server-host:50000"
   });
   ```
4. In Kubernetes, use the service DNS name:
   ```csharp
   Address = "kubemq-cluster.kubemq.svc.cluster.local:50000"
   ```

**See also:** [Configuration](https://github.com/kubemq-io/kubemq-CSharp#configuration) in README

---

## Problem: Authentication failed (invalid token)

**Error message:**
```
KubeMQAuthenticationException: Authentication failed for kubemq-server:50000
```

**Cause:** The JWT token is invalid, expired, or not provided when the server requires authentication.

**Solution:**
1. Verify the token is set:
   ```csharp
   var client = new KubeMQClient(new KubeMQClientOptions
   {
       Address = "kubemq-server:50000",
       AuthToken = "your-valid-jwt-token"
   });
   ```
2. Check token expiration — tokens are time-limited.
3. For dynamic token refresh, implement `ICredentialProvider`:
   ```csharp
   var client = new KubeMQClient(new KubeMQClientOptions
   {
       CredentialProvider = new MyTokenProvider()
   });
   ```

**See also:** [Token Auth Example](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Config/Config.TokenAuth)

---

## Problem: Authorization denied (insufficient permissions)

**Error message:**
```
KubeMQAuthenticationException: Authorization denied: insufficient permissions for channel 'orders'
```

**Cause:** The authenticated user does not have permission to perform the requested
operation on the specified channel.

**Solution:**
1. Check the server's ACL configuration for your client ID.
2. Verify the channel name is correct (names are case-sensitive).
3. Contact the KubeMQ administrator to grant the required permissions.

---

## Problem: Channel not found

**Error message:**
```
KubeMQOperationException: Channel 'orders' not found
```

**Cause:** The channel does not exist on the server and auto-creation is not enabled.

**Solution:**
1. Create the channel before publishing:
   ```csharp
   await client.CreateChannelAsync("orders", "queues");
   ```
2. Or enable auto-creation on the server.
3. Verify the channel type matches your operation (e.g., "events" vs "queues").

---

## Problem: Message too large

**Error message:**
```
KubeMQOperationException: Message size exceeds maximum allowed size
```

**Cause:** The message body exceeds the configured maximum message size (default 100 MB).

**Solution:**
1. Reduce the message body size by compressing or splitting the payload.
2. Increase the maximum size in the client:
   ```csharp
   var client = new KubeMQClient(new KubeMQClientOptions
   {
       MaxSendSize = 200 * 1024 * 1024,    // 200 MB
       MaxReceiveSize = 200 * 1024 * 1024,  // 200 MB
   });
   ```
3. The server must also be configured to accept larger messages.

---

## Problem: Timeout / deadline exceeded

**Error message:**
```
KubeMQTimeoutException: SendCommand to 'control' exceeded deadline of 00:00:05
```

**Cause:** The operation did not complete within the configured timeout. This can happen
due to server load, network latency, or slow command/query handlers.

**Solution:**
1. Increase the default timeout:
   ```csharp
   var client = new KubeMQClient(new KubeMQClientOptions
   {
       DefaultTimeout = TimeSpan.FromSeconds(30)
   });
   ```
2. Or set a per-operation timeout:
   ```csharp
   var response = await client.SendCommandAsync(new CommandMessage
   {
       Channel = "control",
       TimeoutInSeconds = 30,
       Body = Encoding.UTF8.GetBytes("restart")
   });
   ```
3. Investigate why the handler is slow (check server metrics, handler logs).

---

## Problem: Rate limiting / throttling

**Error message:**
```
KubeMQOperationException: Resource exhausted: rate limit exceeded
```

**Cause:** The server is throttling requests because the client is sending faster than
the server can process.

**Solution:**
1. The SDK retries throttled requests automatically with backoff.
2. If retries are exhausted, reduce the send rate.
3. Adjust retry policy for longer backoff:
   ```csharp
   var client = new KubeMQClient(new KubeMQClientOptions
   {
       Retry = new RetryPolicy
       {
           MaxRetries = 10,
           MaxBackoff = TimeSpan.FromSeconds(60)
       }
   });
   ```

---

## Problem: Internal server error

**Error message:**
```
KubeMQOperationException: Internal server error [Internal]
```

**Cause:** An unexpected error occurred on the KubeMQ server.

**Solution:**
1. Check the KubeMQ server logs for details.
2. If the error is transient, the SDK retries automatically.
3. If the error persists, restart the KubeMQ server and report the issue.

---

## Problem: TLS handshake failure

**Error message:**
```
KubeMQConnectionException: TLS handshake failed: certificate validation error
```

**Cause:** The TLS certificate is invalid, expired, self-signed without a trusted CA,
or the server name doesn't match the certificate.

**Solution:**
1. Verify the CA certificate path:
   ```csharp
   var client = new KubeMQClient(new KubeMQClientOptions
   {
       Address = "kubemq-server:50000",
       Tls = new TlsOptions
       {
           Enabled = true,
           CaFile = "/path/to/ca.pem"
       }
   });
   ```
2. For development only, skip certificate verification (NOT for production):
   ```csharp
   Tls = new TlsOptions
   {
       Enabled = true,
       InsecureSkipVerify = true  // Development only!
   }
   ```
3. Verify the certificate matches the server hostname. Override if needed:
   ```csharp
   Tls = new TlsOptions
   {
       Enabled = true,
       ServerNameOverride = "kubemq-server"
   }
   ```

**See also:** [TLS Setup Example](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Config/Config.TlsSetup),
[mTLS Setup Example](https://github.com/kubemq-io/kubemq-CSharp/tree/main/examples/Config/Config.MtlsSetup)

---

## Problem: No messages received (subscriber not getting messages)

**Error message:** No error — subscriber is connected but receives no messages.

**Cause:** Common causes include:
- Subscriber connected after the publisher sent messages (Events are not persisted)
- Channel name mismatch (case-sensitive)
- Group name mismatch — messages load-balanced to another subscriber
- Subscriber not connected yet when messages were published

**Solution:**
1. Verify channel names match exactly between publisher and subscriber.
2. If you need persistence, use Events Store instead of Events.
3. Ensure the subscriber is connected before publishing:
   ```csharp
   // Subscribe first
   var subscription = client.SubscribeToEventsAsync(
       new EventsSubscription { Channel = "my-channel" });

   // Then publish (from another client/process)
   await publisher.PublishEventAsync(new EventMessage
   {
       Channel = "my-channel",
       Body = Encoding.UTF8.GetBytes("hello")
   });
   ```
4. For Events Store, use `FromFirst` to replay all existing events:
   ```csharp
   await foreach (var msg in client.SubscribeToEventStoreAsync(
       new EventStoreSubscription
       {
           Channel = "my-channel",
           StartPosition = EventStoreStartPosition.FromFirst
       }))
   {
       Console.WriteLine(Encoding.UTF8.GetString(msg.Body.Span));
   }
   ```

---

## Problem: Queue message not acknowledged

**Error message:**
```
Message redelivered after visibility timeout expired
```

**Cause:** The message was received but not acknowledged within the visibility timeout.
The server redelivers the message to another consumer.

**Solution:**
1. Acknowledge the message after processing:
   ```csharp
   var response = await client.PollQueueAsync(new QueuePollRequest
   {
       Channel = "tasks",
       VisibilitySeconds = 30
   });

   foreach (var msg in response.Messages)
   {
       await ProcessAsync(msg);
       await msg.AckAsync();  // Must ack within 30 seconds
   }
   ```
2. If processing takes longer, extend the visibility:
   ```csharp
   await msg.ExtendVisibilityAsync(additionalSeconds: 30);
   ```
3. Increase the initial visibility timeout:
   ```csharp
   var response = await client.PollQueueAsync(new QueuePollRequest
   {
       Channel = "tasks",
       VisibilitySeconds = 120  // 2 minutes
   });
   ```

---

## gRPC Status Code Mapping

The SDK maps every gRPC status code to a typed `KubeMQException` subclass. This table
shows how each status code is classified, whether the SDK retries it automatically, and
the exception type you should catch.

| gRPC Status Code | SDK Error Code | Exception Type | Category | Retryable | Suggestion |
|---|---|---|---|---|---|
| `OK` | — | *(none — no exception thrown)* | — | — | — |
| `Cancelled` (client) | `Cancelled` | `OperationCanceledException` | Cancellation | No | Operation was cancelled by the caller. |
| `Cancelled` (server) | `Cancelled` | `KubeMQOperationException` | Transient | Yes | Server cancelled the operation. The SDK will retry automatically. |
| `Unknown` | `Unknown` | `KubeMQOperationException` | Transient | Yes | Unknown error — may be transient. Will retry once. |
| `InvalidArgument` | `InvalidArgument` | `KubeMQOperationException` | Validation | No | Check message format and field values. |
| `DeadlineExceeded` | `DeadlineExceeded` | `KubeMQTimeoutException` | Timeout | Yes | Increase timeout or check server load. |
| `NotFound` | `NotFound` | `KubeMQOperationException` | NotFound | No | Verify the channel/queue exists. Create it first if needed. |
| `AlreadyExists` | `AlreadyExists` | `KubeMQOperationException` | Validation | No | Resource already exists. |
| `PermissionDenied` | `PermissionDenied` | `KubeMQOperationException` | Authorization | No | Check ACL permissions for this channel. |
| `ResourceExhausted` | `ResourceExhausted` | `KubeMQOperationException` | Throttling | Yes | Server is rate-limiting. The SDK will retry with extended backoff. |
| `FailedPrecondition` | `FailedPrecondition` | `KubeMQOperationException` | Validation | No | Operation precondition not met. Check server state. |
| `Aborted` | `Aborted` | `KubeMQOperationException` | Transient | Yes | Transient conflict. The SDK will retry automatically. |
| `OutOfRange` | `OutOfRange` | `KubeMQOperationException` | Validation | No | Value out of acceptable range. |
| `Unimplemented` | `Unimplemented` | `KubeMQOperationException` | Fatal | No | Operation not supported by the server. Upgrade the server or SDK. |
| `Internal` | `Internal` | `KubeMQOperationException` | Fatal | No | Internal server error. If persistent, contact KubeMQ support. |
| `Unavailable` | `Unavailable` | `KubeMQConnectionException`* | Transient | Yes | Server temporarily unavailable. Check connectivity and firewall rules. |
| `DataLoss` | `DataLoss` | `KubeMQOperationException` | Fatal | No | Unrecoverable data loss. Contact KubeMQ support. |
| `Unauthenticated` | `AuthenticationFailed` | `KubeMQAuthenticationException` | Authentication | No | Verify auth token or TLS certificates. |

\* `Unavailable` may map to `KubeMQAuthenticationException` or `KubeMQConfigurationException`
if the underlying error is TLS-related (e.g., certificate validation failure, cipher negotiation failure).

**Source:** [`GrpcErrorMapper.cs`](https://github.com/kubemq-io/kubemq-CSharp/blob/main/src/KubeMQ.Sdk/Internal/Protocol/GrpcErrorMapper.cs)

---

## How to Enable Debug Logging

The SDK uses `Microsoft.Extensions.Logging` for structured diagnostics. By default, no
logs are emitted. To enable debug-level logging, pass an `ILoggerFactory` to the client options.

### Console App

```csharp
using Microsoft.Extensions.Logging;
using KubeMQ.Sdk.Client;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Debug)
        .AddConsole();
});

var client = new KubeMQClient(new KubeMQClientOptions
{
    Address = "localhost:50000",
    LoggerFactory = loggerFactory
});

await client.ConnectAsync();
```

### ASP.NET Core / Dependency Injection

When using `AddKubeMQ()`, the SDK automatically picks up the `ILoggerFactory` from
the DI container. Set the log level in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "KubeMQ.Sdk": "Debug"
    }
  }
}
```

### What to Expect

With `LogLevel.Debug` enabled, the SDK emits structured log messages including:

| Event ID | Level | Message |
|----------|-------|---------|
| 200 | Info | `Connected to {Address}` |
| 201 | Info | `Connection state changed: {PreviousState} -> {CurrentState}` |
| 220 | Info | `Reconnect attempt {Attempt} to {Address}, next retry in {Delay}` |
| 221 | Info | `Reconnected to {Address} after {Attempt} attempt(s)` |
| 400 | Debug | `Message sent to {Channel} in {DurationMs}ms` |
| 401 | Debug | `Message received from {Channel}` |
| 500 | Debug | `Retry attempt {Attempt}/{MaxAttempts} for {Operation}` |
| 501 | Error | `Retries exhausted for {Operation} on {Channel}` |

**Tip:** In production, use `LogLevel.Warning` to keep output minimal while still surfacing
connection issues and retries.
