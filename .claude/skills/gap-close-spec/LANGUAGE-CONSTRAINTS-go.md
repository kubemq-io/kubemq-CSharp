# Go Language Constraints for Spec Agents

**Purpose:** Language-specific pitfalls that spec agents MUST follow to prevent common errors in Go SDK specs.

**Scope:** GO-1 through GO-57. Organized by category. Priority tags: P0 (must), P1 (should), P2 (nice to have).

**K8s ecosystem references:** nats.go, client-go, grpc-go, go-redis, sarama, etcd client v3, controller-runtime.

---

## Table of Contents

1. [Syntax Rules](#syntax-rules) — GO-1, GO-2, GO-4
2. [Naming Rules](#naming-rules) — GO-6, GO-7, GO-8
3. [Error Handling](#error-handling) — GO-3, GO-5, GO-18, GO-19, GO-20, GO-21
4. [Context Propagation](#context-propagation) — GO-12, GO-22, GO-23
5. [Interface Design](#interface-design) — GO-24, GO-25
6. [Configuration & Options](#configuration--options) — GO-16, GO-26, GO-27, GO-28
7. [Concurrency](#concurrency-rules) — GO-9, GO-10, GO-11, GO-29, GO-30, GO-31, GO-32
8. [Dependency](#dependency-rules) — GO-13, GO-14
9. [Resource Cleanup](#resource-cleanup) — GO-33, GO-34
10. [Logging](#logging) — GO-35
11. [Testing](#testing) — GO-36, GO-37, GO-38
12. [gRPC-Specific](#grpc-specific) — GO-17, GO-39, GO-40, GO-41, GO-42, GO-43
13. [Observability](#observability) — GO-44, GO-45
14. [Module & Build](#module--build) — GO-15, GO-46, GO-47, GO-48, GO-49
15. [Common Pitfalls](#common-pitfalls) — GO-50, GO-51, GO-52, GO-53, GO-54, GO-55, GO-56, GO-57

---

## Syntax Rules

### GO-1: No constructors — use factory functions
Go has no constructors. Use `NewXxx()` factory functions.
- **WRONG:** `type Client struct { ... }` with implicit construction
- **RIGHT:** `func NewClient(opts ...Option) (*Client, error)` factory function

### GO-2: Exported vs unexported naming
Go uses capitalization for visibility, not access modifiers.
- Exported (public): `Client`, `SendMessage`, `ErrorCode`
- Unexported (private): `client`, `sendMessage`, `errorCode`
- All types in specs intended for public use MUST start with uppercase.
- Only export types that appear in public function signatures. Internal state machines, retry configs, and transport details stay unexported. See also: GO-55.

### GO-4: No generics before Go 1.18
If the SDK targets Go <1.18, do NOT use generics (`[T any]`). Use `interface{}` or concrete types instead.
- Check the `go.mod` file for the minimum Go version.
- If targeting 1.18+, generics are fine but avoid overuse — Go community prefers concrete types.
- See also: GO-46 for minimum Go version policy.

---

## Naming Rules

### GO-6: Standard library name collisions
These names exist in Go's standard library and should be avoided or prefixed:

| Name | Package | Risk |
|------|---------|------|
| `Context` | `context` | High — used everywhere |
| `Error` | `errors` | High — conflicts with error interface |
| `Logger` | `log` / `slog` | Medium |
| `Client` | `net/http` | Medium — very common |
| `Channel` | built-in (chan) | Medium — keyword-adjacent |
| `Timer` | `time` | Low |
| `Reader`/`Writer` | `io` | Medium |

**Resolution:** Prefix with `KubeMQ` (e.g., `KubeMQClient`) or use domain-specific names (e.g., `EventsClient`).

### GO-7: Package naming convention
Go packages use short, lowercase, single-word names. No underscores or camelCase.
- **WRONG:** `package kubemq_errors`, `package kubeMQClient`
- **RIGHT:** `package kubemq`, `package errors` (within the module)
- SDK packages: `github.com/kubemq-io/kubemq-go/...`
- Sub-packages: `errors`, `client`, `config`, `transport`

### GO-8: Interface naming convention
Go interfaces with a single method are named with the `-er` suffix.
- `Reader`, `Writer`, `Closer`, `Sender`, `Subscriber`
- Multi-method interfaces use descriptive nouns: `MessageHandler`, `CredentialProvider`
- **WRONG:** `type IClient interface` (no `I` prefix — that's C#/Java style)
- Keep interfaces small (1–3 methods). Define interfaces in the package that *uses* them, not the package that implements them.
- Compose large behaviors from small interfaces: `type ReadCloser interface { Reader; Closer }`.
- See also: GO-25 for detailed small interface guidance.

---

## Error Handling

### GO-3: Error interface implementation
Go errors must implement the `error` interface: `Error() string`.
- For wrapping: implement `Unwrap() error` (Go 1.13+)
- For `errors.Is`/`errors.As`: implement `Is(target error) bool` and/or expose typed fields
- **WRONG:** `type KubeMQError struct { Message string }` (missing Error() method)
- **RIGHT:** `type KubeMQError struct { ... }` with `func (e *KubeMQError) Error() string { ... }`

**Sentinel vs custom type guidance:**
- Use sentinel errors (`var ErrXxx = errors.New(...)`) for conditions callers check with `errors.Is` (control flow).
- Use custom error types (`type XxxError struct`) for conditions callers inspect with `errors.As` (detailed info).
- Sentinel error variables use `Err` prefix: `ErrConnectionClosed`, not `ConnectionClosedError`.
- Sentinel error strings start with the package name: `"kubemq: ..."`.
- Custom error types use `Error` suffix: `RequestError`, `TransportError`.
- Always implement `Unwrap() error` on custom error types for chain inspection.
- See also: GO-18 for full sentinel error patterns and examples.

### GO-5: Multiple return values for errors
Go functions return errors as the last return value, not via exceptions.
- **WRONG:** `func Send(msg Message) Result` (no error return)
- **RIGHT:** `func Send(msg Message) (Result, error)`
- Never panic for recoverable errors.

**Error wrapping convention:**
- Use `%w` (not `%v`) when wrapping errors so callers can use `errors.Is`/`errors.As`.
- Add context at each abstraction boundary (transport → client → user).
- Use `%v` only when you intentionally want to hide the underlying error from callers.
- See also: GO-20 for full `%w` wrapping patterns and examples.

### GO-18: Use sentinel errors for control flow, custom types for details
**Priority:** P0  
**Cross-ref:** Extends GO-3  
**Used by:** nats.go (`ErrConnectionClosed`, `ErrTimeout`), etcd (`ErrCompacted`), grpc-go (status codes)

Define exported sentinel errors for conditions callers check with `errors.Is`. Use custom error types for conditions callers inspect with `errors.As`.

```go
// WRONG — string matching for error control flow
if err.Error() == "connection closed" {
    reconnect()
}

// RIGHT — sentinel errors for control flow
var (
    ErrConnectionClosed = errors.New("kubemq: connection closed")
    ErrTimeout          = errors.New("kubemq: request timeout")
    ErrAuthFailed       = errors.New("kubemq: authentication failed")
)

if errors.Is(err, ErrConnectionClosed) {
    reconnect()
}
```

```go
// RIGHT — custom type for detailed inspection
type RequestError struct {
    Op         string
    StatusCode int
    Message    string
    Err        error
}

func (e *RequestError) Error() string {
    return fmt.Sprintf("kubemq: %s failed (status %d): %s", e.Op, e.StatusCode, e.Message)
}

func (e *RequestError) Unwrap() error { return e.Err }

// Caller uses errors.As
var reqErr *RequestError
if errors.As(err, &reqErr) {
    log.Printf("operation %s failed with status %d", reqErr.Op, reqErr.StatusCode)
}
```

**Rules:**
- Sentinel error variables use `Err` prefix: `ErrConnectionClosed`, not `ConnectionClosedError`.
- Sentinel error strings start with the package name: `"kubemq: ..."`.
- Custom error types use `Error` suffix: `RequestError`, `TransportError`.
- Always implement `Unwrap() error` on custom error types for chain inspection.

### GO-19: Error strings are lowercase, no trailing punctuation
**Priority:** P0  
**Used by:** Go standard library, all K8s ecosystem projects, enforced by `golangci-lint`

```go
// WRONG — capitalized, punctuation
return fmt.Errorf("Failed to connect to server.")

// WRONG — ends with colon
return fmt.Errorf("connection failed:")

// RIGHT — lowercase, no punctuation
return fmt.Errorf("failed to connect to server")

// RIGHT — wrapping preserves readability
return fmt.Errorf("send message: %w", err)
// produces: "send message: connection reset by peer"
```

**Rationale:** Errors are frequently wrapped and concatenated. Capitalized or punctuated fragments produce awkward output like `"Send: Failed to connect.: EOF."`.

### GO-20: Wrap errors at abstraction boundaries with %w
**Priority:** P0  
**Cross-ref:** Extends GO-5  
**Used by:** grpc-go, client-go, etcd client v3

```go
// WRONG — raw error return loses context
func (c *Client) Send(ctx context.Context, msg *Message) error {
    return c.transport.send(ctx, msg)
}

// WRONG — %v destroys the error chain
func (c *Client) Send(ctx context.Context, msg *Message) error {
    err := c.transport.send(ctx, msg)
    if err != nil {
        return fmt.Errorf("send failed: %v", err)  // breaks errors.Is/As
    }
    return nil
}

// RIGHT — %w preserves the chain
func (c *Client) Send(ctx context.Context, msg *Message) error {
    err := c.transport.send(ctx, msg)
    if err != nil {
        return fmt.Errorf("send message %q: %w", msg.Channel, err)
    }
    return nil
}
```

**Rules:**
- Use `%w` (not `%v`) when wrapping errors so callers can use `errors.Is`/`errors.As`.
- Add context at each abstraction boundary (transport → client → user).
- Use `%v` only when you intentionally want to hide the underlying error from callers.

### GO-21: Map gRPC status codes to SDK-specific sentinel errors
**Priority:** P0  
**Cross-ref:** Extends GO-17  
**Used by:** grpc-go (`status.FromError`), nats.go, go-redis

```go
// WRONG — exposing raw gRPC errors to SDK users
func (c *Client) Send(ctx context.Context, msg *Message) error {
    _, err := c.grpcClient.SendMessage(ctx, msg.toProto())
    return err // caller must know gRPC status package
}

// RIGHT — map gRPC errors to SDK domain errors
func (c *Client) Send(ctx context.Context, msg *Message) error {
    _, err := c.grpcClient.SendMessage(ctx, msg.toProto())
    if err != nil {
        return mapGRPCError("send", err)
    }
    return nil
}

func mapGRPCError(op string, err error) error {
    st, ok := status.FromError(err)
    if !ok {
        return fmt.Errorf("kubemq: %s: %w", op, err)
    }
    switch st.Code() {
    case codes.Unauthenticated:
        return fmt.Errorf("kubemq: %s: %w", op, ErrAuthFailed)
    case codes.Unavailable:
        return fmt.Errorf("kubemq: %s: %w", op, ErrConnectionClosed)
    case codes.DeadlineExceeded:
        return fmt.Errorf("kubemq: %s: %w", op, ErrTimeout)
    case codes.PermissionDenied:
        return fmt.Errorf("kubemq: %s: %w", op, ErrPermissionDenied)
    default:
        return fmt.Errorf("kubemq: %s: grpc %s: %w", op, st.Code(), err)
    }
}
```

**Rules:**
- Never expose `google.golang.org/grpc/status` types in the SDK's public API.
- Map every relevant gRPC code to an SDK sentinel error.
- Preserve the original error in the chain via `%w` for debugging.

---

## Context Propagation

### GO-12: context.Context is always the first parameter
- **WRONG:** `func Send(msg Message, ctx context.Context) error`
- **RIGHT:** `func Send(ctx context.Context, msg Message) error`
- Never store `context.Context` in a struct field (except for goroutine lifecycle management — name it `shutdownCtx` or `lifetimeCtx` to signal intent). See also: GO-22.
- Use `context.Background()` in `main()`, initialization, and tests. Use `context.TODO()` when context should be propagated but isn't yet. Never pass `nil` as a context. See also: GO-23.

### GO-22: Never store context.Context in struct fields
**Priority:** P0  
**Cross-ref:** Extends GO-12  
**Used by:** Go standard library (documented anti-pattern), client-go, nats.go

```go
// WRONG — storing context in a struct
type Client struct {
    ctx    context.Context
    cancel context.CancelFunc
}

func NewClient(ctx context.Context) *Client {
    ctx, cancel := context.WithCancel(ctx)
    return &Client{ctx: ctx, cancel: cancel}
}

func (c *Client) Send(msg *Message) error {
    return c.transport.send(c.ctx, msg) // stale context
}

// RIGHT — accept context per-call, store only cancel for shutdown
type Client struct {
    shutdownCancel context.CancelFunc
    shutdownCtx    context.Context // only for internal goroutine lifecycle
}

func (c *Client) Send(ctx context.Context, msg *Message) error {
    return c.transport.send(ctx, msg) // caller controls deadline
}
```

**Exception:** A `context.Context` stored solely for goroutine lifecycle management (shutdown signaling) is acceptable. Name it `shutdownCtx` or `lifetimeCtx` to signal intent.

### GO-23: context.Background() vs context.TODO()
**Priority:** P1  
**Cross-ref:** Extends GO-12  
**Used by:** Go standard library convention, enforced by `contextcheck` linter

```go
// WRONG — context.Background() in production code that should propagate a caller context
func (c *Client) reconnect() error {
    return c.dial(context.Background()) // should accept ctx from caller
}

// RIGHT — context.TODO() signals intentional tech debt
func (c *Client) reconnect() error {
    return c.dial(context.TODO()) // TODO: propagate ctx from caller
}

// RIGHT — context.Background() in main/init/tests
func main() {
    ctx := context.Background()
    client, err := kubemq.NewClient(ctx, kubemq.WithAddress("localhost:50000"))
}
```

**Rules:**
- `context.Background()`: use in `main()`, initialization, and tests.
- `context.TODO()`: use when context should be propagated but isn't yet — treat as a TODO marker.
- Never pass `nil` as a context — it causes runtime panics.

---

## Interface Design

### GO-24: Accept interfaces, return concrete types
**Priority:** P0  
**Used by:** Go standard library, client-go, grpc-go

```go
// WRONG — returning an interface hides the concrete type
func NewClient(opts ...Option) (ClientInterface, error) {
    return &client{}, nil
}

// RIGHT — return the concrete type, accept interfaces where needed
func NewClient(opts ...Option) (*Client, error) {
    return &Client{}, nil
}

// RIGHT — accept interfaces for dependencies (dependency injection)
type Client struct {
    transport Transport // interface — mockable in tests
    logger    Logger    // interface — user can provide their own
}
```

**Rationale:** Go proverb: "Accept interfaces, return structs." Returning interfaces prevents callers from accessing methods not on the interface and makes API evolution harder.

### GO-25: Keep interfaces small (1–3 methods)
**Priority:** P1  
**Cross-ref:** Extends GO-8  
**Used by:** Go standard library (`io.Reader`, `io.Writer`, `io.Closer`), nats.go

```go
// WRONG — monolithic interface
type Client interface {
    Connect(ctx context.Context) error
    Close() error
    SendEvent(ctx context.Context, event *Event) error
    SendCommand(ctx context.Context, cmd *Command) (*Response, error)
    SubscribeEvents(ctx context.Context, channel string, handler EventHandler) error
    SubscribeCommands(ctx context.Context, channel string, handler CommandHandler) error
    // ... 15 more methods
}

// RIGHT — small, composable interfaces
type Sender interface {
    Send(ctx context.Context, msg *Message) error
}

type Subscriber interface {
    Subscribe(ctx context.Context, channel string, handler Handler) (Subscription, error)
}

type Closer interface {
    Close() error
}
```

**Rules:**
- Interfaces with 1 method get `-er` suffix: `Sender`, `Closer`.
- Define interfaces in the package that *uses* them, not the package that implements them.
- Compose large behaviors from small interfaces: `type ReadCloser interface { Reader; Closer }`.

---

## Configuration & Options

### GO-16: Functional options pattern
Go SDKs use functional options for configuration:
```go
type Option func(*clientConfig) error

func WithAddress(addr string) Option {
    return func(c *clientConfig) error {
        if addr == "" {
            return fmt.Errorf("kubemq: address must not be empty")
        }
        c.address = addr
        return nil
    }
}

func NewClient(opts ...Option) (*Client, error) {
    cfg := defaultConfig()
    for _, opt := range opts {
        if err := opt(&cfg); err != nil {
            return nil, err
        }
    }
    return &Client{cfg: cfg}, nil
}
```
All configuration in specs MUST use this pattern, not builder pattern or struct literals.
- Options should validate eagerly (return errors from the option function). See also: GO-26.
- Always provide sensible defaults via a `defaultConfig()` function. See also: GO-27.

### GO-26: Functional options must validate eagerly
**Priority:** P0  
**Cross-ref:** Extends GO-16  
**Used by:** nats.go, grpc-go, go-redis

```go
// WRONG — silent invalid config, fails later at runtime
func WithMaxRetries(n int) Option {
    return func(c *clientConfig) {
        c.maxRetries = n // what if n < 0?
    }
}

// RIGHT — validate in the option, return error from constructor
type Option func(*clientConfig) error

func WithMaxRetries(n int) Option {
    return func(c *clientConfig) error {
        if n < 0 {
            return fmt.Errorf("kubemq: max retries must be >= 0, got %d", n)
        }
        c.maxRetries = n
        return nil
    }
}

func NewClient(opts ...Option) (*Client, error) {
    cfg := defaultConfig()
    for _, opt := range opts {
        if err := opt(&cfg); err != nil {
            return nil, err
        }
    }
    return &Client{cfg: cfg}, nil
}
```

**Pattern from nats.go:** Options return an `error` so `nats.Connect(url, nats.MaxReconnects(-1))` can validate at construction time.

### GO-27: Provide sensible defaults for all configuration
**Priority:** P0  
**Cross-ref:** Extends GO-16  
**Used by:** nats.go (2s reconnect wait, 60 max reconnects), grpc-go (20s keepalive timeout), go-redis (5 pool size)

```go
// WRONG — zero-value config means "no timeout"
type clientConfig struct {
    address     string
    dialTimeout time.Duration // zero = blocks forever
}

// RIGHT — explicit defaults
func defaultConfig() clientConfig {
    return clientConfig{
        address:        "localhost:50000",
        dialTimeout:    5 * time.Second,
        maxRetries:     3,
        reconnectWait:  2 * time.Second,
        keepaliveTime:  30 * time.Second,
        maxRecvMsgSize: 4 * 1024 * 1024, // 4MB
    }
}
```

**Rules:**
- Document every default value in godoc.
- A zero-value `clientConfig` must never be valid — always use `defaultConfig()`.
- Timeouts must never default to zero (infinite blocking).

### GO-28: Connection event callbacks
**Priority:** P1  
**Used by:** nats.go (`DisconnectedErrCB`, `ReconnectedCB`, `ClosedCB`), go-redis (hooks)

```go
// RIGHT — event callbacks as functional options
func WithOnDisconnected(cb func(err error)) Option {
    return func(c *clientConfig) error {
        c.onDisconnected = cb
        return nil
    }
}

func WithOnReconnected(cb func()) Option {
    return func(c *clientConfig) error {
        c.onReconnected = cb
        return nil
    }
}

func WithOnError(cb func(err error)) Option {
    return func(c *clientConfig) error {
        c.onError = cb
        return nil
    }
}
```

**Rules:**
- Callbacks must be invoked on a separate goroutine — never block the I/O path.
- Nil callbacks are valid (no-op). Never check `cb != nil` and then fail.
- Document thread safety: callbacks may be called concurrently.

---

## Concurrency Rules

### GO-9: Goroutine leak prevention
Every goroutine launched MUST have a shutdown mechanism.
- Use `context.Context` for cancellation signals.
- Every `go func()` must have a corresponding shutdown path (context cancel, channel close, or WaitGroup).
- **WRONG:** `go func() { for { ... } }()` with no exit condition
- **RIGHT:** `go func() { for { select { case <-ctx.Done(): return; case msg := <-ch: ... } } }()`

**WaitGroup tracking:** Every `go func()` must correspond to a `wg.Add(1)` / `defer wg.Done()`. `Close()` must cancel context, then `wg.Wait()`, then release resources. See also: GO-31.

**Timer leak prevention:** Do not use `time.After` in loops — it creates a new timer on each iteration that leaks until fired. Use `time.NewTicker` or a reusable `time.NewTimer` instead. See also: GO-57.

### GO-10: Channel usage patterns
- Unbuffered channels (`make(chan T)`) block both sender and receiver — use only for synchronization.
- Buffered channels (`make(chan T, n)`) for message passing — document the buffer size rationale.
- Always close channels from the sender side, never the receiver.
- Use `select` with `default` for non-blocking operations.

**Closing conventions:** Only the sender (producer) closes a channel. Use `sync.Once` to prevent double-close panics. Use directional channel types in function signatures: `<-chan` for receive, `chan<-` for send. See also: GO-53.

### GO-11: Mutex usage
- Use `sync.Mutex` for exclusive access, `sync.RWMutex` for read-heavy workloads.
- Always unlock in `defer`: `mu.Lock(); defer mu.Unlock()`
- Never copy a `sync.Mutex` (it's a value type) — embed it as a pointer or use it in a struct that's never copied.
- **WRONG:** Passing a struct containing a mutex by value.

**Goroutine safety:** Every exported method on `Client` must be goroutine-safe. Document goroutine safety in godoc. Use `sync/atomic` for single-field flags, `sync.RWMutex` for compound state. See also: GO-29 and GO-30.

### GO-29: Every exported method on Client must be goroutine-safe
**Priority:** P0  
**Cross-ref:** Extends GO-11  
**Used by:** nats.go, go-redis, etcd client v3 (all document goroutine safety)

```go
// WRONG — unsynchronized state access
func (c *Client) IsConnected() bool {
    return c.connected // data race if called concurrently with reconnect
}

// RIGHT — use atomic for simple flags
type Client struct {
    connected atomic.Bool
}

func (c *Client) IsConnected() bool {
    return c.connected.Load()
}

// RIGHT — use mutex for compound state
func (c *Client) Stats() ConnectionStats {
    c.mu.RLock()
    defer c.mu.RUnlock()
    return ConnectionStats{
        Reconnects: c.reconnectCount,
        BytesSent:  c.bytesSent,
    }
}
```

**Rules:**
- Document goroutine safety in godoc: `// Client is safe for concurrent use by multiple goroutines.`
- Use `sync/atomic` for single-field flags (connected, closed).
- Use `sync.RWMutex` for compound state (stats, configuration snapshots).
- Use `sync.Mutex` for critical sections with writes.

### GO-30: sync.Mutex vs sync.RWMutex vs atomic — decision guide
**Priority:** P1  
**Cross-ref:** Extends GO-11  
**Used by:** nats.go (mutex for conn state), grpc-go (atomic for state transitions)

| Scenario | Use | Example |
|----------|-----|---------|
| Single boolean/int flag | `atomic.Bool` / `atomic.Int64` | Connection state, closed flag |
| Read-heavy, write-rare | `sync.RWMutex` | Stats, cached metadata |
| Write-frequent or short critical sections | `sync.Mutex` | Message queue operations |
| Lock-free counters | `atomic.Int64.Add` | Byte counters, message counts |

```go
// WRONG — mutex for a simple boolean
var mu sync.Mutex
var closed bool

func isClosed() bool {
    mu.Lock()
    defer mu.Unlock()
    return closed
}

// RIGHT — atomic for a simple boolean
var closed atomic.Bool

func isClosed() bool {
    return closed.Load()
}
```

### GO-31: Goroutine lifecycle must be trackable
**Priority:** P0  
**Cross-ref:** Extends GO-9  
**Used by:** nats.go, client-go (shared informers), controller-runtime

```go
// WRONG — fire-and-forget goroutine
func (c *Client) startKeepAlive() {
    go func() {
        for {
            c.ping()
            time.Sleep(30 * time.Second)
        }
    }()
}

// RIGHT — tracked goroutine with WaitGroup and context
func (c *Client) startKeepAlive(ctx context.Context) {
    c.wg.Add(1)
    go func() {
        defer c.wg.Done()
        ticker := time.NewTicker(30 * time.Second)
        defer ticker.Stop()
        for {
            select {
            case <-ctx.Done():
                return
            case <-ticker.C:
                c.ping()
            }
        }
    }()
}

func (c *Client) Close() error {
    c.cancel() // signal all goroutines
    c.wg.Wait() // wait for all goroutines to finish
    return c.conn.Close()
}
```

**Rules:**
- Every `go func()` must correspond to a `wg.Add(1)` / `defer wg.Done()`.
- Use `time.NewTicker` (not `time.Sleep`) in loops — `Ticker` is stoppable.
- `Close()` must cancel context, then `wg.Wait()`, then release resources.

### GO-32: Use errgroup for parallel operations with error collection
**Priority:** P1  
**Used by:** client-go, controller-runtime

```go
// WRONG — manual goroutine coordination with channels
func (c *Client) publishBatch(ctx context.Context, msgs []*Message) error {
    errCh := make(chan error, len(msgs))
    for _, msg := range msgs {
        msg := msg
        go func() {
            errCh <- c.publish(ctx, msg)
        }()
    }
    // complex collection logic...
}

// RIGHT — errgroup handles coordination
import "golang.org/x/sync/errgroup"

func (c *Client) publishBatch(ctx context.Context, msgs []*Message) error {
    g, ctx := errgroup.WithContext(ctx)
    for _, msg := range msgs {
        g.Go(func() error {
            return c.publish(ctx, msg)
        })
    }
    return g.Wait() // returns first error, cancels remaining
}
```

**Note:** As of Go 1.22+, the loop variable capture issue is fixed — the `msg := msg` shadow is no longer needed.

---

## Dependency Rules

### GO-13: Optional dependencies via build tags
Go doesn't have `provided` scope like Maven. For optional dependencies (OTel):
- Use build tags (`//go:build otel`) to conditionally compile instrumentation code.
- OR use a separate sub-package (e.g., `kubemq-go/otel`) that users import only if needed.
- Never import optional dependencies in the main package — it forces all users to download them.

**OTel sub-package pattern (preferred):**
```
kubemq-go/
├── client.go          // core SDK — no OTel imports
├── options.go
├── otel/              // optional sub-package
│   ├── tracing.go     // OTel interceptors
│   └── metrics.go     // OTel metrics
└── go.mod
```
- The core `kubemq` package must have ZERO OTel imports.
- OTel dependencies live only in the `otel` sub-package.
- Users opt in: `kubemq.NewClient(kubemqotel.WithTracing())`.
- Build tags (`//go:build otel`) are an alternative but sub-packages are preferred for discoverability.
- See also: GO-44 for full OTel sub-package guidance.

### GO-14: Module versioning (v2+)
If the SDK is at major version 2+, the module path MUST include the version suffix:
- `github.com/kubemq-io/kubemq-go/v2`
- All import paths must include `/v2` — this is enforced by the Go toolchain.
- **WRONG:** `github.com/kubemq-io/kubemq-go` for v2+ releases.

---

## Resource Cleanup

### GO-33: Implement io.Closer and document Close requirements
**Priority:** P0  
**Used by:** nats.go (`nc.Close()`), etcd client v3 (`cli.Close()`), go-redis (`rdb.Close()`)

```go
// RIGHT — Client implements io.Closer
type Client struct {
    conn       *grpc.ClientConn
    cancel     context.CancelFunc
    wg         sync.WaitGroup
    closed     atomic.Bool
}

// Close shuts down the client and releases all resources.
// Close is safe to call multiple times.
func (c *Client) Close() error {
    if !c.closed.CompareAndSwap(false, true) {
        return nil // already closed
    }
    c.cancel()
    c.wg.Wait()
    return c.conn.Close()
}

var _ io.Closer = (*Client)(nil) // compile-time interface check
```

**Rules:**
- `Close()` must be idempotent (safe to call multiple times).
- `Close()` must block until all goroutines have stopped.
- `Close()` must release all held resources (connections, channels, timers).
- Document that callers must call `Close()` — Go has no finalizers for this.

### GO-34: Use compile-time interface satisfaction checks
**Priority:** P1  
**Used by:** grpc-go, nats.go, standard library patterns

```go
// RIGHT — compile-time check that Client implements io.Closer
var _ io.Closer = (*Client)(nil)

// RIGHT — compile-time check that KubeMQError implements error
var _ error = (*KubeMQError)(nil)

// WRONG — discovering interface non-compliance at runtime
func useCloser(c io.Closer) { c.Close() } // panics if Client doesn't implement Closer
```

Place these checks in the same file as the type definition, right after the type.

---

## Logging

### GO-35: Use slog.Handler interface for SDK logging
**Priority:** P0  
**Used by:** Go standard library (Go 1.21+), modern Go SDKs

SDKs must never force a logging implementation. Accept a `*slog.Logger` or use the `slog.Handler` interface.

```go
// WRONG — hardcoded logging implementation
type Client struct {
    logger *zap.Logger // forces zap on all users
}

// WRONG — custom Logger interface that doesn't match anything
type Logger interface {
    Debug(msg string, args ...interface{})
    Info(msg string, args ...interface{})
    Error(msg string, args ...interface{})
}

// RIGHT — accept *slog.Logger (Go 1.21+ standard)
func WithLogger(logger *slog.Logger) Option {
    return func(c *clientConfig) error {
        c.logger = logger
        return nil
    }
}

// Internal usage:
func (c *Client) connect() error {
    c.logger.Info("connecting to server",
        slog.String("address", c.address),
        slog.String("client_id", c.clientID),
    )
    // ...
}
```

**Rules:**
- Default logger: `slog.Default()` (respects user's global configuration).
- Use `slog.String()`, `slog.Int()`, etc. for typed attributes (not raw key-value).
- Add a `slog.Group` for SDK-scoped attributes: `c.logger.WithGroup("kubemq")`.
- Never log at `Error` level inside the SDK for recoverable situations — use `Warn`.
- Minimum Go version for `log/slog`: Go 1.21.

---

## Testing

### GO-36: Define interfaces at consumption point for testability
**Priority:** P0  
**Used by:** client-go (fake clientsets), grpc-go (test helpers)

```go
// RIGHT — interface defined where it's consumed, not where it's implemented
// In client.go:
type transport interface {
    send(ctx context.Context, msg *pb.Message) (*pb.Result, error)
    subscribe(ctx context.Context, req *pb.SubscribeRequest) (pb.KubeMQ_SubscribeClient, error)
}

type Client struct {
    transport transport // unexported interface — implementation detail
}

// In client_test.go:
type mockTransport struct {
    sendFunc      func(ctx context.Context, msg *pb.Message) (*pb.Result, error)
    subscribeFunc func(ctx context.Context, req *pb.SubscribeRequest) (pb.KubeMQ_SubscribeClient, error)
}

func (m *mockTransport) send(ctx context.Context, msg *pb.Message) (*pb.Result, error) {
    return m.sendFunc(ctx, msg)
}
```

**Rules:**
- Keep mock interfaces unexported (package-internal).
- Use function fields in mock structs for per-test behavior customization.
- For gRPC testing, use `google.golang.org/grpc/test/bufconn` for in-memory connections.

### GO-37: Table-driven tests with t.Run subtests
**Priority:** P1  
**Used by:** Go standard library, all K8s ecosystem projects

```go
// RIGHT — table-driven test
func TestClient_Send(t *testing.T) {
    tests := []struct {
        name    string
        msg     *Message
        wantErr error
    }{
        {
            name:    "valid message",
            msg:     &Message{Channel: "test", Body: []byte("hello")},
            wantErr: nil,
        },
        {
            name:    "empty channel",
            msg:     &Message{Channel: "", Body: []byte("hello")},
            wantErr: ErrInvalidChannel,
        },
        {
            name:    "nil body",
            msg:     &Message{Channel: "test", Body: nil},
            wantErr: ErrEmptyBody,
        },
    }

    for _, tt := range tests {
        t.Run(tt.name, func(t *testing.T) {
            client := newTestClient(t)
            err := client.Send(context.Background(), tt.msg)
            if !errors.Is(err, tt.wantErr) {
                t.Errorf("Send() error = %v, wantErr %v", err, tt.wantErr)
            }
        })
    }
}
```

**Rules:**
- Every test function name: `TestType_Method` or `TestFunction`.
- Use `t.Run` for subtests — enables `-run TestClient_Send/empty_channel`.
- Use `t.Helper()` in test helper functions for correct error line reporting.
- Use `t.Cleanup()` instead of `defer` for test resource cleanup.
- Example tests in `*_test.go` with `Example` prefix serve as both tests and documentation.

### GO-38: Use testify/assert or stdlib — pick one, be consistent
**Priority:** P2  
**Used by:** nats.go (stdlib), client-go (stdlib), go-redis (testify)

```go
// Option A — stdlib (zero dependencies, preferred for libraries)
if got != want {
    t.Errorf("Send() = %v, want %v", got, want)
}

// Option B — testify (more expressive, adds a dependency)
assert.Equal(t, want, got)
assert.NoError(t, err)
assert.ErrorIs(t, err, ErrTimeout)
```

**Recommendation:** For an SDK, prefer stdlib assertions to avoid pulling `testify` as a dependency. If `testify` is used, it must be a test-only dependency (`go.mod` will list it, but only test files import it).

---

## gRPC-Specific

### GO-17: Verify protobuf/gRPC generated code
Before referencing a gRPC method, verify it exists in the generated `.pb.go` files. The `.proto` definition is the source of truth.
- Generated files are typically in a `pb` or `proto` sub-package.
- Do not assume methods exist because they'd be convenient.
- Use `grpc.NewClient` (not the deprecated `grpc.Dial`). gRPC connections auto-reconnect — do not implement custom reconnection on top. See also: GO-41.
- Use interceptor chains (`WithChainUnaryInterceptor`, `WithChainStreamInterceptor`) for cross-cutting concerns (tracing, retry, auth). See also: GO-39.
- Map gRPC status codes to SDK-specific sentinel errors. See also: GO-21.

### GO-39: gRPC interceptors for cross-cutting concerns
**Priority:** P0  
**Cross-ref:** Extends GO-17  
**Used by:** grpc-go, go-grpc-middleware, otelgrpc

```go
// RIGHT — chain interceptors for retry, tracing, logging
conn, err := grpc.NewClient(
    address,
    grpc.WithChainUnaryInterceptor(
        otelgrpc.UnaryClientInterceptor(),       // tracing
        retryInterceptor(3, time.Second),         // retry
        authInterceptor(token),                   // auth
    ),
    grpc.WithChainStreamInterceptor(
        otelgrpc.StreamClientInterceptor(),
        authStreamInterceptor(token),
    ),
)
```

**Rules:**
- Use `WithChainUnaryInterceptor` (not `WithUnaryInterceptor`) to compose multiple interceptors.
- Order matters: outermost interceptor runs first (tracing → retry → auth).
- Separate unary and stream interceptor chains — they have different signatures.
- Auth interceptor must inject metadata, not modify the request proto.

### GO-40: gRPC keepalive configuration
**Priority:** P0  
**Used by:** grpc-go (keepalive package), etcd client v3

```go
import "google.golang.org/grpc/keepalive"

// RIGHT — configure keepalive for long-lived connections
conn, err := grpc.NewClient(
    address,
    grpc.WithKeepaliveParams(keepalive.ClientParameters{
        Time:                30 * time.Second, // ping interval when idle
        Timeout:             10 * time.Second, // wait for ping ack
        PermitWithoutStream: true,             // ping even without active RPCs
    }),
)
```

**Rules:**
- `Time` must not be less than 10 seconds (servers may reject more aggressive pings).
- `PermitWithoutStream: true` is required for connection health monitoring during idle periods.
- Document keepalive defaults and how they interact with server-side enforcement.

### GO-41: gRPC connection is lazy — do not use WithBlock
**Priority:** P0  
**Cross-ref:** Extends GO-17  
**Used by:** grpc-go (official recommendation), etcd client v3

```go
// WRONG — blocks until connection is established
conn, err := grpc.Dial(address, grpc.WithBlock())

// WRONG — grpc.Dial is deprecated in grpc-go v1.63+
conn, err := grpc.Dial(address)

// RIGHT — grpc.NewClient returns immediately, connects lazily
conn, err := grpc.NewClient(address, grpc.WithTransportCredentials(creds))
if err != nil {
    return nil, fmt.Errorf("create grpc client: %w", err)
}
// Connection happens on first RPC or via conn.Connect()
```

**Rules:**
- Use `grpc.NewClient` (not the deprecated `grpc.Dial`).
- gRPC connections auto-reconnect — do not implement custom reconnection on top.
- If you need to verify connectivity eagerly, call a health-check RPC after construction.
- Use `grpc.WithConnectParams` to configure backoff for reconnection.

### GO-42: gRPC metadata propagation for auth tokens
**Priority:** P1  
**Used by:** grpc-go (metadata package), all authenticated gRPC services

```go
// RIGHT — per-RPC credentials via interceptor
func authInterceptor(token string) grpc.UnaryClientInterceptor {
    return func(
        ctx context.Context,
        method string,
        req, reply interface{},
        cc *grpc.ClientConn,
        invoker grpc.UnaryInvoker,
        opts ...grpc.CallOption,
    ) error {
        ctx = metadata.AppendToOutgoingContext(ctx, "authorization", "Bearer "+token)
        return invoker(ctx, method, req, reply, cc, opts...)
    }
}

// WRONG — modifying the proto request to include auth
type SendRequest struct {
    Token   string // auth doesn't belong in the message proto
    Message *Message
}
```

### GO-43: TLS configuration patterns
**Priority:** P1  
**Used by:** grpc-go (credentials package), nats.go, etcd client v3

```go
// RIGHT — standard TLS
func WithTLS(certFile, keyFile, caFile string) Option {
    return func(c *clientConfig) error {
        cert, err := tls.LoadX509KeyPair(certFile, keyFile)
        if err != nil {
            return fmt.Errorf("load client cert: %w", err)
        }
        caCert, err := os.ReadFile(caFile)
        if err != nil {
            return fmt.Errorf("read ca cert: %w", err)
        }
        caPool := x509.NewCertPool()
        if !caPool.AppendCertsFromPEM(caCert) {
            return fmt.Errorf("failed to append ca cert")
        }
        c.tlsConfig = &tls.Config{
            Certificates: []tls.Certificate{cert},
            RootCAs:      caPool,
            MinVersion:   tls.VersionTLS12,
        }
        return nil
    }
}

// Also provide a convenience option for simple cases:
func WithTLSFromConfig(cfg *tls.Config) Option {
    return func(c *clientConfig) error {
        c.tlsConfig = cfg
        return nil
    }
}
```

**Rules:**
- Always set `MinVersion: tls.VersionTLS12` — TLS 1.0/1.1 are deprecated.
- Provide both high-level helpers (`WithTLS(cert, key, ca)`) and escape hatches (`WithTLSFromConfig`).
- Never skip certificate verification in production code — `InsecureSkipVerify` must only be in test helpers.

---

## Observability

### GO-44: OTel must be an optional sub-package
**Priority:** P0  
**Cross-ref:** Extends GO-13  
**Used by:** go-redis (`redisotel`), grpc-go (`otelgrpc`), nats.go (separate contrib repo)

```
kubemq-go/
├── client.go          // core SDK — no OTel imports
├── options.go
├── otel/              // optional sub-package
│   ├── tracing.go     // OTel interceptors
│   └── metrics.go     // OTel metrics
└── go.mod
```

```go
// In otel/tracing.go:
package otel

import (
    "go.opentelemetry.io/otel"
    "go.opentelemetry.io/otel/trace"
)

// WithTracing returns a kubemq.Option that enables OpenTelemetry tracing.
func WithTracing(opts ...TracingOption) kubemq.Option {
    return func(c *kubemq.ClientConfig) error {
        // configure OTel interceptors
        return nil
    }
}
```

**Rules:**
- The core `kubemq` package must have ZERO OTel imports.
- OTel dependencies live only in the `otel` sub-package.
- Users opt in: `kubemq.NewClient(kubemqotel.WithTracing())`.
- Build tags (`//go:build otel`) are an alternative but sub-packages are preferred for discoverability.

### GO-45: Span naming convention for traces
**Priority:** P1  
**Used by:** otelgrpc, OpenTelemetry semantic conventions

```go
// WRONG — inconsistent span names
span := tracer.Start(ctx, "send")
span := tracer.Start(ctx, "KubeMQ.SendMessage")
span := tracer.Start(ctx, "kubemq-send-event")

// RIGHT — follow OTel semantic conventions for messaging
// Format: <messaging.operation> <messaging.destination.name>
span := tracer.Start(ctx, "publish events.orders",
    trace.WithSpanKind(trace.SpanKindProducer),
    trace.WithAttributes(
        semconv.MessagingSystemKey.String("kubemq"),
        semconv.MessagingOperationPublish,
        semconv.MessagingDestinationName("events.orders"),
    ),
)
```

**Rules:**
- Follow OTel messaging semantic conventions for span names and attributes.
- Use `SpanKindProducer` for send operations, `SpanKindConsumer` for receive.
- Set `messaging.system` attribute to `"kubemq"`.

---

## Module & Build

### GO-15: `internal/` packages are not importable
Code in `internal/` directories cannot be imported outside the module. Use this for implementation details.
- Public API types go in top-level packages.
- Internal helpers go in `internal/`.
- **Never** put types referenced in specs' public API into `internal/`.
- Use `internal/` packages for helper types shared between sub-packages but hidden from users. See also: GO-55.

### GO-46: Minimum Go version policy
**Priority:** P0  
**Used by:** client-go (N-2 versions), grpc-go (Go 1.22+), nats.go (Go 1.21+)

**Recommendation:** Target Go 1.21+ as the minimum version.

| Feature | Minimum Go Version | Status |
|---------|-------------------|--------|
| `log/slog` | 1.21 | Required for GO-35 |
| Generics | 1.18 | Available |
| `errors.Is`/`errors.As` | 1.13 | Available |
| `sync/atomic` types (`atomic.Bool`) | 1.19 | Available |
| Range-over-int | 1.22 | Optional |
| Loop variable capture fix | 1.22 | Optional |
| `context.AfterFunc` | 1.21 | Available |

Set in `go.mod`:
```
module github.com/kubemq-io/kubemq-go/v2

go 1.21
```

### GO-47: Minimal external dependencies
**Priority:** P0  
**Used by:** nats.go (very few deps), Go community convention

```
# ALLOWED core dependencies:
google.golang.org/grpc        # gRPC — required
google.golang.org/protobuf    # protobuf — required
golang.org/x/sync             # errgroup — semi-stdlib

# OPTIONAL (sub-packages only):
go.opentelemetry.io/otel      # OTel — in otel/ sub-package only

# AVOID:
github.com/sirupsen/logrus    # use slog instead
github.com/pkg/errors         # use fmt.Errorf with %w
github.com/uber-go/zap        # use slog instead
github.com/stretchr/testify   # use stdlib assertions (or test-only dep)
```

**Rules:**
- Every non-stdlib dependency must be justified in a comment.
- `golang.org/x/*` packages are acceptable (they're semi-official).
- Test-only dependencies don't count (but should still be minimal).
- Run `go mod tidy` before every release to remove unused deps.

### GO-48: Linting configuration (golangci-lint)
**Priority:** P1  
**Used by:** All major Go projects

Recommended linters for the KubeMQ Go SDK:

```yaml
# .golangci.yml
version: "2"
linters:
  enable:
    # Bugs
    - errcheck
    - errorlint
    - bodyclose
    - contextcheck
    - copyloopvar
    # Style
    - errname
    - goimports
    - govet
    - revive
    # Security
    - gosec
    # Complexity
    - cyclop
    - funlen
    # SDK-specific
    - containedctx    # detects context stored in structs
    - gochecknoinits   # detects init() functions
    - gochecknoglobals # detects global variables
```

**Rules for specs:**
- Specs must not produce linter warnings from the enabled set.
- `containedctx` enforces GO-22 (no stored contexts).
- `gochecknoinits` enforces GO-51 (no init functions).
- `errorlint` enforces proper error wrapping with `%w`.

### GO-49: Documentation with godoc
**Priority:** P1  
**Used by:** All Go standard library, K8s ecosystem

```go
// WRONG — no doc comment
func NewClient(opts ...Option) (*Client, error) {

// WRONG — doc comment doesn't start with function name
// Creates a new client with the given options.
func NewClient(opts ...Option) (*Client, error) {

// RIGHT — doc comment starts with the function/type name
// NewClient creates a KubeMQ client with the given options.
// It returns an error if any option is invalid.
//
// The client must be closed with [Client.Close] when no longer needed.
// The client is safe for concurrent use by multiple goroutines.
func NewClient(opts ...Option) (*Client, error) {
```

**Rules:**
- Every exported type, function, and method must have a doc comment.
- Doc comments start with the name of the thing being documented.
- Use `[TypeName.Method]` syntax for cross-references (Go 1.19+).
- Include `Example` functions in `_test.go` for key API surface.

---

## Common Pitfalls

### GO-50: The nil interface trap
**Priority:** P0  
**Used by:** Affects all Go code — documented extensively in Go community

```go
// WRONG — returns a typed nil that is not == nil
func getError() error {
    var err *KubeMQError // typed nil
    return err           // interface{*KubeMQError, nil} != nil
}

if err := getError(); err != nil {
    // THIS EXECUTES — err is non-nil interface with nil pointer
    fmt.Println(err) // likely panics calling err.Error()
}

// RIGHT — return nil directly
func getError() error {
    var err *KubeMQError
    if err == nil {
        return nil // untyped nil — interface{nil, nil} == nil
    }
    return err
}
```

**Rule:** Never return a typed nil pointer as an interface value. Check for nil and return the bare `nil` literal.

### GO-51: No init() functions in libraries
**Priority:** P0  
**Used by:** Go community best practice, enforced by `gochecknoinits` linter

```go
// WRONG — init() in a library package
func init() {
    defaultClient = &Client{address: "localhost:50000"}
    prometheus.MustRegister(messageCounter)
}

// RIGHT — explicit initialization via constructor
func NewClient(opts ...Option) (*Client, error) {
    cfg := defaultConfig()
    for _, opt := range opts {
        if err := opt(&cfg); err != nil {
            return nil, err
        }
    }
    return &Client{cfg: cfg}, nil
}
```

**Rationale:**
- `init()` runs at import time — users can't control when/if it runs.
- Makes testing impossible without importing side effects.
- Creates hidden global state.
- The only acceptable `init()` in a library is for registering codecs/drivers (e.g., `database/sql` drivers), and even that pattern is now discouraged.

### GO-52: No exported global variables
**Priority:** P0  
**Used by:** Go community best practice, enforced by `gochecknoglobals` linter

```go
// WRONG — mutable global state
var DefaultClient *Client

// WRONG — package-level logger
var Logger *slog.Logger = slog.Default()

// RIGHT — sentinel errors are the ONLY acceptable exported vars
var (
    ErrConnectionClosed = errors.New("kubemq: connection closed")
    ErrTimeout          = errors.New("kubemq: request timeout")
)

// RIGHT — use functional options for default configuration
client, err := kubemq.NewClient() // uses defaults internally
```

**Exceptions:** Exported `Err*` sentinel error variables are acceptable and expected.

### GO-53: Channel closing conventions
**Priority:** P1  
**Cross-ref:** Extends GO-10  
**Used by:** nats.go, client-go

```go
// WRONG — receiver closes the channel
func consumer(ch <-chan *Message) {
    for msg := range ch {
        process(msg)
    }
    close(ch) // PANIC: cannot close receive-only channel (also wrong semantically)
}

// WRONG — closing a channel twice
close(ch)
close(ch) // PANIC: close of closed channel

// RIGHT — sender closes, use sync.Once for safety
type subscription struct {
    msgCh     chan *Message
    closeOnce sync.Once
}

func (s *subscription) close() {
    s.closeOnce.Do(func() {
        close(s.msgCh)
    })
}
```

**Rules:**
- Only the sender (producer) closes a channel.
- Use `sync.Once` to prevent double-close panics.
- Use directional channel types in function signatures: `<-chan` for receive, `chan<-` for send.
- Prefer `for range ch` to `for { select { case msg, ok := <-ch` when there's only one channel.

### GO-54: Reconnection with exponential backoff and jitter
**Priority:** P0  
**Used by:** nats.go (ReconnectJitter), grpc-go (backoff.Config), go-redis (automatic retry)

```go
// WRONG — fixed delay reconnection
func (c *Client) reconnectLoop(ctx context.Context) {
    for {
        time.Sleep(2 * time.Second) // thundering herd with multiple clients
        if err := c.connect(); err == nil {
            return
        }
    }
}

// RIGHT — exponential backoff with jitter
func (c *Client) reconnectLoop(ctx context.Context) {
    backoff := time.Second
    maxBackoff := 30 * time.Second

    for attempt := 0; ; attempt++ {
        select {
        case <-ctx.Done():
            return
        case <-time.After(backoff + jitter(backoff)):
            if err := c.connect(); err == nil {
                c.onReconnected()
                return
            }
            backoff = min(backoff*2, maxBackoff)
        }
    }
}

func jitter(d time.Duration) time.Duration {
    return time.Duration(rand.Int64N(int64(d) / 4)) // up to 25% jitter
}
```

**Rules:**
- Always add jitter to prevent thundering herd.
- Cap backoff with a maximum duration.
- Respect context cancellation in the retry loop.
- Invoke callbacks (`onDisconnected`, `onReconnected`) at state transitions.
- Use `grpc.WithConnectParams(grpc.ConnectParams{Backoff: backoff.Config{...}})` for gRPC-level reconnection.

### GO-55: Do not export internal types used only in implementations
**Priority:** P1  
**Cross-ref:** Extends GO-2, GO-15  
**Used by:** Go community best practice, nats.go

```go
// WRONG — exporting everything
type ConnectionState int

const (
    ConnectionStateDisconnected ConnectionState = iota
    ConnectionStateConnecting
    ConnectionStateConnected
)

type RetryConfig struct {  // exported but only used internally
    MaxRetries int
    Backoff    time.Duration
}

// RIGHT — unexported internal types
type connectionState int

const (
    stateDisconnected connectionState = iota
    stateConnecting
    stateConnected
)

type retryConfig struct {
    maxRetries int
    backoff    time.Duration
}
```

**Rules:**
- Export only types that appear in public function signatures.
- Internal state machines, retry configs, and transport details stay unexported.
- Use `internal/` packages for helper types shared between sub-packages but hidden from users.

### GO-56: Subscription/stream handlers must not block the delivery goroutine
**Priority:** P0  
**Used by:** nats.go (async message dispatch), sarama (consumer group handlers)

```go
// WRONG — handler blocks the delivery goroutine
client.Subscribe(ctx, "events", func(msg *Message) {
    result := expensiveProcess(msg) // blocks all other message delivery
    db.Save(result)
})

// RIGHT — handler dispatches to a worker pool
client.Subscribe(ctx, "events", func(msg *Message) {
    select {
    case workerCh <- msg: // non-blocking enqueue
    default:
        c.logger.Warn("worker pool full, dropping message",
            slog.String("channel", msg.Channel),
        )
    }
})
```

**Rules:**
- Document whether handlers are called synchronously or asynchronously.
- If synchronous: document that blocking the handler blocks all delivery.
- Provide guidance on worker pool patterns for slow consumers.
- nats.go pattern: set `MaxPendingMsgs` and `MaxPendingBytes` per subscription.

### GO-57: Avoid time.After in loops — it leaks timers
**Priority:** P0  
**Cross-ref:** Extends GO-9  
**Used by:** Go community (well-known leak pattern)

```go
// WRONG — time.After creates a new timer on each iteration, leaks until GC
for {
    select {
    case msg := <-ch:
        process(msg)
    case <-time.After(5 * time.Second): // LEAK: timer is not garbage collected until it fires
        return ErrTimeout
    }
}

// RIGHT — reusable timer
timer := time.NewTimer(5 * time.Second)
defer timer.Stop()

for {
    timer.Reset(5 * time.Second)
    select {
    case msg := <-ch:
        if !timer.Stop() {
            <-timer.C
        }
        process(msg)
    case <-timer.C:
        return ErrTimeout
    }
}
```

**Note:** As of Go 1.23, `time.After` timers are garbage collected even if they haven't fired, but the pattern above remains preferred for hot loops in SDKs that may target older Go versions.

---

## Summary Matrix

### Priority Distribution

| Priority | Count | Description |
|----------|-------|-------------|
| P0 | 23 | Must add — prevents common spec errors |
| P1 | 13 | Should add — improves robustness |
| P2 | 1 | Nice to have |

### Rules by Section

| Section | Rules | Count |
|---------|-------|-------|
| Syntax | GO-1, GO-2, GO-4 | 3 |
| Naming | GO-6, GO-7, GO-8 | 3 |
| Error Handling | GO-3, GO-5, GO-18, GO-19, GO-20, GO-21 | 6 |
| Context Propagation | GO-12, GO-22, GO-23 | 3 |
| Interface Design | GO-24, GO-25 | 2 |
| Configuration & Options | GO-16, GO-26, GO-27, GO-28 | 4 |
| Concurrency | GO-9, GO-10, GO-11, GO-29, GO-30, GO-31, GO-32 | 7 |
| Dependency | GO-13, GO-14 | 2 |
| Resource Cleanup | GO-33, GO-34 | 2 |
| Logging | GO-35 | 1 |
| Testing | GO-36, GO-37, GO-38 | 3 |
| gRPC-Specific | GO-17, GO-39, GO-40, GO-41, GO-42, GO-43 | 6 |
| Observability | GO-44, GO-45 | 2 |
| Module & Build | GO-15, GO-46, GO-47, GO-48, GO-49 | 5 |
| Common Pitfalls | GO-50, GO-51, GO-52, GO-53, GO-54, GO-55, GO-56, GO-57 | 8 |
| **Total** | | **57** |

### Cross-Reference Index

| New Rule | Extends | Relationship |
|----------|---------|-------------|
| GO-18 | GO-3 | Sentinel vs custom type patterns |
| GO-20 | GO-5 | %w wrapping convention |
| GO-21 | GO-17 | gRPC status code mapping |
| GO-22 | GO-12 | Never store context in structs |
| GO-23 | GO-12 | Background vs TODO usage |
| GO-25 | GO-8 | Small interface guidance |
| GO-26 | GO-16 | Eager validation in options |
| GO-27 | GO-16 | Sensible defaults |
| GO-29 | GO-11 | Goroutine safety for exported methods |
| GO-30 | GO-11 | Mutex vs RWMutex vs atomic guide |
| GO-31 | GO-9 | WaitGroup goroutine tracking |
| GO-39 | GO-17 | gRPC interceptor chains |
| GO-41 | GO-17 | grpc.NewClient (no Dial) |
| GO-44 | GO-13 | OTel sub-package pattern |
| GO-53 | GO-10 | Channel closing conventions |
| GO-55 | GO-2, GO-15 | Unexported internal types |
| GO-57 | GO-9 | time.After leak prevention |

---

## References

- **nats.go**: github.com/nats-io/nats.go — connection lifecycle, functional options, event callbacks
- **client-go**: k8s.io/client-go — shared informer factory, fake clients for testing
- **grpc-go**: google.golang.org/grpc — interceptors, keepalive, connection management, status codes
- **go-redis**: github.com/redis/go-redis — OTel integration (`redisotel`), context per command
- **sarama**: github.com/IBM/sarama — consumer group handlers, async producer patterns
- **etcd client v3**: go.etcd.io/etcd/client/v3 — config struct, distributed primitives, error types
- **controller-runtime**: sigs.k8s.io/controller-runtime — reconciler pattern, manager lifecycle
- **Go official blog**: go.dev/blog/slog, go.dev/blog/when-generics, go.dev/doc/effective_go
- **golangci-lint**: github.com/golangci/golangci-lint
