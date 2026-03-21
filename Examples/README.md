# KubeMQ .NET SDK Examples

Self-contained, runnable examples for all KubeMQ messaging patterns.

## Prerequisites

- .NET 8.0 or later
- KubeMQ server running on `localhost:50000`:
  ```bash
  docker run -d -p 50000:50000 kubemq/kubemq-community:latest
  ```

## Running an Example

```bash
cd examples/Events/Events.BasicPubSub
dotnet run
```

## Examples

### Events (Pub/Sub)

| Example | Description |
|---------|-------------|
| [Events.BasicPubSub](Events/Events.BasicPubSub/) | Publish and subscribe to events on a channel |
| [Events.WildcardSubscription](Events/Events.WildcardSubscription/) | Subscribe using wildcard channel patterns |
| [Events.MultipleSubscribers](Events/Events.MultipleSubscribers/) | Multiple subscribers with group load balancing |

### Events Store (Persistent Pub/Sub)

| Example | Description |
|---------|-------------|
| [EventsStore.PersistentPubSub](EventsStore/EventsStore.PersistentPubSub/) | Publish and subscribe with server-side persistence |
| [EventsStore.ReplayFromSequence](EventsStore/EventsStore.ReplayFromSequence/) | Replay events starting from a sequence number |
| [EventsStore.ReplayFromTime](EventsStore/EventsStore.ReplayFromTime/) | Replay events from a specific timestamp |

### Queues

| Example | Description |
|---------|-------------|
| [Queues.SendReceive](Queues/Queues.SendReceive/) | Basic queue send and receive with acknowledgment |
| [Queues.AckReject](Queues/Queues.AckReject/) | Acknowledge, reject, and requeue messages |
| [Queues.DeadLetterQueue](Queues/Queues.DeadLetterQueue/) | Configure dead letter queue for failed messages |
| [Queues.DelayedMessages](Queues/Queues.DelayedMessages/) | Send messages with delivery delay |
| [Queues.Batch](Queues/Queues.Batch/) | Send and receive batches of queue messages |


### Commands (RPC)

| Example | Description |
|---------|-------------|
| [Commands.SendCommand](Commands/Commands.SendCommand/) | Send a command and wait for execution confirmation |
| [Commands.HandleCommand](Commands/Commands.HandleCommand/) | Subscribe to and handle incoming commands |

### Queries (RPC)

| Example | Description |
|---------|-------------|
| [Queries.SendQuery](Queries/Queries.SendQuery/) | Send a query and receive a data response |
| [Queries.HandleQuery](Queries/Queries.HandleQuery/) | Subscribe to and respond to incoming queries |
| [Queries.CachedResponse](Queries/Queries.CachedResponse/) | Query with server-side response caching |

### Configuration

| Example | Description |
|---------|-------------|
| [Config.TlsSetup](Config/Config.TlsSetup/) | Connect using TLS encryption |
| [Config.MtlsSetup](Config/Config.MtlsSetup/) | Connect using mutual TLS (client certificate) |
| [Config.TokenAuth](Config/Config.TokenAuth/) | Connect with JWT token authentication |
| [Config.CustomTimeouts](Config/Config.CustomTimeouts/) | Configure operation timeouts and retry policy |

### Observability

| Example | Description |
|---------|-------------|
| [Observability.OpenTelemetry](Observability/Observability.OpenTelemetry/) | Set up OpenTelemetry tracing and metrics export |
