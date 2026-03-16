# Python Language Constraints for Spec Agents

**Purpose:** Language-specific pitfalls that spec agents MUST follow to prevent common errors in Python SDK specs.

---

## Syntax Rules

### PY-1: Type hints are required but not enforced at runtime
Python type hints (PEP 484) are for documentation and static analysis only.
- All public API methods MUST include type hints in specs.
- Use `from __future__ import annotations` for forward references (Python 3.7+).
- The SDK ships `"Typing :: Typed"` classifier — specs must ensure a `py.typed` marker file exists in the package root.
- Use `TYPE_CHECKING` guards for imports that are only needed by type checkers (avoids circular imports and runtime cost):

```python
# WRONG — imports heavy module at runtime just for a type hint
from kubemq.transport.channel_manager import ChannelManager

class Client:
    def __init__(self, manager: ChannelManager) -> None: ...

# RIGHT — import only during type checking
from __future__ import annotations
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from kubemq.transport.channel_manager import ChannelManager

class Client:
    def __init__(self, manager: ChannelManager) -> None: ...
```

- For Python 3.9 compatibility, use `dict[str, Any]` only with `from __future__ import annotations`. Without it, use `Dict[str, Any]` from `typing`.
- **WRONG:** `def send(self, msg):` — missing type hints
- **RIGHT:** `def send(self, msg: Message) -> Result:` or `async def send(self, msg: Message) -> Result:`

**Reference:** Already partially used in the codebase (`core/config.py` uses `TYPE_CHECKING`). Azure SDK and redis-py both use this pattern extensively.

### PY-2: async vs sync API duplication
Python async (`asyncio`) and sync APIs are fundamentally different — you cannot use `await` in sync code.
- SDKs often need BOTH sync and async interfaces.
- Options: (a) two separate client classes (`Client` and `AsyncClient`), (b) sync wrapper using `asyncio.run()`, (c) single async-only API.
- Specs must explicitly state which approach is used and be consistent.
- **WRONG:** Single class with both `def send()` and `async def send_async()` — confusing API.

### PY-3: No real private access
Python has no true private members. Convention:
- `_name` — "private by convention" (single underscore)
- `__name` — name-mangled (double underscore) — avoid in SDKs, it complicates inheritance
- Use single underscore for internal/private members in specs.
- Public API = no underscore prefix.

### PY-4: Exception hierarchy
Python exceptions extend `Exception` (not `BaseException` — that's for system exits).
```python
class KubeMQError(Exception):
    """Base exception for KubeMQ SDK."""
    def __init__(self, message: str, code: str | None = None):
        super().__init__(message)
        self.code = code
```
- Use `raise ... from original_error` for exception chaining.
- Understand `__cause__` vs `__context__` distinction:

```python
# `raise X from Y` sets __cause__ (explicit chaining, __suppress_context__ = True)
# Implicit chaining (bare `raise X` inside except) sets __context__ only

# WRONG — loses explicit cause chain
try:
    channel.send(msg)
except grpc.RpcError as e:
    raise KubeMQError("send failed")  # __context__ is set but __suppress_context__ is False
                                       # Python shows "During handling... another exception occurred"

# RIGHT — explicit chaining with `from`
try:
    channel.send(msg)
except grpc.RpcError as e:
    raise KubeMQError("send failed") from e  # __cause__ = e, clean traceback

# RIGHT — suppress original if intentional
try:
    channel.send(msg)
except grpc.RpcError:
    raise KubeMQError("send failed") from None  # hides original, use sparingly
```

- **WRONG:** `raise KubeMQError(str(e))` — loses original traceback
- **RIGHT:** `raise KubeMQError("failed") from e`

**Reference:** nats-py, redis-py, and Azure SDK all use explicit `from e` chaining consistently.

### PY-5: Context managers for resource cleanup
Any class that holds resources (gRPC channels, connections) MUST implement context manager protocol:
```python
class Client:
    def __enter__(self) -> "Client": ...
    def __exit__(self, *args) -> None: ...

    # Async version:
    async def __aenter__(self) -> "Client": ...
    async def __aexit__(self, *args) -> None: ...
```
- Specs must show `with Client(...) as client:` usage examples.

Additionally, use `contextlib.asynccontextmanager` and `aclosing()` for async resource patterns:

```python
# For factory functions that yield resources:
from contextlib import asynccontextmanager

@asynccontextmanager
async def create_client(address: str):
    client = AsyncPubSubClient(address=address)
    try:
        await client.connect()
        yield client
    finally:
        await client.close()

# For safely closing async generators:
from contextlib import aclosing  # Python 3.10+

# WRONG — async generator may not be cleaned up if consumer breaks early
async for msg in client.subscribe(channel):
    process(msg)
    if done:
        break  # generator's finally block may not run

# RIGHT — aclosing() ensures generator cleanup
async with aclosing(client.subscribe(channel)) as stream:
    async for msg in stream:
        process(msg)
        if done:
            break  # __aexit__ triggers generator cleanup
```

**Priority:** P0  
**Reference:** nats-py uses `aclosing()` for subscription streams. Azure SDK uses `asynccontextmanager` for credential scoping.

---

## Naming Rules

### PY-6: Standard library name collisions
These names exist in Python's stdlib and should be avoided:

| Name | Module | Risk |
|------|--------|------|
| `ConnectionError` | builtins | High — built-in exception |
| `TimeoutError` | builtins | High — built-in exception |
| `PermissionError` | builtins | High — built-in exception |
| `logging` | stdlib | Medium — module name |
| `queue` | stdlib | Medium — module name |
| `signal` | stdlib | Medium — module name |
| `channel` | N/A but gRPC uses it | Medium |

**Resolution:** Prefix with `KubeMQ` (e.g., `KubeMQConnectionError`, `KubeMQTimeoutError`).

### PY-7: Package and module naming
Python packages and modules use lowercase with underscores:
- **WRONG:** `kubemqSdk`, `KubeMQ_Client`
- **RIGHT:** `kubemq`, `kubemq.client`, `kubemq.exceptions`
- Package structure:
  - `kubemq/` — root package
  - `kubemq/client.py` — client classes
  - `kubemq/exceptions.py` — exception types (NOT `errors.py` — Python convention is `exceptions`)
  - `kubemq/config.py` — configuration
  - `kubemq/_internal/` — internal modules

### PY-8: Method naming convention
All methods and functions use `snake_case`:
- **WRONG:** `sendMessage`, `getMessage`, `isConnected`
- **RIGHT:** `send_message`, `get_message`, `is_connected`
- Properties use `@property` decorator, not `get_*`/`set_*` methods.

---

## Concurrency Rules

### PY-9: asyncio event loop management
Never create a new event loop inside library code.
- **WRONG:** `loop = asyncio.new_event_loop()` inside the SDK
- **RIGHT:** Use `await` in async methods, let the user manage the event loop.
- For sync wrappers, use `asyncio.run()` at the top level only.

For Python 3.11+, `asyncio.Runner` provides more control than `asyncio.run()`:

```python
# WRONG — calling asyncio.run() multiple times creates/destroys loops
def send_batch(messages):
    for msg in messages:
        asyncio.run(async_send(msg))  # creates new loop each time

# RIGHT (3.11+) — reuse a single runner
def send_batch(messages):
    with asyncio.Runner() as runner:
        for msg in messages:
            runner.run(async_send(msg))

# RIGHT (3.9+) — reuse loop explicitly
def send_batch(messages):
    loop = asyncio.new_event_loop()
    try:
        for msg in messages:
            loop.run_until_complete(async_send(msg))
    finally:
        loop.close()
```

**Priority:** P2  
**Reference:** Python 3.11 whatsnew docs.

### PY-10: Thread safety with GIL caveats
Python's GIL protects against some data races but NOT all.
- `dict`, `list` operations are generally atomic for single operations.
- Compound operations (check-then-act) still need `threading.Lock`.
- For async code, use `asyncio.Lock` (NOT `threading.Lock`).
- **WRONG:** Using `threading.Lock` in async code — causes deadlocks.

### PY-11: Graceful shutdown
Async cleanup must use `asyncio` shutdown patterns:
```python
async def close(self) -> None:
    """Gracefully shut down the client."""
    self._running = False
    if self._channel:
        await self._channel.close()
    # Cancel pending tasks
    for task in self._tasks:
        task.cancel()
    await asyncio.gather(*self._tasks, return_exceptions=True)
```
- Always cancel pending tasks and await them.
- Use `atexit` or signal handlers for cleanup in sync mode.

For Python 3.11+, prefer `asyncio.TaskGroup` for structured concurrency:

```python
# WRONG — manual task tracking is error-prone
self._tasks = []
self._tasks.append(asyncio.create_task(self._ping_loop()))
self._tasks.append(asyncio.create_task(self._recv_loop()))

# RIGHT (3.11+) — TaskGroup handles cancellation on error
async def run(self):
    async with asyncio.TaskGroup() as tg:
        tg.create_task(self._ping_loop())
        tg.create_task(self._recv_loop())
    # If either task raises, both are cancelled and ExceptionGroup is raised

# IMPORTANT: Since SDK targets 3.9+, keep the manual pattern as fallback.
import sys
if sys.version_info >= (3, 11):
    # Use TaskGroup
    pass
else:
    # Use manual task tracking + asyncio.gather
    pass
```

**Priority:** P1  
**Reference:** nats-py PR #675 adopted structured concurrency patterns. Python 3.11 docs (PEP 654).

---

## Dependency Rules

### PY-12: Optional dependencies with extras
For optional dependencies (OTel, specific logging backends):
- Declare as extras in `pyproject.toml`:
```toml
[project.optional-dependencies]
otel = [
    "opentelemetry-api>=1.20.0",
    "opentelemetry-sdk>=1.20.0",
    "opentelemetry-instrumentation-grpc>=0.41b0",
]
```
- The current `pyproject.toml` has cli/config/docs extras but NOT otel — specs that reference OTel MUST ensure this is added.
- Use conditional imports with no-op fallback:

```python
# WRONG — importing OTel unconditionally
from opentelemetry import trace
tracer = trace.get_tracer(__name__)

# RIGHT — graceful degradation with no-op
try:
    from opentelemetry import trace
    _HAS_OTEL = True
except ImportError:
    _HAS_OTEL = False

def _get_tracer():
    if _HAS_OTEL:
        return trace.get_tracer("kubemq")
    return None  # or a no-op tracer object
```

- **Never** make OTel a hard dependency in the main `[project.dependencies]`.

**Priority:** P0  
**Reference:** Azure SDK uses `azure-core[tracing]` extras. The `opentelemetry-instrumentation-grpc` package provides `GrpcAioInstrumentorClient` for async gRPC clients.

### PY-13: Minimum Python version
- Check `pyproject.toml` for `requires-python`.
- Features like `match/case` (3.10+), `|` union types (3.10+), `TypeAlias` (3.10+) may not be available.
- Use `from __future__ import annotations` for postponed evaluation of annotations (3.7+).
- `dataclasses` require Python 3.7+.

Comprehensive feature availability table for the SDK (targeting `>=3.9`):

| Feature | Min Version | Alternative for 3.9 |
|---------|------------|---------------------|
| `X \| Y` union syntax in annotations | 3.10 | `Union[X, Y]` or `from __future__ import annotations` |
| `match/case` | 3.10 | `if/elif` chains |
| `TypeAlias` | 3.10 | Plain assignment with comment |
| `tomllib` | 3.11 | `tomli` (backport, already in deps) |
| `asyncio.TaskGroup` | 3.11 | `asyncio.gather()` + manual tracking |
| `ExceptionGroup` / `except*` | 3.11 | Catch individual exceptions |
| `asyncio.Runner` | 3.11 | `asyncio.run()` or manual loop |
| `type` statement (PEP 695) | 3.12 | `TypeAlias` or plain assignment |
| `@override` decorator | 3.12 | `typing_extensions.override` |

Since the SDK targets `>=3.9`, specs MUST NOT use 3.10+ features without either:
1. A `from __future__ import annotations` import (for annotation-only syntax), or
2. A `typing_extensions` fallback, or
3. A `sys.version_info` guard.

**Priority:** P0  
**Reference:** The codebase already uses `tomli` fallback (`core/config.py` line 251–254). This pattern must be consistent.

---

## Build Rules

### PY-14: pyproject.toml is the standard
Modern Python projects use `pyproject.toml`, not `setup.py` or `setup.cfg`:
- Build backend: `hatchling`, `setuptools`, or `poetry-core`
- **WRONG:** Referencing `setup.py` configuration in specs
- **RIGHT:** All configuration in `pyproject.toml`

### PY-15: Verify gRPC generated code
Before referencing a gRPC method, verify it exists in the `_pb2.py` / `_pb2_grpc.py` files.
- Python gRPC uses `grpcio-tools` for code generation.
- Service stubs are `{ServiceName}Stub`.
- Do not assume methods exist because they'd be convenient.

### PY-16: Test framework
Python SDK tests should use `pytest` (not `unittest`):
- Async tests use `pytest-asyncio` with `@pytest.mark.asyncio`.
- Fixtures for client setup/teardown.
- `pytest-cov` for coverage reporting.

With `asyncio_mode = "auto"` (current `pyproject.toml` setting), `@pytest.mark.asyncio` is NOT needed — all async test functions run automatically:

```python
# WRONG (with auto mode) — redundant decorator
@pytest.mark.asyncio
async def test_send():
    ...

# RIGHT (with auto mode) — just define async test
async def test_send():
    ...

# Fixtures for async client lifecycle:
@pytest.fixture
async def pubsub_client():
    async with AsyncPubSubClient(address="localhost:50000") as client:
        yield client

# WRONG — fixture leaks resources
@pytest.fixture
async def pubsub_client():
    client = AsyncPubSubClient(address="localhost:50000")
    return client  # never closed!
```

**Priority:** P1  
**Reference:** pytest-asyncio 0.23+ docs. The KubeMQ `pyproject.toml` already sets `asyncio_mode = "auto"`.

---

## Pitfalls Discovered from Prior Runs

These rules were added based on concrete issues found during the Python SDK gap-close spec run (102 issues across 2 review rounds).

### PY-17: `asyncio.Semaphore._value` is private
Never access `asyncio.Semaphore._value` — it's a CPython implementation detail, not part of the public API.
- **WRONG:** `if sem._value > 0:` — breaks on non-CPython interpreters
- **RIGHT:** Use try-acquire pattern: `await asyncio.wait_for(sem.acquire(), timeout=0)` wrapped in `try/except asyncio.TimeoutError`

### PY-18: `logging.Logger` doesn't support kwargs
Python's stdlib `logging.Logger.info()`, `.debug()`, etc. use `%s` positional formatting, NOT keyword arguments.
- **WRONG:** `logger.info("connected", address=addr, client_id=cid)`
- **RIGHT:** `logger.info("connected address=%s client_id=%s", addr, cid)`
- For structured logging, use a `Logger` Protocol adapter that wraps stdlib logging with kwargs support.

### PY-19: `asyncio.get_event_loop()` is deprecated in 3.10+
In async code, use `asyncio.get_running_loop()` instead.
- **WRONG:** `loop = asyncio.get_event_loop()` — deprecated, may create a new loop unexpectedly
- **RIGHT:** `loop = asyncio.get_running_loop()` — raises `RuntimeError` if no loop is running (fail-fast)
- In sync code where you need to create a loop, use `asyncio.run()` at the top level only.

### PY-20: gRPC channel options must be verified
Not all gRPC channel option names exist. `grpc.tls_ciphers` is NOT a valid option.
- Always verify option names against the [gRPC Python docs](https://grpc.github.io/grpc/python/glossary.html#term-channel_arguments).
- Valid options are tuples: `(("grpc.keepalive_time_ms", 10000),)`
- **WRONG:** `options=[("grpc.tls_ciphers", "AES256")]` — invalid option name
- **RIGHT:** Verify against the gRPC channel arguments glossary first.

### PY-21: `CancelledError` is `BaseException` in Python 3.9+
`asyncio.CancelledError` inherits from `BaseException`, NOT `Exception`.
- `except Exception:` will NOT catch `CancelledError` — this is intentional.
- Do NOT catch `CancelledError` in retry loops — retrying a cancelled task is semantically wrong.
- The `finally` block is the correct place for cleanup after cancellation.
- **WRONG:** `except (Exception, asyncio.CancelledError):` in a retry loop
- **RIGHT:** Let `CancelledError` propagate; use `finally` for cleanup.

---

## SDK Design Rules

### PY-22: Dataclass `field(default_factory=...)` for mutable defaults [P0]
Python's mutable default argument trap applies to dataclasses, function parameters, and class attributes.

```python
# WRONG — mutable default shared across instances
@dataclass
class ClientConfig:
    metadata: dict[str, str] = {}  # ValueError from dataclasses!
    channels: list[str] = []       # ValueError from dataclasses!

# WRONG — function with mutable default
def send(self, msg: Message, metadata: dict[str, str] = {}) -> Result:
    metadata["timestamp"] = now()  # mutates shared default!
    ...

# RIGHT — use field(default_factory=...)
@dataclass
class ClientConfig:
    metadata: dict[str, str] = field(default_factory=dict)
    channels: list[str] = field(default_factory=list)

# RIGHT — use None sentinel for function params
def send(self, msg: Message, metadata: dict[str, str] | None = None) -> Result:
    metadata = metadata or {}
    metadata["timestamp"] = now()
    ...
```

Note: `@dataclass` will raise `ValueError` if you try `= {}` or `= []`, but regular classes and functions will not — making the function-parameter form an especially dangerous silent bug.

**Reference:** Python docs "Common Gotchas". The KubeMQ codebase correctly uses `field(default_factory=...)` in `core/config.py` — this rule ensures specs don't regress.

### PY-23: `__repr__` must mask secrets, `__str__` for user display [P0]
SDK types containing credentials or tokens must mask them in string representations.

```python
# WRONG — leaks auth_token in logs, debugger, error messages
@dataclass
class ClientConfig:
    address: str
    auth_token: str | None = None
    # default __repr__ shows: ClientConfig(address='host:50000', auth_token='secret123')

# RIGHT — mask secrets in __repr__
@dataclass
class ClientConfig:
    address: str
    auth_token: str | None = None

    def __repr__(self) -> str:
        return (
            f"ClientConfig(address={self.address!r}, "
            f"auth_token={'***' if self.auth_token else None!r})"
        )

# Convention for SDK types:
# __repr__ → unambiguous, for developers/debuggers (mask secrets)
# __str__  → user-friendly, for display/logging (mask secrets)
# If no __str__, Python falls back to __repr__
```

**Reference:** The KubeMQ codebase already does this in `core/config.py` line 143–152. Azure SDK masks credentials in all repr output. redis-py masks passwords in connection URLs.

### PY-24: `typing.Protocol` for callback signatures [P1]
Use `Protocol` for callback type definitions instead of `Callable` — it provides better documentation, IDE support, and allows keyword arguments.

```python
from typing import Protocol, runtime_checkable

# WRONG — Callable doesn't document parameter names or allow kwargs
from typing import Callable
on_message: Callable[[str, bytes], None]  # what are the args?

# RIGHT — Protocol with clear method signature
@runtime_checkable
class MessageHandler(Protocol):
    def __call__(self, channel: str, body: bytes, metadata: dict[str, str]) -> None: ...

class AsyncMessageHandler(Protocol):
    async def __call__(self, channel: str, body: bytes, metadata: dict[str, str]) -> None: ...

# Usage in subscription APIs:
async def subscribe(
    self,
    channel: str,
    handler: MessageHandler | AsyncMessageHandler,
) -> None: ...
```

**Reference:** nats-py uses callback protocols for message handlers. Azure SDK defines `Protocol` types for credential interfaces.

### PY-25: `@dataclass(frozen=True, slots=True)` for immutable value types [P1]
Message types, results, and config objects that should not be modified after creation.

```python
# WRONG — mutable message allows accidental modification
@dataclass
class EventMessage:
    channel: str
    body: bytes
    metadata: dict[str, str] = field(default_factory=dict)

msg = EventMessage(channel="ch", body=b"hi")
msg.channel = "other"  # silent mutation, potential bug

# RIGHT — frozen for value types that should be immutable
@dataclass(frozen=True)
class EventMessage:
    channel: str
    body: bytes
    metadata: dict[str, str] = field(default_factory=dict)  # still creates new dict per instance

msg = EventMessage(channel="ch", body=b"hi")
msg.channel = "other"  # FrozenInstanceError!

# BETTER (3.10+) — frozen + slots for memory efficiency and speed
@dataclass(frozen=True, slots=True)
class EventSendResult:
    id: str
    sent: bool
    error: str | None = None
```

When to use frozen: messages, results, configs, subscription descriptors.
When NOT to use frozen: client classes, builders, internal state objects.

`slots=True` requires Python 3.10+. For 3.9 compatibility, use `frozen=True` alone or add `__slots__` manually.

**Reference:** The KubeMQ codebase uses `frozen=True` for `TLSConfig` and `KeepAliveConfig`. redis-py uses frozen dataclasses for configuration objects.

### PY-26: Structured logging with `Logger` protocol adapter [P1]
Expands on PY-18 with a concrete adapter pattern.

```python
import logging
from typing import Any

class SDKLogger:
    """Logger adapter that supports kwargs-style structured logging
    on top of stdlib logging.
    """
    def __init__(self, name: str) -> None:
        self._logger = logging.getLogger(name)

    def info(self, msg: str, **kwargs: Any) -> None:
        if kwargs:
            extra_str = " ".join(f"{k}={v}" for k, v in kwargs.items())
            self._logger.info("%s %s", msg, extra_str)
        else:
            self._logger.info(msg)

    def error(self, msg: str, **kwargs: Any) -> None:
        exc = kwargs.pop("exc_info", None)
        if kwargs:
            extra_str = " ".join(f"{k}={v}" for k, v in kwargs.items())
            self._logger.error("%s %s", msg, extra_str, exc_info=exc)
        else:
            self._logger.error(msg, exc_info=exc)

    # ... debug, warning, etc.

# Usage:
logger = SDKLogger("kubemq.pubsub")
logger.info("message sent", channel="events", msg_id="abc123")
# Output: message sent channel=events msg_id=abc123

# WRONG — don't force structlog/loguru as hard dependencies
# RIGHT — use stdlib logging, let users configure their own handlers
```

Naming convention for loggers: use the module's `__name__` so users can configure logging per-module (e.g., `logging.getLogger("kubemq.transport")`).

**Reference:** Azure SDK uses a similar `ClientLogger` wrapper. nats-py uses stdlib `logging.getLogger("nats")`.

### PY-27: `importlib.metadata.version()` for version detection [P1]
The current SDK hardcodes `__version__ = "4.0.0"` in `__init__.py`. This can drift from `pyproject.toml`.

```python
# WRONG — version hardcoded in code, drifts from pyproject.toml
__version__ = "4.0.0"

# RIGHT — single source of truth from package metadata
from importlib.metadata import version, PackageNotFoundError

try:
    __version__ = version("kubemq")
except PackageNotFoundError:
    __version__ = "0.0.0-dev"  # fallback for editable installs
```

This uses `importlib.metadata` (stdlib since 3.8) and reads the version from the installed package metadata, which always matches `pyproject.toml`.

**Reference:** All modern Python packages (hatch, flit, setuptools-scm) recommend this pattern. The Azure SDK uses it across all packages.

### PY-28: `__all__` management in `__init__.py` [P0]
The `__all__` list defines the public API and controls `from kubemq import *`.

Rules for specs:
1. Every public type MUST appear in `__all__`.
2. Every entry in `__all__` MUST have a corresponding import statement.
3. Internal modules (`_internal/`) MUST NOT appear in `__all__`.
4. `__all__` entries must be sorted by category (see existing `__init__.py`).
5. When adding a new public type, add it to BOTH the import AND `__all__`.

```python
# WRONG — import exists but not in __all__
from kubemq.core.health import HealthReport
# __all__ = [...] — HealthReport missing!

# WRONG — in __all__ but no import
__all__ = ["HealthReport"]
# NameError when user does `from kubemq import HealthReport`

# RIGHT — both import and __all__ entry
from kubemq.core.health import HealthReport
__all__ = ["HealthReport"]
```

The `ruff` rule `F401` is suppressed for `__init__.py` files (see `pyproject.toml`), so these re-exports won't trigger "unused import" warnings.

**Reference:** Python packaging guide. The KubeMQ `__init__.py` already follows this pattern — this rule ensures specs maintain it.

### PY-29: `typing.overload` for method variants [P2]
When a method accepts different argument combinations, use `@overload` for type-checker clarity.

```python
from typing import overload

# WRONG — single signature with Optional, unclear which combinations are valid
def subscribe(
    self,
    channel: str,
    handler: MessageHandler | None = None,
    group: str | None = None,
) -> Subscription | AsyncIterator[Message]: ...

# RIGHT — overloaded signatures show valid combinations
@overload
def subscribe(self, channel: str, handler: MessageHandler) -> Subscription: ...
@overload
def subscribe(self, channel: str) -> AsyncIterator[Message]: ...

def subscribe(self, channel: str, handler: MessageHandler | None = None):
    if handler is not None:
        return self._subscribe_with_handler(channel, handler)
    return self._subscribe_iterator(channel)
```

Use `@overload` sparingly — only when the return type or behavior genuinely differs based on arguments. Don't overload just because a parameter is optional.

**Reference:** grpc-stubs uses extensive `@overload` for channel creation methods. redis-py uses `@overload` for `get()` variants.

---

## K8s Ecosystem SDK Patterns

### PY-30: Error hierarchy follows redis-py / Azure SDK pattern [P1]
Error handling pattern used by major K8s ecosystem SDKs.

| SDK | Base Exception | Recoverable vs Fatal | Exception Chaining |
|-----|---------------|---------------------|-------------------|
| redis-py | `RedisError` | `ConnectionError` (retry), `ResponseError` (don't) | Yes |
| nats-py | `NatsError` | Connection errors auto-retry | `from e` |
| kubernetes-client | `ApiException` | HTTP status-based | Partial |
| Azure SDK | `AzureError` → `ClientAuthenticationError`, `HttpResponseError` | Status-based | Yes |
| confluent-kafka | `KafkaError` | Error code enum | Via `error_cb` callback |

Pattern to adopt: exceptions should declare whether they are **retryable**:

```python
class KubeMQError(Exception):
    """Base exception for KubeMQ SDK."""
    retryable: bool = False  # subclasses override

class KubeMQConnectionError(KubeMQError):
    """Connection-related error — generally retryable."""
    retryable: bool = True

class KubeMQTimeoutError(KubeMQError):
    """Operation timed out — generally retryable."""
    retryable: bool = True

class KubeMQValidationError(KubeMQError):
    """Input validation failed — NOT retryable."""
    retryable: bool = False
```

This allows retry logic to check `except KubeMQError as e: if e.retryable: retry()`.

**Reference:** redis-py distinguishes recoverable vs non-recoverable. Azure SDK `RetryPolicy` checks error types. nats-py auto-retries connection errors.

### PY-31: Connection lifecycle follows nats-py pattern [P0]
Connection management patterns from messaging SDKs:

```python
# nats-py pattern — explicit connect with reconnection callbacks
nc = NATS()
await nc.connect(
    servers=["nats://localhost:4222"],
    reconnect_time_wait=2,        # seconds between reconnect attempts
    max_reconnect_attempts=60,    # -1 for infinite
    disconnected_cb=on_disconnect,
    reconnected_cb=on_reconnect,
    error_cb=on_error,
)

# KubeMQ should follow a similar pattern:
client = AsyncPubSubClient(
    address="localhost:50000",
    auto_reconnect=True,
    reconnect_interval_seconds=2,
    on_disconnect=my_disconnect_handler,   # optional callback
    on_reconnect=my_reconnect_handler,     # optional callback
)
async with client:
    await client.send_event(msg)
```

Key patterns across SDKs:
1. **nats-py:** Async-only, auto-reconnect with callbacks, subscription restoration
2. **redis-py:** Sync+async, connection pool, `Redis.from_pool()` for lifecycle control
3. **kubernetes-client:** Sync-only, config loading from kubeconfig/env
4. **confluent-kafka:** C-based, callback-driven, `poll()` loop pattern

Specs must:
- Always show the context manager usage as primary
- Document what happens to in-flight operations during reconnection
- Document whether subscriptions auto-restore after reconnect

**Reference:** nats-py reconnection docs, redis-py `Redis.from_pool()`.

### PY-32: Async-first with sync wrapper (Azure SDK pattern) [P0]
Documents the architectural decision for sync/async coexistence.

```
# Azure SDK layout — async in `aio/` subpackage
azure/
  servicebus/
    _servicebus_client.py          # sync implementation
    aio/
      _servicebus_client_async.py  # async implementation

# KubeMQ layout — already follows a similar pattern
kubemq/
  pubsub/
    client.py        # sync client
    async_client.py  # async client

# Key rules:
# 1. Async is the primary implementation
# 2. Sync wraps async (or is a separate thin implementation)
# 3. NEVER mix sync and async gRPC in the same process (deadlock risk)
# 4. Each client class is self-contained — no shared mutable state between sync/async
```

**Sync wrapper gotcha** — `grpc.aio` and `grpc` (sync) have separate channel types:

```python
# WRONG — using grpc.aio.Channel in sync code
channel = grpc.aio.insecure_channel(address)  # async channel
stub = kubemq_pb2_grpc.kubemqStub(channel)
stub.SendEvent(request)  # deadlock or error

# RIGHT — sync code uses grpc.insecure_channel
channel = grpc.insecure_channel(address)
stub = kubemq_pb2_grpc.kubemqStub(channel)
response = stub.SendEvent(request)

# RIGHT — async code uses grpc.aio.insecure_channel
channel = grpc.aio.insecure_channel(address)
stub = kubemq_pb2_grpc.kubemqStub(channel)
response = await stub.SendEvent(request)
```

**Reference:** Azure SDK design guidelines. gRPC Python docs explicitly warn: "AsyncIO objects must only be used on the thread where they were created." The KubeMQ codebase already separates sync/async — this rule prevents regression.

### PY-33: OTel integration follows `opentelemetry-instrumentation-grpc` [P1]
The standard OTel integration pattern for gRPC Python:

```python
# WRONG — manually creating spans for every gRPC call
tracer = trace.get_tracer("kubemq")
with tracer.start_as_current_span("send_event"):
    response = await stub.SendEvent(request)

# RIGHT — use gRPC instrumentor (automatic span creation)
from opentelemetry.instrumentation.grpc import GrpcAioInstrumentorClient

def _setup_otel(self) -> None:
    """Set up OpenTelemetry instrumentation if available."""
    try:
        from opentelemetry.instrumentation.grpc import GrpcAioInstrumentorClient
        GrpcAioInstrumentorClient().instrument()
    except ImportError:
        pass  # OTel not installed, skip

# For custom spans beyond gRPC calls (e.g., retry logic, queue polling):
def _get_tracer(self):
    try:
        from opentelemetry import trace
        return trace.get_tracer("kubemq", __version__)
    except ImportError:
        return None

# Span naming convention: "kubemq.<operation>" (e.g., "kubemq.send_event")
```

The `GrpcAioInstrumentorClient` automatically:
- Creates spans for all gRPC calls
- Propagates trace context via gRPC metadata
- Sets span status from gRPC status codes
- Works with both unary and streaming calls

**Reference:** `opentelemetry-instrumentation-grpc` docs. Azure SDK uses `DistributedTracingPolicy` in its pipeline.

---

## Enterprise Python Patterns

### PY-34: Minimum Python version policy [P0]
The SDK currently targets `>=3.9`. This has implications:

1. **Python 3.9 reaches EOL October 2025** — already unsupported.
2. **Python 3.10 reaches EOL October 2026** — 7 months from now.
3. For a v4 SDK shipping in 2026, consider `>=3.11` as minimum to unlock:
   - `tomllib` (no backport needed)
   - `asyncio.TaskGroup`
   - `ExceptionGroup` / `except*`
   - Better `asyncio` performance
   - `typing.Self` (for builder patterns)

If keeping `>=3.9`:
- Every spec using 3.10+ features must include a fallback
- Test matrix must include 3.9, 3.10, 3.11, 3.12, 3.13
- CI must verify the oldest supported version works

```toml
# Recommended for v4 launch:
requires-python = ">=3.11"

# If backward compat is critical:
requires-python = ">=3.9"
# But then EVERY feature must be gated:
# typing_extensions for Self, override
# tomli for TOML
# manual TaskGroup fallback
```

**Reference:** NEP 29 (NumPy version support policy) — drop Python versions 42 months after release. kubernetes-client/python supports 3.8+. nats-py supports 3.8+. redis-py supports 3.8+.

### PY-35: `ruff` as unified linter/formatter [P1]
The `pyproject.toml` already configures ruff. Specs must be aware of which rules are enforced:

Key enabled ruff rules and their impact on generated code:
- `UP` (pyupgrade): Will flag `Optional[X]` → suggest `X | None` (but only safe with `from __future__ import annotations` on 3.9)
- `B` (bugbear): Flags mutable default args (`B006`), bare `except` (`B001`), `setattr` with constant (`B010`)
- `SIM` (simplify): Flags `if x == True` → `if x`, unnecessary `else` after `return`
- `C4` (comprehensions): Flags `list(x for x in y)` → `[x for x in y]`
- `I` (isort): Enforces import sorting order

```python
# ruff B006 — mutable default argument (but B008 is IGNORED in pyproject.toml)
# WRONG (caught by ruff):
def send(self, msg, headers={}): ...

# RIGHT:
def send(self, msg, headers=None): ...

# ruff SIM105 — use contextlib.suppress
# WRONG (caught by ruff, but SUPPRESSED in tests):
try:
    os.remove(path)
except FileNotFoundError:
    pass

# RIGHT:
from contextlib import suppress
with suppress(FileNotFoundError):
    os.remove(path)
```

**Reference:** The KubeMQ `pyproject.toml` already configures ruff. This rule ensures specs generate ruff-compliant code.

### PY-36: `mypy --strict` compatibility [P1]
While the current `mypy` config is not strict, specs should generate code compatible with strict mode:

```python
# mypy strict flags that matter for SDK code:
# disallow_untyped_defs — every function needs type annotations
# disallow_any_generics — dict, list, set need type params
# no_implicit_reexport — __init__.py re-exports need explicit __all__

# WRONG — fails mypy --strict
def send(self, msg):  # missing types
    result = {}       # dict without type params
    ...

# RIGHT — passes mypy --strict
def send(self, msg: Message) -> SendResult:
    result: dict[str, Any] = {}
    ...

# WRONG — implicit re-export (fails with no_implicit_reexport)
# In __init__.py:
from .client import Client  # not in __all__
# User does `from kubemq import Client` → mypy error

# RIGHT — explicit re-export
from .client import Client
__all__ = ["Client"]
# Or: from .client import Client as Client  (explicit re-export via alias)
```

**Reference:** Azure SDK enforces mypy strict across all packages. The explicit re-export pattern (`as Client`) is documented in mypy docs.

### PY-37: `py.typed` marker file [P1]
For type checkers to recognize the package as typed, a `py.typed` marker file must exist.

```
# File: src/kubemq/py.typed
# (empty file — its mere existence signals the package ships type information)
```

The `pyproject.toml` already has `"Typing :: Typed"` classifier. Both must be present:
1. `py.typed` file in the package directory (for type checkers)
2. `"Typing :: Typed"` classifier in `pyproject.toml` (for PyPI)

**Reference:** PEP 561. All typed packages on PyPI should ship this. The `grpcio` package itself lacks `py.typed` (see grpc/grpc#29041), which is a known gap.

### PY-38: Documentation with `mkdocs` + `mkdocstrings` [P1]
The `pyproject.toml` already has docs extras. Specs must write Google-style docstrings compatible with `mkdocstrings`:

```python
# Google-style docstring (preferred for mkdocstrings)
async def send_event(
    self,
    message: EventMessage,
    timeout: float | None = None,
) -> EventSendResult:
    """Send an event message to a channel.

    Args:
        message: The event message to send.
        timeout: Optional timeout in seconds. Defaults to client default.

    Returns:
        The send result containing the message ID and status.

    Raises:
        KubeMQConnectionError: If the client is not connected.
        KubeMQValidationError: If the message is invalid.
        KubeMQTimeoutError: If the operation times out.

    Example:
        ```python
        result = await client.send_event(
            EventMessage(channel="events", body=b"hello")
        )
        print(f"Sent: {result.id}")
        ```
    """
```

Rules:
1. Every public method MUST have a docstring with Args, Returns, and Raises.
2. Use Google style (not NumPy or Sphinx style).
3. Include code examples for primary API methods.
4. Document all exceptions that can be raised.

**Reference:** Azure SDK docstring guidelines. Google Python style guide.

---

## Common Python SDK Pitfalls

### PY-39: `contextlib.suppress` vs bare `except` [P0]

```python
# WRONG — bare except catches KeyboardInterrupt, SystemExit, CancelledError
try:
    await channel.close()
except:
    pass

# WRONG — except Exception still catches too broadly
try:
    await channel.close()
except Exception:
    pass  # swallows KubeMQError, ValueError, etc.

# RIGHT — catch specific exceptions
try:
    await channel.close()
except grpc.RpcError:
    pass  # channel already closed, safe to ignore

# RIGHT — contextlib.suppress for clean ignore
from contextlib import suppress
with suppress(grpc.RpcError):
    await channel.close()
```

**Reference:** ruff rule `SIM105` enforces `contextlib.suppress`. The KubeMQ `pyproject.toml` suppresses this in tests but not in source.

### PY-40: `weakref.finalize()` for callback cleanup [P2]
When clients register callbacks (message handlers, error handlers), strong references prevent garbage collection.

```python
import weakref

# WRONG — strong reference to handler prevents GC of the handler's owner
class Subscriber:
    def __init__(self):
        self._handlers: list[Callable] = []

    def on_message(self, handler: Callable) -> None:
        self._handlers.append(handler)  # strong ref, potential leak

# RIGHT — weak references for user-provided callbacks
class Subscriber:
    def __init__(self):
        self._handlers: list[weakref.ref] = []

    def on_message(self, handler: Callable) -> None:
        ref = weakref.ref(handler, self._remove_handler)
        self._handlers.append(ref)

    def _remove_handler(self, ref: weakref.ref) -> None:
        self._handlers.remove(ref)

# BETTER — use weakref.finalize for resource cleanup
class Client:
    def __init__(self):
        self._channel = grpc.aio.insecure_channel(...)
        # ensure channel closes even if client is GC'd without close()
        self._finalizer = weakref.finalize(self, self._cleanup, self._channel)

    @staticmethod
    def _cleanup(channel):
        # Note: static method — does not prevent GC of Client
        channel.close()
```

**Caveat:** `weakref` doesn't work with bound methods (`obj.method`) — the bound method is immediately GC'd. Use `weakref.WeakMethod` for that case, or store the object itself.

**Reference:** Python `weakref` docs. The nats-py client uses `weakref` for internal subscription tracking.

### PY-41: `asyncio.shield()` for critical cleanup [P1]
Protect critical operations (ack, transaction commit) from cancellation.

```python
# WRONG — ack can be cancelled mid-flight, leaving message in limbo
async def process_message(self, msg: QueueMessageReceived) -> None:
    await self.handle(msg)
    await msg.ack()  # if task is cancelled here, message is neither acked nor nacked

# RIGHT — shield the ack from cancellation
async def process_message(self, msg: QueueMessageReceived) -> None:
    await self.handle(msg)
    try:
        await asyncio.shield(msg.ack())
    except asyncio.CancelledError:
        # shield was cancelled but ack continues in background
        raise  # re-raise so caller knows we were cancelled

# IMPORTANT: asyncio.shield() does NOT prevent the *awaiter* from seeing CancelledError.
# It only prevents the *inner coroutine* from being cancelled.
# The pattern above re-raises CancelledError — this is correct behavior.

# For cleanup in __aexit__:
async def __aexit__(self, *args) -> None:
    # Shield close operations from cancellation during __aexit__
    await asyncio.shield(self._drain_and_close())
```

**Reference:** Python asyncio docs on shield. nats-py PR #675 uses shield for connection health checks.

### PY-42: `atexit` and signal handlers for sync cleanup [P1]
Sync clients need proper cleanup when the process exits.

```python
import atexit
import signal

class SyncClient:
    def __init__(self, address: str):
        self._channel = grpc.insecure_channel(address)
        # Register cleanup for normal exit
        self._atexit_registered = False
        atexit.register(self._cleanup)
        self._atexit_registered = True

    def _cleanup(self) -> None:
        """Cleanup called at interpreter shutdown."""
        if self._channel:
            self._channel.close()

    def close(self) -> None:
        """Explicit close — also unregisters atexit handler."""
        self._cleanup()
        if self._atexit_registered:
            atexit.unregister(self._cleanup)
            self._atexit_registered = False

# WRONG — signal.signal() in library code
# Libraries should NOT install signal handlers — it overrides user's handlers
signal.signal(signal.SIGTERM, my_handler)  # BAD in library code!

# RIGHT — document that users should handle signals
# In SDK docs: "For graceful shutdown on SIGTERM, wrap client usage in a signal handler"
```

Rules:
1. `atexit.register()` is safe in library code.
2. `signal.signal()` is NOT safe in library code — it overrides user's handlers and only works on main thread.
3. Always unregister atexit handlers when `close()` is called explicitly.
4. `atexit` handlers run during normal interpreter shutdown, NOT on `SIGKILL`.
5. In async code, use `asyncio` shutdown hooks instead of `atexit`.

**Reference:** Python `atexit` docs. confluent-kafka-python uses `atexit` for producer flush.

### PY-43: `threading.daemon` threads and cleanup semantics [P1]
Relevant for sync clients that spawn background threads.

```python
import threading

# WRONG — non-daemon thread prevents process exit
def _start_keepalive(self):
    t = threading.Thread(target=self._keepalive_loop)
    t.start()  # process won't exit until this thread finishes

# RIGHT — daemon thread exits with main thread
def _start_keepalive(self):
    t = threading.Thread(target=self._keepalive_loop, daemon=True)
    t.start()  # exits when main thread exits

# CAVEAT: daemon threads are abruptly killed — no cleanup runs.
# For clean shutdown, use an Event:
def _start_keepalive(self):
    self._stop_event = threading.Event()
    t = threading.Thread(target=self._keepalive_loop, daemon=True)
    t.start()

def _keepalive_loop(self):
    while not self._stop_event.is_set():
        self._ping()
        self._stop_event.wait(timeout=30)  # interruptible sleep

def close(self):
    self._stop_event.set()  # signal thread to stop
```

**Reference:** Python threading docs. redis-py uses daemon threads for health checking.

### PY-44: Iterator protocol vs async generator for subscription streams [P1]
Subscription APIs can return async generators or implement `__aiter__`/`__anext__`. Both have tradeoffs.

```python
from typing import AsyncIterator

# Pattern 1: Async generator (simpler, preferred for subscriptions)
async def subscribe(self, channel: str) -> AsyncIterator[EventMessage]:
    """Subscribe to events. Use `async for` to consume."""
    stream = self._stub.Subscribe(request)
    try:
        async for response in stream:
            yield _convert_to_event(response)
    finally:
        stream.cancel()

# Usage:
async for event in client.subscribe("my-channel"):
    process(event)

# Pattern 2: Async iterator class (more control, cancellation support)
class Subscription:
    def __init__(self, stream):
        self._stream = stream
        self._cancelled = False

    def __aiter__(self):
        return self

    async def __anext__(self) -> EventMessage:
        if self._cancelled:
            raise StopAsyncIteration
        try:
            response = await self._stream.__anext__()
            return _convert_to_event(response)
        except StopAsyncIteration:
            raise

    async def cancel(self) -> None:
        self._cancelled = True
        self._stream.cancel()

# IMPORTANT: Always use aclosing() when breaking out of async generators
from contextlib import aclosing

async with aclosing(client.subscribe("ch")) as stream:
    async for event in stream:
        if should_stop(event):
            break  # generator cleanup runs via aclosing
```

**Reference:** nats-py returns async iterators for subscriptions. gRPC Python `__aiter__` on call objects.

### PY-45: `copy.deepcopy` and `pickle` compatibility [P2]
SDK types should NOT be picklable by default (they contain non-serializable resources like gRPC channels).

```python
# WRONG — allowing pickle of client objects
import pickle
data = pickle.dumps(client)  # will fail or produce corrupt state

# RIGHT — explicitly prevent pickling of stateful objects
class Client:
    def __reduce__(self):
        raise TypeError(f"{self.__class__.__name__} objects cannot be pickled")

# For message/config dataclasses, pickling is OK and works automatically
# with @dataclass. frozen+slots dataclasses have optimized __getstate__/__setstate__.

# deepcopy should work for message types but NOT for client types:
import copy
msg_copy = copy.deepcopy(event_message)  # OK — pure data
client_copy = copy.deepcopy(client)       # BAD — duplicates channels, connections
```

Rule: Client/connection types should raise `TypeError` on `pickle`/`deepcopy`. Message and config types should support both.

**Reference:** redis-py connection objects are not picklable. gRPC channel objects raise errors on pickle.

### PY-46: `sys.excepthook` and `threading.excepthook` — background exception handling [P1]
Unhandled exceptions in background threads/tasks are silently lost by default.

```python
# WRONG — exception in daemon thread is silently lost
def _keepalive_loop(self):
    while True:
        self._ping()  # if this raises, thread dies silently

# RIGHT — catch and log exceptions in background threads
def _keepalive_loop(self):
    try:
        while not self._stop_event.is_set():
            self._ping()
            self._stop_event.wait(30)
    except Exception:
        self._logger.error("keepalive loop failed", exc_info=True)

# For async tasks, unhandled exceptions emit a warning by default.
# Set a custom exception handler on the loop:
def _handle_task_exception(self, loop, context):
    exception = context.get("exception")
    self._logger.error("Unhandled exception in async task", exc_info=exception)

loop = asyncio.get_running_loop()
loop.set_exception_handler(self._handle_task_exception)

# IMPORTANT: Libraries should NOT set sys.excepthook or threading.excepthook
# These are global hooks and would affect the user's application.
# Instead, catch exceptions at task/thread boundaries.
```

**Reference:** Python asyncio docs on exception handling. nats-py PR #675 adds explicit exception handling in ping/read loops to prevent silent failures.

---

## gRPC-Specific Python Patterns

### PY-47: `grpc.StatusCode` to KubeMQ exception mapping — complete list [P0]
The existing `from_grpc_error()` function maps status codes. Specs must use the full mapping and handle `AioRpcError` correctly:

```python
import grpc

# Complete status code mapping (specs must not invent unmapped codes)
STATUS_CODE_MAP = {
    grpc.StatusCode.OK:                  None,  # not an error
    grpc.StatusCode.CANCELLED:           KubeMQError,            # usually CancelledError
    grpc.StatusCode.UNKNOWN:             KubeMQError,
    grpc.StatusCode.INVALID_ARGUMENT:    KubeMQValidationError,
    grpc.StatusCode.DEADLINE_EXCEEDED:   KubeMQTimeoutError,
    grpc.StatusCode.NOT_FOUND:           KubeMQChannelError,
    grpc.StatusCode.ALREADY_EXISTS:      KubeMQChannelError,
    grpc.StatusCode.PERMISSION_DENIED:   KubeMQAuthenticationError,
    grpc.StatusCode.RESOURCE_EXHAUSTED:  KubeMQMessageError,
    grpc.StatusCode.FAILED_PRECONDITION: KubeMQError,
    grpc.StatusCode.ABORTED:             KubeMQTransactionError,
    grpc.StatusCode.OUT_OF_RANGE:        KubeMQValidationError,
    grpc.StatusCode.UNIMPLEMENTED:       KubeMQError,
    grpc.StatusCode.INTERNAL:            KubeMQError,
    grpc.StatusCode.UNAVAILABLE:         KubeMQConnectionError,
    grpc.StatusCode.DATA_LOSS:           KubeMQError,
    grpc.StatusCode.UNAUTHENTICATED:     KubeMQAuthenticationError,
}

# Async error handling:
# grpc.aio.AioRpcError inherits from grpc.RpcError
# Both have .code() and .details() methods
try:
    response = await stub.SendEvent(request)
except grpc.aio.AioRpcError as e:
    raise from_grpc_error(e) from e
# The `from e` is critical — see PY-4.
```

**Reference:** gRPC status codes reference. The existing `from_grpc_error()` in `core/exceptions.py` covers most codes but misses `FAILED_PRECONDITION`, `OUT_OF_RANGE`, `UNIMPLEMENTED`, and `DATA_LOSS`.

### PY-48: `grpc.aio.Channel` lifecycle — no re-entry [P0]
gRPC async channels cannot be used after close, and their context manager cannot be re-entered.

```python
# WRONG — reusing a closed channel
channel = grpc.aio.insecure_channel(address)
await channel.close()
stub = kubemq_pb2_grpc.kubemqStub(channel)  # undefined behavior!

# WRONG — re-entering a channel context manager
channel = grpc.aio.insecure_channel(address)
async with channel:
    ...
async with channel:  # ERROR — cannot re-enter
    ...

# RIGHT — create a new channel for reconnection
async def _reconnect(self):
    if self._channel:
        await self._channel.close()
    self._channel = grpc.aio.insecure_channel(self._address, options=self._options)
    self._stub = kubemq_pb2_grpc.kubemqStub(self._channel)

# Channel state checking:
state = self._channel.get_state(try_to_connect=False)
if state == grpc.ChannelConnectivity.SHUTDOWN:
    # Channel is permanently closed, must create new one
    await self._reconnect()
```

**Reference:** gRPC Python asyncio docs: "Channels should not be entered and exited multiple times."

### PY-49: gRPC keepalive options — correct names and coordination [P0]
Extends PY-20 with the complete set of valid keepalive options.

```python
# VALID keepalive channel options (exhaustive list):
channel_options = [
    ("grpc.keepalive_time_ms", 30000),          # period between pings (ms)
    ("grpc.keepalive_timeout_ms", 10000),        # wait for ack (ms)
    ("grpc.keepalive_permit_without_calls", 1),  # ping even without active RPCs
    ("grpc.http2.max_pings_without_data", 0),    # allow pings without data (0=unlimited)
    ("grpc.max_send_message_length", 100 * 1024 * 1024),   # 100MB
    ("grpc.max_receive_message_length", 100 * 1024 * 1024), # 100MB
]

# WRONG — these are NOT valid channel options:
# ("grpc.tls_ciphers", "...")      — does not exist (see PY-20)
# ("grpc.keepalive_interval", ...) — wrong name
# ("grpc.keep_alive_time", ...)    — wrong name (underscore vs no underscore)
# ("grpc.max_message_size", ...)   — wrong name (must specify send or receive)

# IMPORTANT: keepalive_time_ms MUST be set to enable keepalive.
# Setting only keepalive_timeout_ms does NOT enable keepalive pings.

# Server coordination:
# If server has grpc.http2.min_recv_ping_interval_without_data_ms = 300000 (5 min)
# and client sends pings every 30s, server will send GOAWAY with "too_many_pings".
# Client keepalive_time_ms must be >= server's min_recv_ping_interval.
```

**Reference:** gRPC Core keepalive guide. grpc/grpc#32095 discusses common keepalive misconfigurations.

### PY-50: gRPC interceptor implementation for async [P1]
Interceptors must use the correct async base classes.

```python
import grpc
import grpc.aio

# WRONG — using sync interceptor base class in async code
class MyInterceptor(grpc.UnaryUnaryClientInterceptor):  # sync!
    def intercept_unary_unary(self, continuation, client_call_details, request):
        ...

# RIGHT — using async interceptor base class
class MyInterceptor(grpc.aio.UnaryUnaryClientInterceptor):
    async def intercept_unary_unary(self, continuation, client_call_details, request):
        # Add metadata (e.g., auth token)
        new_metadata = list(client_call_details.metadata or [])
        new_metadata.append(("authorization", f"Bearer {self._token}"))

        new_details = grpc.aio.ClientCallDetails(
            method=client_call_details.method,
            timeout=client_call_details.timeout,
            metadata=new_metadata,
            credentials=client_call_details.credentials,
            wait_for_ready=client_call_details.wait_for_ready,
        )
        return await continuation(new_details, request)

# Channel creation with interceptors:
channel = grpc.aio.insecure_channel(
    address,
    interceptors=[MyInterceptor(token)],
    options=channel_options,
)

# IMPORTANT: Interceptors receive ALL calls on the channel.
# State sharing between interceptors should use contextvars (not class attributes).
```

Available async interceptor base classes:
- `grpc.aio.UnaryUnaryClientInterceptor`
- `grpc.aio.UnaryStreamClientInterceptor`
- `grpc.aio.StreamUnaryClientInterceptor`
- `grpc.aio.StreamStreamClientInterceptor`

**Reference:** gRPC Python aio interceptor docs. The KubeMQ codebase has `transport/interceptors.py`.

### PY-51: gRPC metadata handling — tuple lists, not dicts [P1]
gRPC metadata is a sequence of (key, value) tuples, NOT a dict.

```python
# WRONG — treating metadata as dict
metadata = {"authorization": "Bearer token123"}
response = await stub.SendEvent(request, metadata=metadata)

# RIGHT — metadata is a sequence of tuples
metadata = [
    ("authorization", "Bearer token123"),
    ("x-kubemq-client-id", client_id),
]
response = await stub.SendEvent(request, metadata=metadata)

# IMPORTANT: metadata keys can repeat (unlike dict)
metadata = [
    ("x-custom-header", "value1"),
    ("x-custom-header", "value2"),  # valid — both values sent
]

# Reading response metadata (also tuple lists):
# grpc.aio calls return call objects with metadata access
call = stub.SendEvent(request, metadata=metadata)
response = await call
initial_metadata = await call.initial_metadata()  # tuple list
trailing_metadata = await call.trailing_metadata()  # tuple list

# Binary metadata keys must end with '-bin'
metadata = [
    ("x-binary-data-bin", b"\x00\x01\x02"),  # binary value OK with -bin suffix
]
```

**Reference:** gRPC Python docs on metadata. The `-bin` suffix convention is a gRPC wire format requirement.

### PY-52: Protobuf message conversion — don't trust `__dict__` [P0]
Protobuf message fields have specific access patterns.

```python
import kubemq.grpc.kubemq_pb2 as pb2

# WRONG — accessing protobuf fields via __dict__
msg_dict = proto_message.__dict__  # includes internal protobuf state

# WRONG — using dict() or vars() on protobuf messages
d = dict(proto_message)  # doesn't work as expected

# RIGHT — use MessageToDict for conversion
from google.protobuf.json_format import MessageToDict, ParseDict
d = MessageToDict(proto_message, preserving_proto_field_name=True)

# RIGHT — access fields directly
channel = proto_message.Channel
body = proto_message.Body

# Serialization:
data = proto_message.SerializeToString()  # bytes
proto_message.ParseFromString(data)       # in-place parse

# IMPORTANT: protobuf field names in Python are the ORIGINAL proto names,
# not Python snake_case. If the proto has `ClientID`, Python uses `ClientID`.
# This is different from the grpc-generated stub methods which use snake_case.

# Check if a field is set (proto3 — scalar fields have default values):
proto_message.HasField("optional_field")  # only works for message/oneof fields
# For scalar fields, compare with default value:
if proto_message.ClientID != "":
    ...
```

**Reference:** protobuf Python docs. The KubeMQ proto uses PascalCase field names (e.g., `ClientID`, `Channel`, `Body`).

### PY-53: gRPC streaming — `async for` and error handling [P0]
Streaming RPCs have specific patterns in Python gRPC.

```python
# Server streaming (subscribe pattern):
async def subscribe(self, request):
    call = self._stub.SubscribeToEvents(request)
    try:
        async for response in call:
            yield self._convert_response(response)
    except grpc.aio.AioRpcError as e:
        if e.code() == grpc.StatusCode.CANCELLED:
            return  # normal cancellation
        raise from_grpc_error(e) from e
    finally:
        call.cancel()  # ensure stream is cancelled

# Client streaming (batch send pattern):
async def send_batch(self, messages):
    async def request_iterator():
        for msg in messages:
            yield self._convert_to_proto(msg)

    response = await self._stub.SendEventsStream(request_iterator())
    return self._convert_result(response)

# Bidirectional streaming:
async def stream(self):
    async def outgoing():
        while True:
            msg = await self._outgoing_queue.get()
            yield self._convert_to_proto(msg)

    call = self._stub.BidirectionalStream(outgoing())

    async for response in call:
        await self._handle_response(response)

# IMPORTANT: Calling cancel() on a stream that's already completed is safe (no-op).
# IMPORTANT: async for on a cancelled stream raises AioRpcError with CANCELLED status.
```

**Reference:** gRPC Python asyncio docs. The KubeMQ codebase uses streaming in `queues/upstream_sender.py` and `queues/downstream_receiver.py`.

---

## Rules Added from Implementation Retrospective (2026-03-11)

### PY-54: Eager Initialization of Shared Locks/Semaphores

Lazy-initialized `threading.Semaphore`, `threading.Lock`, `asyncio.Semaphore`, or `asyncio.Lock` objects MUST be eagerly constructed in `__init__` or guarded by a dedicated `threading.Lock` to avoid race conditions when multiple threads/tasks access the accessor concurrently.

### PY-55: Buffer Drain Data Fate

Buffer drain operations (`drain_all()`, `flush()`) MUST either replay buffered items to their destination, invoke a notification/callback with the drained data, or explicitly document that data is discarded. Never silently discard buffered data on the success path.

### PY-56: Zero-Capacity Buffer Guard

When `max_bytes=0` or `max_size=0` is a valid configuration for a bounded buffer/queue, every `put()`/`enqueue()` method MUST raise an appropriate error immediately rather than blocking forever. This applies to all overflow modes including `"block"`.

### PY-57: Exception Handler Logging Must Preserve Tracebacks

All `logger.error()` or `logger.warning()` calls inside `except` blocks MUST include `exc_info=True` to preserve the full traceback for debugging. Alternatively, use `logger.exception()` which includes `exc_info=True` by default.
