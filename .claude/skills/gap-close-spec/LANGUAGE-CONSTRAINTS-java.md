# Java Language Constraints for Spec Agents

**Purpose:** Language-specific pitfalls discovered from prior gap-close-spec runs. Spec agents MUST follow ALL rules listed here to prevent common errors.

**Source:** Retrospective analysis of 98 issues across 6 reviews (Java SDK gap-close-spec run, 2026-03-10), enhanced with K8s-ecosystem SDK analysis and enterprise Java best practices.

**Priority Legend:**
- **P0** — Must follow. Omission causes compile errors, runtime failures, or security issues.
- **P1** — Should follow. Prevents subtle bugs, improves SDK quality significantly.
- **P2** — Nice to have. Aligns with ecosystem conventions and modern Java idioms.

---

## Syntax Rules

### J-1: No import aliases

Java does NOT support import aliases (`import X as Y`). This is a compile error.
- **Instead:** Use fully-qualified class names inline, or use static imports for specific methods.
- **Example (WRONG):** `import io.kubemq.sdk.error.KubeMQException as KException;`
- **Example (RIGHT):** `import io.kubemq.sdk.error.KubeMQException;` or use `io.kubemq.sdk.error.KubeMQException` inline.

### J-2: Method bodies are required

Every method in a concrete class must have a complete body. Abstract methods are only allowed in abstract classes or interfaces.
- **In specs:** Never write `public void doSomething(); // to be defined later` in a concrete class.
- **Instead:** Provide the full method body, or explicitly declare the class `abstract`.

### J-3: Checked vs unchecked exceptions — use unchecked with a layered hierarchy [P0]

Java distinguishes between checked exceptions (extends `Exception`) and unchecked exceptions (extends `RuntimeException`).
- Checked exceptions MUST be declared in `throws` clauses.
- If your exception hierarchy extends `RuntimeException`, do NOT add `throws` clauses — it's misleading.
- Be consistent: decide once whether the KubeMQ exception hierarchy is checked or unchecked, and stick with it across all specs.

**Concrete guidance:** Every major K8s-ecosystem SDK uses unchecked (`RuntimeException`) exceptions:
- **AWS SDK v2:** `SdkException → SdkClientException / AwsServiceException → S3Exception, ...`
- **fabric8:** `KubernetesClientException extends RuntimeException`
- **Lettuce:** `RedisException extends RuntimeException`
- **NATS:** `IOException` (checked) for connect, unchecked for message operations

The KubeMQ SDK **must** use unchecked exceptions to avoid forcing `try/catch` on every call. Use a layered hierarchy:

```java
// WRONG — flat exception hierarchy
public class KubeMQException extends RuntimeException { ... }
// Every catch block catches everything, no granularity

// RIGHT — layered hierarchy (AWS SDK v2 pattern)
public abstract class KubeMQException extends RuntimeException {
    private final String errorCode;
    protected KubeMQException(String message, String errorCode, Throwable cause) {
        super(message, cause);
        this.errorCode = errorCode;
    }
    public String errorCode() { return errorCode; }
}

// SDK-side errors (config, serialization, connectivity)
public class KubeMQClientException extends KubeMQException { ... }

// Server-side errors (returned by KubeMQ server)
public class KubeMQServerException extends KubeMQException {
    private final io.grpc.Status.Code grpcStatusCode;
    public io.grpc.Status.Code grpcStatusCode() { return grpcStatusCode; }
}

// Specific subtypes for programmatic handling
public class KubeMQAuthenticationException extends KubeMQClientException { ... }
public class KubeMQConnectionException extends KubeMQClientException { ... }
public class KubeMQTimeoutException extends KubeMQClientException { ... }
```

**Cause chaining is mandatory:** Every exception that wraps another must preserve the cause chain. Losing the original stack trace makes debugging impossible.

```java
// WRONG — original cause lost
catch (StatusRuntimeException e) {
    throw new KubeMQServerException("Send failed: " + e.getMessage());
}

// RIGHT — full context preserved
catch (StatusRuntimeException e) {
    Status status = Status.fromThrowable(e);
    throw new KubeMQServerException(
        "Send failed: " + status.getDescription(),
        mapGrpcCode(status.getCode()),
        e  // cause chain preserved
    );
}
```

**References:** AWS SDK v2 (`SdkException` → `AwsServiceException`), fabric8 (`KubernetesClientException`), jetcd wraps gRPC status.

### J-4: Generics are invariant

Java generics are invariant by default. `List<Dog>` is NOT a `List<Animal>`.
- Use `? extends T` for producer (read-only) positions.
- Use `? super T` for consumer (write-only) positions.
- Verify all generic type parameters are correctly bounded in interface definitions.

---

## Naming Rules

### J-5: Standard library name collisions

These class names exist in the JDK or common libraries and MUST NOT be reused for KubeMQ types:

| JDK/Library Class | Package | Risk |
|-----------|---------|------|
| `CancellationException` | `java.util.concurrent` | High — commonly imported in async code |
| `TimeoutException` | `java.util.concurrent` | High — commonly imported |
| `ExecutionException` | `java.util.concurrent` | High — CompletableFuture unwrapping |
| `IOException` | `java.io` | High — everywhere |
| `LoggerFactory` | SLF4J (`org.slf4j`) | High — every class with logging |
| `Logger` | SLF4J / `java.util.logging` | Medium — depends on import |
| `Channel` | `java.nio.channels` | Medium — NIO code |
| `Future` | `java.util.concurrent` | Medium — async code |
| `Timer` | `java.util` | Low |
| `ValidationException` | `javax.validation` | Medium — Bean Validation API |
| `AuthorizationException` | `javax.security` / Spring | Medium — security frameworks |
| `NotFoundException` | `javax.ws.rs` | Medium — JAX-RS |
| `Builder` | multiple | Medium — common pattern name |
| `Result` | multiple | Low — common return type name |

**Resolution:** Prefix with `KubeMQ` (e.g., `KubeMQTimeoutException`, `KubeMQValidationException`, `KubeMQAuthorizationException`, `KubeMQNotFoundException`) or use a distinct descriptive name (e.g., `OperationTimedOutException`).

### J-6: Package naming convention

All KubeMQ SDK types must be in `io.kubemq.sdk.*` packages. Use these sub-packages:
- `io.kubemq.sdk.exception` — All exception types (NOT `io.kubemq.sdk.error`)
- `io.kubemq.sdk.client` — Client classes
- `io.kubemq.sdk.config` — Configuration types
- `io.kubemq.sdk.common` — Shared utilities and base types

Pick ONE package convention and use it consistently across all specs. Do NOT mix `error/` and `exception/` sub-packages.

---

## Concurrency Rules

### J-7: ThreadLocalRandom is NOT a static field

`ThreadLocalRandom.current()` must be called at point of use, never stored in a static field.
- **WRONG:** `private static final ThreadLocalRandom random = ThreadLocalRandom.current();`
- **RIGHT:** `ThreadLocalRandom.current().nextInt(100)` at point of use.

### J-8: CAS loops need bounded retry

`AtomicReference.compareAndSet()` loops can cause `StackOverflowError` if implemented as recursive calls, or spin forever if unbounded.
- Always use a `while` loop with a maximum retry count (e.g., 100 iterations).
- Add exponential backoff or `Thread.onSpinWait()` (Java 9+) between retries.
- Log a warning if max retries are exhausted.

### J-9: Single-threaded executor bottleneck

Using `Executors.newSingleThreadExecutor()` for callback execution creates a bottleneck if callbacks are slow.
- For callback executors, prefer `Executors.newCachedThreadPool()` or a bounded pool with queue.
- If ordering matters, use a single-threaded executor but document the ordering guarantee and throughput limitation.

### J-10: Lock and semaphore release in finally — restore interrupt flag [P0]

All lock acquisitions and semaphore permits MUST be released in a `finally` block.
```java
lock.lock();
try {
    // critical section
} finally {
    lock.unlock();
}
```

**Interrupt flag restoration:** Catching `InterruptedException` and swallowing it silently breaks the cooperative cancellation contract. Always restore the interrupt flag.

```java
// WRONG — interrupt flag lost
try {
    channel.awaitTermination(5, TimeUnit.SECONDS);
} catch (InterruptedException e) {
    logger.warn("Interrupted during shutdown");
    // interrupt flag is cleared — callers never know
}

// RIGHT — restore interrupt flag
try {
    channel.awaitTermination(5, TimeUnit.SECONDS);
} catch (InterruptedException e) {
    Thread.currentThread().interrupt();
    channel.shutdownNow();
}
```

**References:** Effective Java Item 81. fabric8 PR #2429 specifically refactored `InterruptedException` handling.

---

## Dependency Rules

### J-11: `provided` scope requires lazy loading — use strategy pattern and context classloader [P0]

Dependencies with Maven `<scope>provided</scope>` (e.g., OpenTelemetry, SLF4J) may not be present at runtime.
- **NEVER** import `provided`-scope classes directly in eagerly-loaded classes (causes `NoClassDefFoundError`).
- **ALWAYS** use a proxy/factory pattern with classpath detection.
- **ALWAYS** use the thread context classloader — the default `Class.forName()` uses the caller's classloader, which may not see classes in application server environments (Tomcat, WildFly, OSGi).

**Strategy pattern for optional dependencies (OTel example):**

```java
// WRONG — direct import causes NoClassDefFoundError if OTel is absent
import io.opentelemetry.api.trace.Tracer;

public class KubeMQClient {
    private final Tracer tracer = GlobalOpenTelemetry.getTracer("kubemq");
}

// RIGHT — strategy with classpath detection
public interface TelemetryProvider {
    void recordSend(String channel, long latencyNanos, boolean success);
    void recordReceive(String channel, int messageCount);
    Closeable startSpan(String operationName, Map<String, String> attributes);
}

// No-op implementation (always available)
final class NoOpTelemetryProvider implements TelemetryProvider {
    static final NoOpTelemetryProvider INSTANCE = new NoOpTelemetryProvider();
    @Override public void recordSend(String c, long l, boolean s) {}
    @Override public void recordReceive(String c, int m) {}
    @Override public Closeable startSpan(String n, Map<String,String> a) {
        return () -> {};
    }
}

// OTel implementation (loaded only if OTel is on classpath)
// This class is in a separate package and NEVER directly referenced
final class OpenTelemetryProvider implements TelemetryProvider {
    private final Tracer tracer;
    private final Meter meter;
    // ...
}

// Factory (classpath detection with context classloader)
public final class TelemetryProviderFactory {
    public static TelemetryProvider create() {
        try {
            Class.forName(
                "io.opentelemetry.api.GlobalOpenTelemetry",
                false, // don't initialize
                Thread.currentThread().getContextClassLoader()
            );
            return new OpenTelemetryProvider();
        } catch (ClassNotFoundException | NoClassDefFoundError e) {
            return NoOpTelemetryProvider.INSTANCE;
        }
    }
}
```

**Classloader note:** The default `Class.forName(String)` uses the SDK's classloader, which may not see application classes in container environments. Always use the three-argument form with `Thread.currentThread().getContextClassLoader()`.

**References:** Lettuce optionally integrates with Micrometer via `MicrometerOptions`. NATS uses no-op instrumentation when metrics are absent. AWS SDK v2 uses `ExecutionInterceptor` pipeline with optional metric publishers. JDBC `DriverManager` and SLF4J 2.x both use context classloaders.

### J-12: Maven plugin configuration accuracy

When specifying Maven plugin configurations (JaCoCo, Surefire, Spotless, etc.), verify the plugin version exists and the configuration keys are valid. Common mistakes:
- Wrong `<goal>` names
- Non-existent configuration keys
- Version numbers that don't exist on Maven Central

---

## Build Rules

### J-13: Verify gRPC method existence — channel lifecycle and status mapping [P0]

Before referencing a gRPC method (e.g., `SendQueueMessagesBatch`), verify it exists in the `.proto` file or generated stubs. The proto definition is the source of truth — do not assume a method exists because it would be convenient.

**ManagedChannel lifecycle:** Create a single `ManagedChannel` at client construction and reuse it. Never create per-request channels. Always shut down with the two-phase pattern: `shutdown()` then `shutdownNow()` after timeout.

```java
// WRONG — channel per request (connection leak, no reuse)
public Result send(Message msg) {
    ManagedChannel ch = ManagedChannelBuilder.forTarget(address).build();
    try {
        return KubemqGrpc.newBlockingStub(ch).send(msg.toProto());
    } finally {
        ch.shutdown(); // may not complete before GC
    }
}

// RIGHT — two-phase shutdown in close()
@Override
public void close() {
    channel.shutdown();
    try {
        if (!channel.awaitTermination(5, TimeUnit.SECONDS)) {
            channel.shutdownNow();
            if (!channel.awaitTermination(5, TimeUnit.SECONDS)) {
                logger.atWarn().log("Channel did not terminate cleanly");
            }
        }
    } catch (InterruptedException e) {
        channel.shutdownNow();
        Thread.currentThread().interrupt();
    }
}
```

**gRPC status mapping:** Every gRPC `StatusRuntimeException` must be caught and mapped to the appropriate SDK exception type. Never let raw gRPC exceptions leak to SDK users.

```java
// WRONG — gRPC exception leaks to user
public Result send(Message msg) {
    return stub.send(msg.toProto()); // StatusRuntimeException leaks
}

// RIGHT — comprehensive status mapping
private KubeMQException mapGrpcException(StatusRuntimeException e) {
    Status status = Status.fromThrowable(e);
    return switch (status.getCode()) {
        case UNAVAILABLE ->
            new KubeMQConnectionException("Server unavailable", e);
        case DEADLINE_EXCEEDED ->
            new KubeMQTimeoutException("Request timed out", e);
        case UNAUTHENTICATED ->
            new KubeMQAuthenticationException("Invalid credentials", e);
        case PERMISSION_DENIED ->
            new KubeMQAuthorizationException("Insufficient permissions", e);
        case INVALID_ARGUMENT ->
            new KubeMQValidationException(status.getDescription(), e);
        case NOT_FOUND ->
            new KubeMQNotFoundException(status.getDescription(), e);
        default ->
            new KubeMQServerException(
                status.getDescription(),
                status.getCode().name(),
                e);
    };
}
```

**References:** gRPC-java issue #11020 (channel lifecycle). fabric8 maps HTTP status codes to `KubernetesClientException`. jetcd maps gRPC status to specific exception types.

### J-14: JMH benchmark integration

JMH benchmarks require a specific Maven configuration with `jmh-generator-annprocess` annotation processor. The benchmarks should be in a separate module or use the `maven-shade-plugin` to create an executable benchmark JAR. Do not assume `mvn test` runs JMH benchmarks.

---

## Exception Hierarchy

### J-15: Use unchecked exceptions with a layered hierarchy [P0]

Every major K8s-ecosystem SDK uses unchecked (`RuntimeException`) exceptions:
- **AWS SDK v2:** `SdkException → SdkClientException / AwsServiceException → S3Exception, ...`
- **fabric8:** `KubernetesClientException extends RuntimeException`
- **Lettuce:** `RedisException extends RuntimeException`
- **NATS:** `IOException` (checked) for connect, unchecked for message operations

The KubeMQ SDK **must** use unchecked exceptions to avoid forcing `try/catch` on every call.

```java
// WRONG — flat exception hierarchy
public class KubeMQException extends RuntimeException { ... }
// Every catch block catches everything, no granularity

// RIGHT — layered hierarchy (AWS SDK v2 pattern)
public abstract class KubeMQException extends RuntimeException {
    private final String errorCode;
    protected KubeMQException(String message, String errorCode, Throwable cause) {
        super(message, cause);
        this.errorCode = errorCode;
    }
    public String errorCode() { return errorCode; }
}

// SDK-side errors (config, serialization, connectivity)
public class KubeMQClientException extends KubeMQException { ... }

// Server-side errors (returned by KubeMQ server)
public class KubeMQServerException extends KubeMQException {
    private final io.grpc.Status.Code grpcStatusCode;
    public io.grpc.Status.Code grpcStatusCode() { return grpcStatusCode; }
}

// Specific subtypes for programmatic handling
public class KubeMQAuthenticationException extends KubeMQClientException { ... }
public class KubeMQConnectionException extends KubeMQClientException { ... }
public class KubeMQTimeoutException extends KubeMQClientException { ... }
```

**References:** AWS SDK v2 (`SdkException` → `AwsServiceException`), fabric8 (`KubernetesClientException`)

### J-16: Exception cause chaining is mandatory [P0]

Every exception that wraps another must preserve the cause chain. Losing the original stack trace makes debugging impossible.

```java
// WRONG — original cause lost
catch (StatusRuntimeException e) {
    throw new KubeMQServerException("Send failed: " + e.getMessage());
}

// WRONG — message-only, no cause
catch (StatusRuntimeException e) {
    throw new KubeMQServerException(e.getMessage(), e);
    // ↑ OK but missing error code extraction
}

// RIGHT — full context preserved
catch (StatusRuntimeException e) {
    Status status = Status.fromThrowable(e);
    throw new KubeMQServerException(
        "Send failed: " + status.getDescription(),
        mapGrpcCode(status.getCode()),
        e  // cause chain preserved
    );
}
```

**References:** fabric8 constructors always accept `Throwable cause`, jetcd wraps gRPC status.

### J-17: Exceptions must not expose sensitive data in `toString()` / `getMessage()` [P0]

Never include credentials, tokens, or full connection strings in exception messages. These end up in logs, monitoring systems, and error-reporting tools.

```java
// WRONG — token in exception message
throw new KubeMQAuthenticationException(
    "Auth failed with token: " + authToken);

// WRONG — full connection string with credentials
throw new KubeMQConnectionException(
    "Cannot connect to " + connectionString);

// RIGHT — redacted
throw new KubeMQAuthenticationException(
    "Authentication failed for client '" + clientId + "'");

// RIGHT — mask sensitive parts
throw new KubeMQConnectionException(
    "Cannot connect to " + redactUri(address));
```

**References:** AWS SDK v2 never includes credentials in exceptions. Lettuce masks passwords in connection URIs.

### J-18: Do NOT implement `Serializable` on exception types [P1]

SDK exceptions should **not** implement `Serializable` beyond what `Throwable` already provides. Adding serialization-specific fields (like gRPC `Status` objects or `Channel` references) creates `NotSerializableException` when the exception crosses serialization boundaries (RMI, distributed caches).

```java
// WRONG — non-serializable field in a Serializable exception
public class KubeMQServerException extends KubeMQException {
    private final io.grpc.Status grpcStatus; // Status is NOT Serializable
}

// RIGHT — store only serializable primitives
public class KubeMQServerException extends KubeMQException {
    private final int grpcStatusCodeValue;    // int is serializable
    private final String grpcStatusDescription; // String is serializable
}
```

**References:** fabric8 `KubernetesClientException` stores HTTP code as `int` and status as a serializable POJO.

---

## CompletableFuture Patterns

### J-19: Prefer `join()` over `get()` in SDK internals [P0]

`CompletableFuture.get()` wraps failures in checked `ExecutionException`, requiring ugly unwrapping. `join()` wraps in unchecked `CompletionException`, aligning with the unchecked exception strategy from J-15.

```java
// WRONG — forces checked exception handling on callers
public Result send(Message msg) throws ExecutionException, InterruptedException {
    return sendAsync(msg).get();
}

// WRONG — get() with timeout still wraps in ExecutionException
public Result send(Message msg) throws Exception {
    return sendAsync(msg).get(30, TimeUnit.SECONDS);
}

// RIGHT — join() throws unchecked CompletionException
public Result send(Message msg) {
    try {
        return sendAsync(msg).join();
    } catch (CompletionException e) {
        throw unwrap(e);
    }
}

// RIGHT — proper unwrapping utility
private static KubeMQException unwrap(CompletionException e) {
    Throwable cause = e.getCause();
    if (cause instanceof KubeMQException kex) return kex;
    if (cause instanceof StatusRuntimeException sre) {
        return KubeMQServerException.fromGrpcStatus(sre);
    }
    return new KubeMQClientException("Unexpected error", cause);
}
```

**References:** AWS SDK v2 uses `join()` in sync wrappers. Lettuce unwraps `CompletionException` in `LettuceFutures`.

### J-20: Always supply a custom Executor for async operations [P0]

`CompletableFuture.supplyAsync(task)` without an executor uses `ForkJoinPool.commonPool()`, which has only `Runtime.getRuntime().availableProcessors() - 1` threads. An SDK performing I/O on this pool can starve the entire application.

```java
// WRONG — uses ForkJoinPool.commonPool(), starves application
public CompletableFuture<Result> sendAsync(Message msg) {
    return CompletableFuture.supplyAsync(() -> doSend(msg));
}

// WRONG — thenApplyAsync without executor also uses commonPool
return sendAsync(msg).thenApplyAsync(this::processResult);

// RIGHT — SDK-managed executor
private final ExecutorService executor;

public CompletableFuture<Result> sendAsync(Message msg) {
    return CompletableFuture.supplyAsync(() -> doSend(msg), executor);
}

// RIGHT — executor supplied to all async stages
return sendAsync(msg).thenApplyAsync(this::processResult, executor);
```

**The executor must be shut down in `close()`** (see J-27).

**References:** Kafka `KafkaProducer` uses its own `Sender` thread. Lettuce uses Netty event loops. AWS SDK v2 uses `SdkAsyncHttpClient` with dedicated thread pools.

### J-21: Use `thenCompose` for chaining async operations, not `thenApply` [P1]

`thenApply` is for synchronous transformations. Using it for async operations creates `CompletableFuture<CompletableFuture<T>>` — a nested future that callers must manually unwrap.

```java
// WRONG — nested futures
public CompletableFuture<CompletableFuture<Result>> sendWithRetry(Message msg) {
    return sendAsync(msg)
        .thenApply(result -> {
            if (result.isRetryable()) {
                return sendAsync(msg); // returns CF<Result>, not Result
            }
            return CompletableFuture.completedFuture(result);
        });
}

// RIGHT — flat chain
public CompletableFuture<Result> sendWithRetry(Message msg) {
    return sendAsync(msg)
        .thenCompose(result -> {
            if (result.isRetryable()) {
                return sendAsync(msg); // properly composed
            }
            return CompletableFuture.completedFuture(result);
        });
}
```

### J-22: Apply timeouts explicitly on CompletableFuture [P1]

`CompletableFuture` has no built-in timeout before Java 9. Even in Java 9+, `orTimeout()` must be called explicitly — it is never automatic.

```java
// WRONG — hangs forever if server never responds
public CompletableFuture<Result> sendAsync(Message msg) {
    return CompletableFuture.supplyAsync(() -> doSend(msg), executor);
}

// RIGHT (Java 9+) — explicit timeout
public CompletableFuture<Result> sendAsync(Message msg) {
    return CompletableFuture.supplyAsync(() -> doSend(msg), executor)
        .orTimeout(config.requestTimeoutMs(), TimeUnit.MILLISECONDS);
}

// RIGHT (Java 8) — scheduled timeout
public CompletableFuture<Result> sendAsync(Message msg) {
    CompletableFuture<Result> future =
        CompletableFuture.supplyAsync(() -> doSend(msg), executor);
    scheduler.schedule(
        () -> future.completeExceptionally(
            new KubeMQTimeoutException("Request timed out")),
        config.requestTimeoutMs(), TimeUnit.MILLISECONDS);
    return future;
}
```

**References:** AWS SDK v2 applies `ApiCallTimeout` and `ApiCallAttemptTimeout`. NATS uses `Duration`-based request timeouts. jetcd uses `retryDelay` and explicit deadlines.

---

## Builder Pattern

### J-23: Builders must validate required fields in `build()` [P0]

Missing required fields must throw `IllegalArgumentException` at build time, not `NullPointerException` at usage time.

```java
// WRONG — no validation, NPE at runtime later
public static class Builder {
    private String address;
    public KubeMQClient build() {
        return new KubeMQClient(this);
    }
}

// WRONG — validation uses generic exception
public KubeMQClient build() {
    if (address == null) throw new RuntimeException("address missing");
    return new KubeMQClient(this);
}

// RIGHT — clear validation with IllegalArgumentException
public KubeMQClient build() {
    if (address == null || address.isBlank()) {
        throw new IllegalArgumentException(
            "KubeMQClient.Builder: 'address' is required and must not be blank");
    }
    if (port <= 0 || port > 65535) {
        throw new IllegalArgumentException(
            "KubeMQClient.Builder: 'port' must be between 1 and 65535, got " + port);
    }
    return new KubeMQClient(this);
}
```

**References:** AWS SDK v2 validates in `build()`. NATS `Options.Builder` validates servers list. Lettuce `RedisURI.Builder` validates host/port.

### J-24: Builders must be inner static classes [P1]

Builders must be `public static` inner classes so they can be instantiated without an existing instance of the outer class.

```java
// WRONG — non-static inner class (requires outer instance)
public class KubeMQClient {
    public class Builder { ... }
}

// WRONG — separate top-level class (poor discoverability)
public class KubeMQClientBuilder { ... }

// RIGHT — static inner class with static factory method
public class KubeMQClient implements AutoCloseable {
    private KubeMQClient(Builder builder) { ... }

    public static Builder builder() {
        return new Builder();
    }

    public static final class Builder {
        private String address;
        private int port = 50000; // sensible default

        private Builder() {}  // prevent external instantiation

        public Builder address(String address) {
            this.address = address;
            return this;
        }

        public KubeMQClient build() { ... }
    }
}
```

**References:** All major SDKs use this pattern — AWS SDK v2, Lettuce `ClientOptions.Builder`, NATS `Options.Builder`, Kafka uses `Properties` but newer APIs (AdminClient) use builder-like patterns.

### J-25: Builder setters must return `this` for fluent chaining [P2]

Builder methods that set values must return the builder instance. Void-returning setters break fluent chaining and feel unidiomatic in Java.

```java
// WRONG — void setters
public void setAddress(String address) { this.address = address; }
public void setPort(int port) { this.port = port; }

// WRONG — "set" prefix is unnecessary on builders
public Builder setAddress(String address) { ... }

// RIGHT — no "set" prefix, returns this
public Builder address(String address) {
    this.address = address;
    return this;
}
```

**References:** AWS SDK v2, Lettuce, OkHttp, all modern Java SDKs use this convention.

---

## Resource Management

### J-26: Client classes must implement `AutoCloseable` [P0]

Any class that holds resources (gRPC channels, executors, scheduled tasks) **must** implement `AutoCloseable` to work with try-with-resources.

```java
// WRONG — no AutoCloseable, resource leak
public class KubeMQClient {
    private final ManagedChannel channel;

    public void disconnect() {
        channel.shutdown();
    }
}

// RIGHT — implements AutoCloseable
public class KubeMQClient implements AutoCloseable {
    private final ManagedChannel channel;
    private final ExecutorService executor;

    @Override
    public void close() {
        executor.shutdown();
        channel.shutdown();
        try {
            if (!channel.awaitTermination(5, TimeUnit.SECONDS)) {
                channel.shutdownNow();
            }
            if (!executor.awaitTermination(5, TimeUnit.SECONDS)) {
                executor.shutdownNow();
            }
        } catch (InterruptedException e) {
            channel.shutdownNow();
            executor.shutdownNow();
            Thread.currentThread().interrupt();
        }
    }
}
```

**References:** fabric8 `KubernetesClient extends Closeable`. Lettuce `StatefulRedisConnection extends AutoCloseable`. AWS SDK v2 `SdkClient extends SdkAutoCloseable`. Kafka `AdminClient extends AutoCloseable`.

### J-27: `close()` must shut down ALL managed resources [P0]

`close()` must release resources in reverse order of creation: cancel scheduled tasks → shut down executors → close channels. Missing any resource causes thread leaks.

```java
// WRONG — only closes channel, leaks executor and scheduler
@Override
public void close() {
    channel.shutdown();
}

// WRONG — doesn't handle InterruptedException properly
@Override
public void close() {
    channel.shutdown();
    channel.awaitTermination(5, TimeUnit.SECONDS); // unhandled InterruptedException
    executor.shutdown();
}

// RIGHT — complete shutdown sequence
@Override
public void close() {
    // 1. Stop accepting new work
    reconnectScheduler.shutdownNow();

    // 2. Shut down executor (allow in-flight work to complete)
    executor.shutdown();

    // 3. Shut down gRPC channel
    channel.shutdown();

    try {
        // 4. Await orderly termination
        if (!executor.awaitTermination(5, TimeUnit.SECONDS)) {
            executor.shutdownNow();
        }
        if (!channel.awaitTermination(5, TimeUnit.SECONDS)) {
            channel.shutdownNow();
        }
    } catch (InterruptedException e) {
        executor.shutdownNow();
        channel.shutdownNow();
        Thread.currentThread().interrupt();
    }
}
```

**References:** AWS SDK v2 closes HTTP client, credential providers, and event loops. Lettuce shuts down Netty event loops and connection pools. gRPC docs mandate `awaitTermination` after `shutdown`.

### J-28: Restore interrupt flag after catching `InterruptedException` [P0]

Catching `InterruptedException` and swallowing it silently breaks the cooperative cancellation contract. Always restore the interrupt flag.

```java
// WRONG — interrupt flag lost
try {
    channel.awaitTermination(5, TimeUnit.SECONDS);
} catch (InterruptedException e) {
    logger.warn("Interrupted during shutdown");
    // interrupt flag is cleared — callers never know
}

// RIGHT — restore interrupt flag
try {
    channel.awaitTermination(5, TimeUnit.SECONDS);
} catch (InterruptedException e) {
    Thread.currentThread().interrupt();
    channel.shutdownNow();
}
```

**References:** Effective Java Item 81. fabric8 PR #2429 specifically refactored `InterruptedException` handling. Every major SDK follows this pattern.

---

## Configuration

### J-29: Configuration precedence: explicit builder > env vars > system props > defaults [P1]

SDKs should support multiple configuration sources with a clear, documented precedence order.

```java
// RIGHT — layered configuration resolution
public static final class Builder {
    private String address;
    private int port = -1;

    public KubeMQClient build() {
        String resolvedAddress = firstNonNull(
            address,                                        // explicit builder
            System.getenv("KUBEMQ_ADDRESS"),                // environment variable
            System.getProperty("kubemq.address"),           // system property
            null                                            // no default — required
        );
        int resolvedPort = firstPositive(
            port,                                           // explicit builder
            intEnv("KUBEMQ_PORT"),                           // env
            intProp("kubemq.port"),                          // sysprop
            50000                                            // default
        );
        if (resolvedAddress == null) {
            throw new IllegalArgumentException("address is required");
        }
        return new KubeMQClient(resolvedAddress, resolvedPort);
    }
}
```

**References:** AWS SDK v2 follows: explicit > env > system property > profile > EC2 metadata. Kafka uses `Properties` with sensible defaults. NATS reads `NATS_URL` env var as fallback.

### J-30: Defensive copies for mutable configuration [P1]

Builders that accept mutable collections must copy them. Otherwise, callers can modify the collection after `build()`, causing undefined behavior.

```java
// WRONG — stores reference to caller's mutable list
public Builder serverAddresses(List<String> addresses) {
    this.addresses = addresses; // caller can modify after build()
    return this;
}

// RIGHT — defensive copy
public Builder serverAddresses(List<String> addresses) {
    this.addresses = List.copyOf(addresses); // immutable copy (Java 10+)
    return this;
}

// RIGHT (Java 8) — defensive copy
public Builder serverAddresses(List<String> addresses) {
    this.addresses = Collections.unmodifiableList(new ArrayList<>(addresses));
    return this;
}
```

**References:** Effective Java Item 50. AWS SDK v2 copies all collection parameters. NATS copies server list in `Options.Builder`.

---

## Null Safety

### J-31: Public API must document nullability [P1]

All public method parameters and return types must be annotated with `@Nullable` or `@NonNull`. Use `org.jspecify:jspecify` annotations (the emerging standard, endorsed by Google, JetBrains, and Eclipse).

```java
// WRONG — caller doesn't know if null is allowed
public Result send(Message message, Map<String, String> metadata) { ... }
public String getClientId() { ... }

// RIGHT — nullability is explicit
import org.jspecify.annotations.NonNull;
import org.jspecify.annotations.Nullable;

public @NonNull Result send(
    @NonNull Message message,
    @Nullable Map<String, String> metadata
) { ... }

public @Nullable String getClientId() { ... }
```

Use `@NullMarked` at the package level to mark all types as `@NonNull` by default:
```java
// package-info.java
@NullMarked
package io.kubemq.sdk.client;

import org.jspecify.annotations.NullMarked;
```

**References:** AWS SDK v2 uses custom `@SdkInternalApi` annotations. Google's guava uses `@Nullable`. JSpecify is the converging standard across the ecosystem (1.0 released 2024).

### J-32: Prefer `Optional` for return types, never for parameters [P1]

`Optional` is designed for return types to signal "may be absent." Using it for parameters forces callers to wrap values unnecessarily.

```java
// WRONG — Optional as parameter
public void send(Message msg, Optional<Duration> timeout) { ... }

// WRONG — returning null without Optional or documentation
public String getLastError() { return null; } // caller doesn't know to check

// RIGHT — Optional for return types
public Optional<String> getLastError() {
    return Optional.ofNullable(lastError);
}

// RIGHT — overloaded methods or nullable parameter
public void send(Message msg) { send(msg, defaultTimeout); }
public void send(Message msg, Duration timeout) { ... }
```

**References:** AWS SDK v2 uses `Optional` for optional return values. Effective Java Item 55.

---

## Logging

### J-33: Use SLF4J 2.x fluent API for structured logging [P1]

SLF4J 2.0+ provides a fluent logging API via `LoggingEventBuilder` that supports key-value pairs. This is cleaner than string concatenation and avoids the argument-count limitations of the parameterized API.

```java
// WRONG — string concatenation (always evaluates, even if level disabled)
logger.debug("Connected to " + address + " with client " + clientId);

// WRONG — SLF4J 1.x parameterized (limited to a few args, no structure)
logger.debug("Connected to {} with client {}", address, clientId);

// RIGHT — SLF4J 2.x fluent API (structured, lazy evaluation)
logger.atDebug()
    .addKeyValue("address", address)
    .addKeyValue("clientId", clientId)
    .log("Connected to server");
```

**Important:** SLF4J is a `provided` dependency. The `Logger` field must be obtained via `LoggerFactory.getLogger(getClass())` — see J-11 about lazy loading.

**References:** SLF4J 2.0 manual. Spring Boot 3.x uses SLF4J 2.x fluent API internally.

### J-34: Use MDC for request-scoped context in logging [P2]

MDC (Mapped Diagnostic Context) should be used for request-scoped values like trace IDs, client IDs, and channel names. This avoids polluting every log statement with the same parameters.

```java
// WRONG — repeating context in every log statement
logger.info("Sending message, clientId={}, channel={}", clientId, channel);
logger.info("Message sent, clientId={}, channel={}, latency={}ms",
    clientId, channel, latency);

// RIGHT — set MDC once, use in log pattern
MDC.put("kubemq.clientId", clientId);
MDC.put("kubemq.channel", channel);
try {
    logger.info("Sending message");
    // ... operation ...
    logger.info("Message sent, latency={}ms", latency);
} finally {
    MDC.remove("kubemq.clientId");
    MDC.remove("kubemq.channel");
}
```

**Caveat:** MDC is thread-local. For `CompletableFuture` chains, MDC context must be captured and restored manually or via a wrapping executor:
```java
// MDC-propagating executor
public class MdcExecutor implements Executor {
    private final Executor delegate;
    public MdcExecutor(Executor delegate) { this.delegate = delegate; }

    @Override
    public void execute(Runnable command) {
        Map<String, String> context = MDC.getCopyOfContextMap();
        delegate.execute(() -> {
            MDC.setContextMap(context != null ? context : Map.of());
            try { command.run(); }
            finally { MDC.clear(); }
        });
    }
}
```

**References:** Logback MDC documentation. Spring Sleuth uses MDC for trace propagation.

---

## gRPC Patterns

### J-35: Map gRPC status codes to SDK exception types consistently [P0]

Every gRPC `StatusRuntimeException` must be caught and mapped to the appropriate SDK exception type. Never let raw gRPC exceptions leak to SDK users.

```java
// WRONG — gRPC exception leaks to user
public Result send(Message msg) {
    return stub.send(msg.toProto()); // StatusRuntimeException leaks
}

// RIGHT — comprehensive status mapping
private KubeMQException mapGrpcException(StatusRuntimeException e) {
    Status status = Status.fromThrowable(e);
    return switch (status.getCode()) {
        case UNAVAILABLE ->
            new KubeMQConnectionException("Server unavailable", e);
        case DEADLINE_EXCEEDED ->
            new KubeMQTimeoutException("Request timed out", e);
        case UNAUTHENTICATED ->
            new KubeMQAuthenticationException("Invalid credentials", e);
        case PERMISSION_DENIED ->
            new KubeMQAuthorizationException("Insufficient permissions", e);
        case INVALID_ARGUMENT ->
            new KubeMQValidationException(status.getDescription(), e);
        case NOT_FOUND ->
            new KubeMQNotFoundException(status.getDescription(), e);
        default ->
            new KubeMQServerException(
                status.getDescription(),
                status.getCode().name(),
                e);
    };
}
```

**References:** fabric8 maps HTTP status codes to `KubernetesClientException`. jetcd maps gRPC status to specific exception types. AWS SDK v2 maps HTTP codes to service-specific exceptions.

### J-36: `ManagedChannel` lifecycle — one channel per client, shutdown in `close()` [P0]

Create a single `ManagedChannel` at client construction and reuse it. Never create per-request channels. Always shut down with the two-phase pattern: `shutdown()` then `shutdownNow()` after timeout.

```java
// WRONG — channel per request (connection leak, no reuse)
public Result send(Message msg) {
    ManagedChannel ch = ManagedChannelBuilder.forTarget(address).build();
    try {
        return KubemqGrpc.newBlockingStub(ch).send(msg.toProto());
    } finally {
        ch.shutdown(); // may not complete before GC
    }
}

// WRONG — shutdown without awaiting termination
@Override
public void close() {
    channel.shutdown(); // in-flight RPCs may be interrupted
}

// RIGHT — two-phase shutdown
@Override
public void close() {
    channel.shutdown();
    try {
        if (!channel.awaitTermination(5, TimeUnit.SECONDS)) {
            channel.shutdownNow();
            if (!channel.awaitTermination(5, TimeUnit.SECONDS)) {
                logger.atWarn().log("Channel did not terminate cleanly");
            }
        }
    } catch (InterruptedException e) {
        channel.shutdownNow();
        Thread.currentThread().interrupt();
    }
}
```

**Reconnection note:** When reconnecting (e.g., after a connection failure), always shut down the old `ManagedChannel` before creating a new one. Failing to do so leaks the old channel's threads and connections.
```java
// WRONG — leaks old channel on reconnect
private void reconnect() {
    this.channel = ManagedChannelBuilder.forTarget(address).build(); // old channel leaked
}

// RIGHT — shutdown old channel first
private void reconnect() {
    ManagedChannel old = this.channel;
    if (old != null) {
        old.shutdownNow(); // force-close old channel
    }
    this.channel = ManagedChannelBuilder.forTarget(address).build();
}
```

**References:** gRPC-java issue #11020 (channel lifecycle). Best practice: one channel per target, stubs are lightweight and can be created per-call.

### J-37: `ClientInterceptor` chain ordering matters [P1]

Interceptors execute in registration order for requests (first registered → first executed) and reverse order for responses. Auth interceptors must run before tracing interceptors so that auth metadata is visible in traces.

```java
// WRONG — tracing interceptor registered before auth (traces miss auth metadata)
ManagedChannel channel = ManagedChannelBuilder.forTarget(address)
    .intercept(new TracingInterceptor())   // runs first on request
    .intercept(new AuthInterceptor(token)) // runs second — too late for tracing
    .build();

// RIGHT — auth first, tracing wraps the authenticated call
ManagedChannel channel = ManagedChannelBuilder.forTarget(address)
    .intercept(
        new AuthInterceptor(token),     // adds auth metadata
        new TracingInterceptor(),       // traces the call including auth
        new RetryInterceptor()          // retry wraps everything
    )
    .build();
```

**Note:** `ManagedChannelBuilder.intercept(List)` applies interceptors in reverse list order (last in list executes first on outgoing calls). Verify the ordering semantics in your gRPC version.

**References:** gRPC-java issue #11339. gRPC docs on interceptor ordering.

### J-38: Use `CallOptions` for per-RPC deadlines, not channel-level defaults [P1]

Channel-level deadlines apply to all RPCs. Per-RPC deadlines should use `CallOptions` via stub configuration.

```java
// WRONG — channel-level default affects all operations
ManagedChannelBuilder.forTarget(address)
    .defaultLoadBalancingPolicy("round_robin")
    // No way to set per-call deadline here

// RIGHT — per-RPC deadline on stub
KubemqGrpc.KubemqBlockingStub stub = KubemqGrpc.newBlockingStub(channel)
    .withDeadlineAfter(config.requestTimeoutMs(), TimeUnit.MILLISECONDS);

// RIGHT — different timeouts for different operations
KubemqGrpc.KubemqBlockingStub fastStub = baseStub
    .withDeadlineAfter(5, TimeUnit.SECONDS);    // for ping/health
KubemqGrpc.KubemqBlockingStub slowStub = baseStub
    .withDeadlineAfter(30, TimeUnit.SECONDS);   // for batch send
```

**References:** gRPC-java `CallOptions` Javadoc. jetcd uses per-call deadlines.

### J-39: TLS configuration — prefer `TlsChannelCredentials` over raw Netty `SslContext` [P1]

Modern gRPC-java provides `TlsChannelCredentials` as a transport-agnostic TLS API. Raw Netty `SslContext` ties the SDK to a specific transport.

```java
// WRONG — ties SDK to Netty transport
SslContext sslContext = GrpcSslContexts.forClient()
    .trustManager(new File("ca.pem"))
    .build();
ManagedChannel channel = NettyChannelBuilder.forTarget(address)
    .sslContext(sslContext)
    .build();

// RIGHT — transport-agnostic TLS
ChannelCredentials creds = TlsChannelCredentials.newBuilder()
    .trustManager(new File("ca.pem"))
    .build();
ManagedChannel channel = Grpc.newChannelBuilder(address, creds).build();

// RIGHT — mTLS with client certificate
ChannelCredentials creds = TlsChannelCredentials.newBuilder()
    .keyManager(new File("client.pem"), new File("client.key"))
    .trustManager(new File("ca.pem"))
    .build();
```

**References:** gRPC-java TLS guide. `Grpc.newChannelBuilder()` is the recommended entry point since gRPC 1.36+.

### J-40: Metadata injection for auth tokens via `CallCredentials` [P1]

Auth tokens must be injected via `CallCredentials` (per-RPC) or `ClientInterceptor` (channel-wide), not by modifying the channel builder.

```java
// WRONG — token hardcoded at channel creation, cannot refresh
ManagedChannel channel = ManagedChannelBuilder.forTarget(address)
    .intercept(new Metadata.Key... /* manual metadata */)
    .build();

// RIGHT — CallCredentials for refreshable auth
public class KubeMQCallCredentials extends CallCredentials {
    private final Supplier<String> tokenSupplier;

    public KubeMQCallCredentials(Supplier<String> tokenSupplier) {
        this.tokenSupplier = tokenSupplier;
    }

    @Override
    public void applyRequestMetadata(
            RequestInfo requestInfo,
            Executor appExecutor,
            MetadataApplier applier) {
        Metadata metadata = new Metadata();
        metadata.put(
            Metadata.Key.of("authorization", Metadata.ASCII_STRING_MARSHALLER),
            "Bearer " + tokenSupplier.get());
        applier.apply(metadata);
    }
}

// Usage
stub = stub.withCallCredentials(new KubeMQCallCredentials(this::getToken));
```

**References:** gRPC-java auth guide. AWS SDK v2 uses `CredentialsProvider` with refresh.

### J-41: gRPC retry via service config JSON [P2]

gRPC has built-in retry support via service config. Use it instead of hand-rolled retry loops when applicable.

```java
// WRONG — manual retry loop with custom backoff (duplicates gRPC functionality)
for (int attempt = 0; attempt < maxRetries; attempt++) {
    try {
        return stub.send(request);
    } catch (StatusRuntimeException e) {
        if (e.getStatus().getCode() != Status.Code.UNAVAILABLE) throw e;
        Thread.sleep(backoff);
    }
}

// RIGHT — gRPC built-in retry via service config
String retryConfig = """
    {
      "methodConfig": [{
        "name": [{"service": "kubemq.Kubemq"}],
        "retryPolicy": {
          "maxAttempts": 5,
          "initialBackoff": "0.5s",
          "maxBackoff": "30s",
          "backoffMultiplier": 2,
          "retryableStatusCodes": ["UNAVAILABLE"]
        }
      }]
    }
    """;

ManagedChannel channel = ManagedChannelBuilder.forTarget(address)
    .defaultServiceConfig(new Gson().fromJson(retryConfig, Map.class))
    .enableRetry()
    .build();
```

**Note:** Application-level retry is still needed for non-idempotent operations or when you need custom retry predicates beyond status codes.

**References:** gRPC retry design doc. gRPC-java retry configuration guide.

### J-42: Use `io.grpc.Context` for request-scoped data propagation [P2]

`io.grpc.Context` propagates request-scoped data (trace IDs, auth, deadlines) across thread boundaries in gRPC. Do not use `ThreadLocal` for this — it breaks when gRPC switches threads internally.

```java
// WRONG — ThreadLocal breaks across gRPC thread switches
private static final ThreadLocal<String> TRACE_ID = new ThreadLocal<>();

// RIGHT — gRPC Context key
public static final Context.Key<String> TRACE_ID_KEY =
    Context.key("kubemq-trace-id");

// Attaching context
Context ctx = Context.current().withValue(TRACE_ID_KEY, traceId);
Context previous = ctx.attach();
try {
    stub.send(request);
} finally {
    ctx.detach(previous);
}

// Reading in interceptor
String traceId = TRACE_ID_KEY.get(); // reads from current gRPC context
```

**References:** gRPC-java `Context` Javadoc. OpenTelemetry uses gRPC Context for span propagation.

---

## Reconnection and Resilience

### J-43: Reconnection must use jittered exponential backoff [P0]

Reconnection without jitter causes thundering-herd problems when a server restarts and all clients reconnect simultaneously.

```java
// WRONG — fixed delay (thundering herd on server restart)
while (!connected) {
    try { connect(); }
    catch (Exception e) {
        Thread.sleep(2000); // all clients reconnect at the same instant
    }
}

// WRONG — exponential backoff without jitter (still synchronized)
long delay = Math.min(baseDelay * (1L << attempt), maxDelay);
Thread.sleep(delay);

// RIGHT — exponential backoff with full jitter (AWS recommended)
long delay = ThreadLocalRandom.current().nextLong(
    0, Math.min(baseDelay * (1L << attempt), maxDelay));
Thread.sleep(delay);

// RIGHT — complete reconnection loop
private void reconnectLoop() {
    int attempt = 0;
    while (!closed && !Thread.currentThread().isInterrupted()) {
        try {
            connect();
            attempt = 0; // reset on success
            return;
        } catch (Exception e) {
            attempt++;
            if (attempt >= maxReconnectAttempts) {
                logger.atError().log("Max reconnection attempts reached");
                notifyListeners(ConnectionState.FAILED);
                return;
            }
            long delay = ThreadLocalRandom.current().nextLong(
                0, Math.min(baseDelayMs * (1L << attempt), maxDelayMs));
            logger.atInfo()
                .addKeyValue("attempt", attempt)
                .addKeyValue("delayMs", delay)
                .log("Reconnecting after failure");
            try { Thread.sleep(delay); }
            catch (InterruptedException ie) {
                Thread.currentThread().interrupt();
                return;
            }
        }
    }
}
```

**References:** NATS uses `ReconnectWait` with custom `ReconnectDelayHandler` for jitter. Kafka uses `reconnect.backoff.ms` and `reconnect.backoff.max.ms`. Lettuce uses exponential backoff in `DelayHandler`. AWS architecture blog recommends full jitter.

### J-44: Expose connection state via listener/callback [P1]

Users need visibility into connection state changes for monitoring and operational readiness. Follow the NATS pattern of advisory callbacks.

```java
// WRONG — connection state is opaque
public class KubeMQClient {
    // user has no idea what's happening
}

// RIGHT — connection state listener
public interface ConnectionListener {
    void onConnected(ConnectionInfo info);
    void onDisconnected(Throwable cause);
    void onReconnecting(int attempt, long delayMs);
    void onReconnected(ConnectionInfo info);
    void onClosed();
}

// Builder accepts listener
public Builder connectionListener(ConnectionListener listener) {
    this.connectionListener = listener;
    return this;
}
```

**References:** NATS provides `ConnectionListener` with `CONNECTED`, `DISCONNECTED`, `RECONNECTED` events. Lettuce uses `ConnectionEvents`. Kafka uses state-change callbacks.

---

## Observability

### J-45: OpenTelemetry integration must be fully optional [P0]

OTel dependencies must be `provided` scope. The SDK must function identically whether or not OTel JARs are on the classpath. Use a strategy pattern with classpath detection.

```java
// WRONG — direct import causes NoClassDefFoundError if OTel is absent
import io.opentelemetry.api.trace.Tracer;

public class KubeMQClient {
    private final Tracer tracer = GlobalOpenTelemetry.getTracer("kubemq");
}

// RIGHT — strategy with classpath detection
public interface TelemetryProvider {
    void recordSend(String channel, long latencyNanos, boolean success);
    void recordReceive(String channel, int messageCount);
    Closeable startSpan(String operationName, Map<String, String> attributes);
}

// No-op implementation (always available)
final class NoOpTelemetryProvider implements TelemetryProvider {
    static final NoOpTelemetryProvider INSTANCE = new NoOpTelemetryProvider();
    @Override public void recordSend(String c, long l, boolean s) {}
    @Override public void recordReceive(String c, int m) {}
    @Override public Closeable startSpan(String n, Map<String,String> a) {
        return () -> {};
    }
}

// OTel implementation (loaded only if OTel is on classpath)
// This class is in a separate package and NEVER directly referenced
final class OpenTelemetryProvider implements TelemetryProvider {
    private final Tracer tracer;
    private final Meter meter;
    // ...
}

// Factory (classpath detection)
public final class TelemetryProviderFactory {
    public static TelemetryProvider create() {
        try {
            Class.forName("io.opentelemetry.api.GlobalOpenTelemetry");
            return new OpenTelemetryProvider();
        } catch (ClassNotFoundException | NoClassDefFoundError e) {
            return NoOpTelemetryProvider.INSTANCE;
        }
    }
}
```

**References:** Lettuce optionally integrates with Micrometer via `MicrometerOptions`. NATS uses no-op instrumentation when metrics are absent. AWS SDK v2 uses `ExecutionInterceptor` pipeline with optional metric publishers.

---

## Enterprise Patterns

### J-46: Target Java 11 as minimum, design for Java 17+ [P1]

Java 11 is the safe minimum for broad enterprise support (EOL extended through 2032 by vendors). However, the SDK should be designed to take advantage of Java 17+ features where available and must not use any APIs removed after Java 11.

| Feature | Minimum Java | Usage in SDK |
|---------|-------------|--------------|
| `var` (local variable type inference) | 10 | OK in implementation |
| `List.of()`, `Map.of()` | 9 | OK — use for immutable collections |
| `Optional.orElseThrow()` (no-arg) | 10 | OK |
| `String.isBlank()` | 11 | OK |
| Records | 16 | Not for public API (limits Java 11 users) |
| Sealed classes | 17 | Not for public API |
| Pattern matching `switch` | 21 | Not yet — internal use only |
| Virtual threads | 21 | Support but don't require (see J-49) |

**References:** AWS SDK v2 requires Java 8+. fabric8 requires Java 11+. Spring Framework 6 requires Java 17+. grpc-java requires Java 8+.

### J-47: Publish a Maven BOM for multi-module SDK [P1]

If the KubeMQ SDK is split into multiple modules (e.g., `kubemq-sdk-core`, `kubemq-sdk-pubsub`, `kubemq-sdk-queues`, `kubemq-sdk-otel`), publish a BOM artifact so users can import one version for all modules.

```xml
<!-- kubemq-sdk-bom/pom.xml -->
<project>
    <groupId>io.kubemq</groupId>
    <artifactId>kubemq-sdk-bom</artifactId>
    <version>${project.version}</version>
    <packaging>pom</packaging>

    <dependencyManagement>
        <dependencies>
            <dependency>
                <groupId>io.kubemq</groupId>
                <artifactId>kubemq-sdk-core</artifactId>
                <version>${project.version}</version>
            </dependency>
            <dependency>
                <groupId>io.kubemq</groupId>
                <artifactId>kubemq-sdk-queues</artifactId>
                <version>${project.version}</version>
            </dependency>
        </dependencies>
    </dependencyManagement>
</project>
```

**Consumer usage:**
```xml
<dependencyManagement>
    <dependencies>
        <dependency>
            <groupId>io.kubemq</groupId>
            <artifactId>kubemq-sdk-bom</artifactId>
            <version>4.0.0</version>
            <type>pom</type>
            <scope>import</scope>
        </dependency>
    </dependencies>
</dependencyManagement>

<dependencies>
    <dependency>
        <groupId>io.kubemq</groupId>
        <artifactId>kubemq-sdk-core</artifactId>
        <!-- version managed by BOM -->
    </dependency>
</dependencies>
```

**References:** Azure SDK BOM (`com.azure:azure-sdk-bom`). Spring Boot Starter BOM. AWS SDK v2 BOM (`software.amazon.awssdk:bom`).

### J-48: `provided` scope dependencies must include fallback in Javadoc [P1]

Every feature that requires a `provided`-scope dependency must document what happens when the dependency is absent and how to add it.

```xml
<!-- pom.xml -->
<dependency>
    <groupId>io.opentelemetry</groupId>
    <artifactId>opentelemetry-api</artifactId>
    <version>${otel.version}</version>
    <scope>provided</scope>
    <optional>true</optional>
</dependency>
```

```java
/**
 * Enables OpenTelemetry tracing for all KubeMQ operations.
 *
 * <p>Requires {@code io.opentelemetry:opentelemetry-api} on the classpath.
 * If absent, tracing is silently disabled (no-op).
 *
 * <p>To enable, add to your Maven pom.xml:
 * <pre>{@code
 * <dependency>
 *     <groupId>io.opentelemetry</groupId>
 *     <artifactId>opentelemetry-api</artifactId>
 *     <version>1.35.0</version>
 * </dependency>
 * }</pre>
 *
 * @since 4.0.0
 */
public Builder enableTracing(boolean enabled) { ... }
```

### J-49: Support virtual threads without requiring Java 21 [P2]

The SDK should be compatible with virtual threads (Java 21+) but must not require them. Key constraints:

1. Do not use `synchronized` blocks on I/O paths — virtual threads pin to platform threads inside `synchronized`. Use `ReentrantLock` instead.
2. Do not assume `ThreadLocal` is cheap — virtual threads can create millions of instances.
3. Allow users to supply their own `Executor` (which may be a virtual-thread executor).

```java
// WRONG — synchronized pins virtual threads
public synchronized void send(Message msg) {
    channel.send(msg.toProto());
}

// RIGHT — ReentrantLock doesn't pin
private final ReentrantLock sendLock = new ReentrantLock();

public void send(Message msg) {
    sendLock.lock();
    try {
        channel.send(msg.toProto());
    } finally {
        sendLock.unlock();
    }
}

// RIGHT — allow user-supplied executor (virtual thread compatible)
public Builder executor(Executor executor) {
    this.executor = executor; // user can pass Executors.newVirtualThreadPerTaskExecutor()
    return this;
}
```

**References:** JEP 444 (Virtual Threads). gRPC-java is compatible with virtual threads. Spring Boot 3.2+ supports virtual threads.

### J-50: Javadoc standards for public API [P2]

All public classes and methods must have Javadoc with `@since`, `@param`, `@return`, `@throws`, and code examples for key entry points.

```java
// WRONG — no Javadoc
public CompletableFuture<SendResult> sendAsync(Message message) { ... }

// WRONG — trivial Javadoc that adds no value
/** Sends a message asynchronously. */
public CompletableFuture<SendResult> sendAsync(Message message) { ... }

// RIGHT — complete Javadoc
/**
 * Sends a message to the configured KubeMQ channel asynchronously.
 *
 * <p>The returned future completes when the server acknowledges receipt.
 * If the server is unreachable, the future completes exceptionally with
 * {@link KubeMQConnectionException}.
 *
 * <p>Example usage:
 * <pre>{@code
 * client.sendAsync(message)
 *     .thenAccept(result -> System.out.println("Sent: " + result.id()))
 *     .exceptionally(ex -> { logger.error("Send failed", ex); return null; });
 * }</pre>
 *
 * @param message the message to send; must not be {@code null}
 * @return a future that completes with the send result
 * @throws NullPointerException if {@code message} is null
 * @since 4.0.0
 * @see #send(Message) for the synchronous variant
 */
public CompletableFuture<SendResult> sendAsync(@NonNull Message message) { ... }
```

**References:** AWS SDK v2 Javadoc includes `@since` on every public method. Google Java Style Guide §7.

---

## Testing Patterns

### J-51: Provide a test-double/mock server for users [P1]

The SDK should ship a test utility (in a separate Maven module with `test` scope) that provides an in-process gRPC server for unit testing.

```java
// Test utility module: kubemq-sdk-test
public class KubeMQTestServer implements AutoCloseable {
    private final Server server;
    private final MutableHandlerRegistry serviceRegistry;

    public KubeMQTestServer() {
        serviceRegistry = new MutableHandlerRegistry();
        server = InProcessServerBuilder
            .forName("kubemq-test")
            .fallbackHandlerRegistry(serviceRegistry)
            .directExecutor()
            .build()
            .start();
    }

    public ManagedChannel createChannel() {
        return InProcessChannelBuilder
            .forName("kubemq-test")
            .directExecutor()
            .build();
    }

    public void stubSendResponse(SendResult result) { ... }
    public void stubSendError(Status status) { ... }

    @Override
    public void close() {
        server.shutdownNow();
    }
}
```

**Usage:**
```java
@Test
void sendMessage_success() {
    try (var testServer = new KubeMQTestServer()) {
        testServer.stubSendResponse(SendResult.success("msg-123"));

        var client = KubeMQClient.builder()
            .channel(testServer.createChannel()) // inject test channel
            .build();

        SendResult result = client.send(message);
        assertThat(result.id()).isEqualTo("msg-123");
    }
}
```

**References:** gRPC-java provides `InProcessServer` for testing. AWS SDK v2 provides `MockAsyncHttpClient`. Kafka provides `MockProducer`/`MockConsumer`.

### J-52: Test for thread safety with concurrent test harnesses [P2]

Specs that claim thread safety must include test scenarios exercising concurrent access.

```java
@Test
void client_isThreadSafe() throws Exception {
    int threadCount = 10;
    int opsPerThread = 100;
    CountDownLatch latch = new CountDownLatch(threadCount);
    AtomicInteger errors = new AtomicInteger(0);

    ExecutorService pool = Executors.newFixedThreadPool(threadCount);
    for (int i = 0; i < threadCount; i++) {
        pool.submit(() -> {
            try {
                for (int j = 0; j < opsPerThread; j++) {
                    client.send(testMessage());
                }
            } catch (Exception e) {
                errors.incrementAndGet();
            } finally {
                latch.countDown();
            }
        });
    }

    latch.await(30, TimeUnit.SECONDS);
    assertThat(errors.get()).isZero();
}
```

**References:** Lettuce runs concurrent Redis command tests. gRPC-java uses `CyclicBarrier` in concurrency tests.

---

## Additional Pitfalls

### J-53: `equals`/`hashCode` contracts for value types [P1]

Any class used as a map key, in sets, or compared for equality must implement `equals` and `hashCode` consistently. Missing `hashCode` when `equals` is overridden breaks `HashMap`/`HashSet`.

```java
// WRONG — equals without hashCode
public class ChannelName {
    private final String name;

    @Override
    public boolean equals(Object o) {
        return o instanceof ChannelName cn && name.equals(cn.name);
    }
    // hashCode not overridden — uses Object.hashCode() (identity)
}

// RIGHT — consistent equals/hashCode
public class ChannelName {
    private final String name;

    @Override
    public boolean equals(Object o) {
        if (this == o) return true;
        if (!(o instanceof ChannelName cn)) return false;
        return name.equals(cn.name);
    }

    @Override
    public int hashCode() {
        return name.hashCode();
    }
}
```

**Better:** Use Java 16+ records for value types (if Java 16+ is the minimum):
```java
public record ChannelName(String name) {}
// equals, hashCode, toString auto-generated
```

**References:** Effective Java Items 10-11. Every SDK value type must follow this contract.

### J-54: Maven shade plugin conflicts with gRPC/Protobuf [P0]

If the SDK uses `maven-shade-plugin`, protobuf and gRPC classes **must be relocated** to avoid classpath conflicts with other gRPC users in the same JVM.

```xml
<!-- WRONG — shading without relocation -->
<plugin>
    <groupId>org.apache.maven.plugins</groupId>
    <artifactId>maven-shade-plugin</artifactId>
    <configuration>
        <!-- No relocations — causes "Duplicate class" errors -->
    </configuration>
</plugin>

<!-- RIGHT — relocate conflicting packages -->
<plugin>
    <groupId>org.apache.maven.plugins</groupId>
    <artifactId>maven-shade-plugin</artifactId>
    <configuration>
        <relocations>
            <relocation>
                <pattern>com.google.protobuf</pattern>
                <shadedPattern>io.kubemq.shaded.protobuf</shadedPattern>
            </relocation>
        </relocations>
    </configuration>
</plugin>
```

**Better approach:** Use `grpc-netty-shaded` (official shaded artifact) instead of shading manually:
```xml
<dependency>
    <groupId>io.grpc</groupId>
    <artifactId>grpc-netty-shaded</artifactId>
    <version>${grpc.version}</version>
</dependency>
```

**References:** gRPC-java recommends `grpc-netty-shaded`. Multiple GitHub issues on protobuf version conflicts.

### J-55: SLF4J binding conflicts in transitive dependencies [P1]

The SDK must **never** declare a concrete SLF4J binding (e.g., `logback-classic`, `slf4j-simple`) as a `compile` or `runtime` dependency. Only `slf4j-api` should be a dependency, and it should be `provided` scope.

```xml
<!-- WRONG — forces a logging implementation on users -->
<dependency>
    <groupId>ch.qos.logback</groupId>
    <artifactId>logback-classic</artifactId>
    <version>1.4.14</version>
</dependency>

<!-- WRONG — compile scope for SLF4J API (forces version on users) -->
<dependency>
    <groupId>org.slf4j</groupId>
    <artifactId>slf4j-api</artifactId>
    <version>2.0.12</version>
    <scope>compile</scope>
</dependency>

<!-- RIGHT — provided scope, no binding -->
<dependency>
    <groupId>org.slf4j</groupId>
    <artifactId>slf4j-api</artifactId>
    <version>2.0.12</version>
    <scope>provided</scope>
</dependency>

<!-- In test scope only -->
<dependency>
    <groupId>ch.qos.logback</groupId>
    <artifactId>logback-classic</artifactId>
    <version>1.4.14</version>
    <scope>test</scope>
</dependency>
```

**References:** SLF4J manual — "Embedded components such as libraries or frameworks should not declare a dependency on any SLF4J binding." fabric8, AWS SDK v2, and Kafka all follow this.

### J-56: `@SuppressWarnings` — only acceptable for specific, documented cases [P2]

`@SuppressWarnings` must always include a comment explaining why the suppression is safe. Acceptable uses:

| Suppression | When acceptable |
|------------|-----------------|
| `"unchecked"` | Generic type erasure where type safety is enforced by other means |
| `"deprecation"` | When supporting older API versions that use deprecated methods |
| `"serial"` | On `RuntimeException` subclasses where serialization is intentional |
| `"rawtypes"` | Never in new code — always parameterize generics |

```java
// WRONG — blanket suppression without justification
@SuppressWarnings("unchecked")
public <T> T getResult() {
    return (T) result;
}

// RIGHT — documented, narrow suppression
@SuppressWarnings("unchecked") // Safe: T is always Message, enforced by Builder.messageType()
private <T extends Message> T deserialize(byte[] data) {
    return (T) serializer.deserialize(data);
}
```

### J-57: Do not use `Executors.newFixedThreadPool` without naming threads [P1]

Unnamed threads make debugging impossible. Always use a `ThreadFactory` that names threads with the SDK prefix.

```java
// WRONG — anonymous threads appear as "pool-1-thread-1" in dumps
ExecutorService executor = Executors.newFixedThreadPool(4);

// RIGHT — named threads for debuggability
ExecutorService executor = Executors.newFixedThreadPool(4, new ThreadFactory() {
    private final AtomicInteger counter = new AtomicInteger(0);
    @Override
    public Thread newThread(Runnable r) {
        Thread t = new Thread(r, "kubemq-io-" + counter.incrementAndGet());
        t.setDaemon(true); // daemon threads won't prevent JVM shutdown
        return t;
    }
});
```

**References:** Kafka names threads `kafka-producer-network-thread`, `kafka-coordinator-heartbeat-thread`. Lettuce uses `lettuce-nioEventLoop-*`. NATS uses `jnats-*`.

### J-58: Daemon threads for SDK-managed pools [P1]

All threads created by the SDK must be daemon threads. Non-daemon threads prevent JVM shutdown, causing applications to hang on exit.

```java
// WRONG — non-daemon threads prevent JVM shutdown
Thread reconnectThread = new Thread(this::reconnectLoop);
reconnectThread.start();

// RIGHT — daemon thread
Thread reconnectThread = new Thread(this::reconnectLoop, "kubemq-reconnect");
reconnectThread.setDaemon(true);
reconnectThread.start();

// RIGHT — daemon thread factory for executor
ThreadFactory factory = r -> {
    Thread t = new Thread(r, "kubemq-worker-" + counter.incrementAndGet());
    t.setDaemon(true);
    return t;
};
```

**References:** NATS uses daemon threads for reconnection. Kafka producer threads are daemon by default.

### J-59: Avoid `ClassLoader` assumptions — use `Thread.currentThread().getContextClassLoader()` [P2]

When using `Class.forName()` for optional dependency detection (see J-11), always use the thread context classloader. The default `Class.forName()` uses the caller's classloader, which may not see classes in application server environments (Tomcat, WildFly, OSGi).

```java
// WRONG — uses SDK classloader (may not see application classes)
Class.forName("io.opentelemetry.api.trace.Tracer");

// RIGHT — uses thread context classloader
Class.forName(
    "io.opentelemetry.api.trace.Tracer",
    false, // don't initialize
    Thread.currentThread().getContextClassLoader()
);
```

**References:** JDBC `DriverManager` uses context classloader. SLF4J 2.x uses `ServiceLoader` with context classloader.

### J-60: Stub/placeholder methods must log warnings, never silently discard [P1]

Methods that are intentionally incomplete (e.g., `sendBufferedMessage()` during graceful shutdown, or features not yet implemented) must log at WARN level describing what they are NOT doing. Never silently discard data or operations.

```java
// WRONG — silent no-op (user loses messages with no indication)
public void sendBufferedMessage(QueueMessage msg) {
    // no-op
}

// RIGHT — warn about discarded operation
public void sendBufferedMessage(QueueMessage msg) {
    log.atWarn()
        .addKeyValue("messageId", msg.getId())
        .log("Discarding message — buffer send not implemented");
}
```

**Source:** Implementation retrospective — silent stubs caused data loss that was invisible during testing. Only caught during QA deep review.

### J-61: `@SuppressWarnings("deprecation")` on internal callers of deprecated aliases [P2]

When implementing a `@Deprecated` method alias (e.g., for backward compatibility) and an internal method calls it, the internal caller must add `@SuppressWarnings("deprecation")` to avoid noisy compiler warnings during builds.

```java
// The deprecated alias
@Deprecated
public void setChannel(String channel) {
    this.channelName = channel;
}

// WRONG — internal caller triggers deprecation warning in build output
public void initFromConfig(Config config) {
    setChannel(config.getChannel()); // compiler warning: setChannel is deprecated
}

// RIGHT — suppress on internal caller
@SuppressWarnings("deprecation")
public void initFromConfig(Config config) {
    setChannel(config.getChannel()); // no warning
}
```

**Source:** Implementation retrospective — noisy deprecation warnings in build output from SDK-internal code calling its own deprecated methods.

---

## K8s Ecosystem SDK Pattern Summary

| SDK | Exception Style | Async Model | Config Style | Reconnection | Observability |
|-----|----------------|-------------|--------------|--------------|---------------|
| **AWS SDK v2** | Unchecked hierarchy (`SdkException`) | `CompletableFuture` | Builder per client | Built-in retry with backoff | `ExecutionInterceptor`, optional metrics |
| **fabric8** | Unchecked (`KubernetesClientException`) | `CompletableFuture` | Fluent builder | `ExceptionHandler`-based retry | Micrometer (optional) |
| **kubernetes-client/java** | Mixed checked/unchecked | `CompletableFuture`, callbacks | Builder | Exponential backoff | Minimal |
| **NATS (jnats)** | Checked `IOException` (connect), unchecked (ops) | `CompletableFuture` | `Options.Builder` | Jittered backoff, advisory callbacks | No built-in |
| **Kafka** | Unchecked (`KafkaException`) | `Future<RecordMetadata>`, callbacks | `Properties` | Auto-reconnect with backoff | JMX, Micrometer (optional) |
| **Lettuce** | Unchecked (`RedisException`) | `CompletableFuture`, Reactive | `ClientOptions.Builder` | Auto-reconnect, configurable delay | Micrometer (optional) |
| **jetcd** | Unchecked, wraps gRPC status | `CompletableFuture` | Builder | Configurable retry, limited watch retry | No built-in |
| **grpc-java** | `StatusRuntimeException` (unchecked) | `StreamObserver`, `ListenableFuture` | `ManagedChannelBuilder` | Service config retry | OTel (optional) |

**Recommended KubeMQ Java SDK pattern:** Follow AWS SDK v2 for exception hierarchy and builder design. Follow NATS for reconnection callbacks. Follow Lettuce/fabric8 for resource management. Follow gRPC-java for channel/interceptor patterns.

---

## Quick Reference: Rule Index

| Rule | Section | Priority | Title |
|------|---------|----------|-------|
| J-1 | Syntax | — | No import aliases |
| J-2 | Syntax | — | Method bodies are required |
| J-3 | Syntax | P0 | Checked vs unchecked exceptions — use unchecked with a layered hierarchy |
| J-4 | Syntax | — | Generics are invariant |
| J-5 | Naming | — | Standard library name collisions |
| J-6 | Naming | — | Package naming convention |
| J-7 | Concurrency | — | ThreadLocalRandom is NOT a static field |
| J-8 | Concurrency | — | CAS loops need bounded retry |
| J-9 | Concurrency | — | Single-threaded executor bottleneck |
| J-10 | Concurrency | P0 | Lock and semaphore release in finally — restore interrupt flag |
| J-11 | Dependency | P0 | `provided` scope requires lazy loading — strategy pattern and context classloader |
| J-12 | Dependency | — | Maven plugin configuration accuracy |
| J-13 | Build | P0 | Verify gRPC method existence — channel lifecycle and status mapping |
| J-14 | Build | — | JMH benchmark integration |
| J-15 | Exception Hierarchy | P0 | Use unchecked exceptions with a layered hierarchy |
| J-16 | Exception Hierarchy | P0 | Exception cause chaining is mandatory |
| J-17 | Exception Hierarchy | P0 | Exceptions must not expose sensitive data |
| J-18 | Exception Hierarchy | P1 | Do NOT implement Serializable on exception types |
| J-19 | CompletableFuture | P0 | Prefer `join()` over `get()` in SDK internals |
| J-20 | CompletableFuture | P0 | Always supply a custom Executor for async operations |
| J-21 | CompletableFuture | P1 | Use `thenCompose` for chaining async operations |
| J-22 | CompletableFuture | P1 | Apply timeouts explicitly on CompletableFuture |
| J-23 | Builder | P0 | Builders must validate required fields in `build()` |
| J-24 | Builder | P1 | Builders must be inner static classes |
| J-25 | Builder | P2 | Builder setters must return `this` for fluent chaining |
| J-26 | Resource Management | P0 | Client classes must implement `AutoCloseable` |
| J-27 | Resource Management | P0 | `close()` must shut down ALL managed resources |
| J-28 | Resource Management | P0 | Restore interrupt flag after catching `InterruptedException` |
| J-29 | Configuration | P1 | Configuration precedence |
| J-30 | Configuration | P1 | Defensive copies for mutable configuration |
| J-31 | Null Safety | P1 | Public API must document nullability |
| J-32 | Null Safety | P1 | Prefer `Optional` for return types, never for parameters |
| J-33 | Logging | P1 | Use SLF4J 2.x fluent API for structured logging |
| J-34 | Logging | P2 | Use MDC for request-scoped context in logging |
| J-35 | gRPC Patterns | P0 | Map gRPC status codes to SDK exception types consistently |
| J-36 | gRPC Patterns | P0 | `ManagedChannel` lifecycle |
| J-37 | gRPC Patterns | P1 | `ClientInterceptor` chain ordering matters |
| J-38 | gRPC Patterns | P1 | Use `CallOptions` for per-RPC deadlines |
| J-39 | gRPC Patterns | P1 | TLS configuration — prefer `TlsChannelCredentials` |
| J-40 | gRPC Patterns | P1 | Metadata injection via `CallCredentials` |
| J-41 | gRPC Patterns | P2 | gRPC retry via service config JSON |
| J-42 | gRPC Patterns | P2 | Use `io.grpc.Context` for request-scoped data |
| J-43 | Reconnection | P0 | Reconnection must use jittered exponential backoff |
| J-44 | Reconnection | P1 | Expose connection state via listener/callback |
| J-45 | Observability | P0 | OpenTelemetry integration must be fully optional |
| J-46 | Enterprise | P1 | Target Java 11 as minimum, design for Java 17+ |
| J-47 | Enterprise | P1 | Publish a Maven BOM for multi-module SDK |
| J-48 | Enterprise | P1 | `provided` scope dependencies must include fallback in Javadoc |
| J-49 | Enterprise | P2 | Support virtual threads without requiring Java 21 |
| J-50 | Enterprise | P2 | Javadoc standards for public API |
| J-51 | Testing | P1 | Provide a test-double/mock server for users |
| J-52 | Testing | P2 | Test for thread safety with concurrent test harnesses |
| J-53 | Additional Pitfalls | P1 | `equals`/`hashCode` contracts for value types |
| J-54 | Additional Pitfalls | P0 | Maven shade plugin conflicts with gRPC/Protobuf |
| J-55 | Additional Pitfalls | P1 | SLF4J binding conflicts in transitive dependencies |
| J-56 | Additional Pitfalls | P2 | `@SuppressWarnings` — only for specific, documented cases |
| J-57 | Additional Pitfalls | P1 | Do not use `Executors.newFixedThreadPool` without naming threads |
| J-58 | Additional Pitfalls | P1 | Daemon threads for SDK-managed pools |
| J-59 | Additional Pitfalls | P2 | Avoid `ClassLoader` assumptions |
| J-60 | Additional Pitfalls | P1 | Stub/placeholder methods must log warnings |
| J-61 | Additional Pitfalls | P2 | `@SuppressWarnings("deprecation")` on internal callers of deprecated aliases |
