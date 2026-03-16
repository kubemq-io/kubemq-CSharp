# JS/TS Language Constraints for Spec Agents

**Purpose:** Language-specific pitfalls that spec agents MUST follow to prevent common errors in JavaScript/TypeScript SDK specs.

---

## Syntax Rules

### JS-1: CommonJS vs ESM module format

The SDK must support both module systems or clearly pick one:
- **CommonJS:** `const { Client } = require('@kubemq/sdk');` — Node.js default before v20
- **ESM:** `import { Client } from '@kubemq/sdk';` — modern standard
- Set `"type": "module"` in `package.json` — omitting it entirely is ambiguous for consumers.
- Use `package.json` `"exports"` field to support both, with per-condition `"types"` nesting:
```json
{
  "name": "@kubemq/sdk",
  "type": "module",
  "main": "./dist/cjs/index.cjs",
  "module": "./dist/esm/index.js",
  "types": "./dist/types/index.d.ts",
  "exports": {
    ".": {
      "import": {
        "types": "./dist/types/index.d.ts",
        "default": "./dist/esm/index.js"
      },
      "require": {
        "types": "./dist/types/index.d.cts",
        "default": "./dist/cjs/index.cjs"
      }
    }
  },
  "files": ["dist"],
  "sideEffects": false,
  "engines": { "node": ">=20.0.0" }
}
```
- Specs must state which module format(s) are supported.
- **WRONG:** Only providing CommonJS when the ecosystem is moving to ESM.
- **WRONG:** Omitting `"type"` field entirely — ambiguous for consumers.
- **WRONG:** Only providing `"types"` at the top level — consumers using conditional exports won't find type declarations.
- **RIGHT:** Nest `"types"` inside each condition (`"import"` / `"require"`) so TypeScript resolves declarations correctly with `moduleResolution: "bundler"` or `"node16"`.
- Use **tsup** (recommended) or **unbuild** as the build tool for dual-format output with zero config.
- **K8s ecosystem reference:** AWS SDK v3 uses conditional `exports` with per-condition `"types"`.

### JS-2: Promise-based async API

All async operations MUST return `Promise<T>`, usable with `async/await`:
- **WRONG:** Callback-style `send(msg, (err, result) => { ... })`
- **RIGHT:** `async send(msg: Message): Promise<Result>`
- Event-based patterns (subscriptions) use EventEmitter or async iterators, not callbacks.

### JS-3: TypeScript strict mode

All TypeScript code in specs must be valid under `"strict": true`:
- No implicit `any` types
- Strict null checks enabled (`string | null` vs `string`)
- No unused parameters (prefix with `_`)
- **WRONG:** `function send(msg) { ... }` — implicit any
- **RIGHT:** `function send(msg: Message): Promise<Result> { ... }`

### JS-4: Error handling with typed errors

JavaScript/TypeScript errors extend `Error`. Use the ES2022 `Error(message, { cause })` pattern and `Symbol.hasInstance` for cross-version `instanceof` safety:

```typescript
const KUBEMQ_ERROR_SYMBOL = Symbol.for('kubemq.error');

export class KubeMQError extends Error {
  static [Symbol.hasInstance](instance: unknown): instance is KubeMQError {
    return (
      typeof instance === 'object' &&
      instance !== null &&
      (instance as Record<symbol, unknown>)[KUBEMQ_ERROR_SYMBOL] === true
    );
  }

  readonly code: string;
  readonly requestId?: string;

  constructor(message: string, code: string, options?: { cause?: Error; requestId?: string }) {
    super(message, { cause: options?.cause }); // ES2022 cause
    this.name = 'KubeMQError';
    this.code = code;
    this.requestId = options?.requestId;
    Object.setPrototypeOf(this, new.target.prototype);
    Object.defineProperty(this, KUBEMQ_ERROR_SYMBOL, { value: true });
  }
}
```

- Always set `this.name` in custom errors.
- Always fix prototype chain with `Object.setPrototypeOf` (required for TypeScript class inheritance of Error).
- Use ES2022 `Error(message, { cause })` options-bag pattern for error chaining.
- **WRONG:** `new KubeMQError("fail", "TIMEOUT", undefined, originalError)` — positional `cause` arg.
- **RIGHT:** `new KubeMQError("fail", "TIMEOUT", { cause: originalError })` — options bag with `cause`.
- **WRONG:** Relying on `instanceof KubeMQError` without `Symbol.hasInstance` — breaks when multiple package versions are installed.
- Use `Symbol.for('kubemq.error')` with `Symbol.hasInstance` for cross-version `instanceof` safety.
- **K8s ecosystem reference:** Temporal SDK and GraphQL-js both use `Symbol.hasInstance` for cross-version safety. `@kubernetes/client-node` re-exports error classes to ensure single-version `instanceof`.

### JS-5: Interface vs type alias

Use `interface` for object shapes that can be extended, `type` for unions and primitives:
- **Interface:** `interface ClientConfig { address: string; ... }` — extendable
- **Type:** `type ErrorCode = 'TIMEOUT' | 'AUTH_FAILED' | 'NOT_FOUND';` — union
- **WRONG:** Using `type` for everything or `interface` for everything.

---

## Naming Rules

### JS-6: Standard library / ecosystem name collisions

These names are commonly used in Node.js/browser and should be avoided:

| Name | Source | Risk |
|------|--------|------|
| `Error` | global | High — base error type |
| `TypeError` | global | High — common error |
| `Buffer` | Node.js global | High — binary data |
| `Channel` | `worker_threads` / BroadcastChannel | Medium |
| `EventEmitter` | `events` | Medium |
| `Logger` | various logging libs | Medium |
| `Timer` | global (setTimeout return) | Low |
| `Response` | `fetch` API | Medium |
| `Request` | `fetch` API | Medium |

**Resolution:** Prefix with `KubeMQ` (e.g., `KubeMQError`) or use descriptive names.

### JS-7: Package and file naming

- Package name: `@kubemq/sdk` (scoped npm package)
- File names: `kebab-case.ts` (e.g., `kubemq-client.ts`, `error-types.ts`)
- **WRONG:** `KubeMQClient.ts`, `error_types.ts`
- Export barrel: `src/index.ts` re-exports all public API
- Internal modules: `src/internal/` — not exported from barrel

### JS-8: Method and property naming

All methods and properties use `camelCase`:
- **WRONG:** `send_message`, `get_channel`, `is_connected`
- **RIGHT:** `sendMessage`, `getChannel`, `isConnected`
- Constants use `UPPER_SNAKE_CASE`: `DEFAULT_TIMEOUT`, `MAX_RETRIES`
- Enums use `PascalCase` for names, `PascalCase` or `UPPER_SNAKE_CASE` for values.

---

## Concurrency Rules

### JS-9: Single-threaded event loop awareness

JavaScript is single-threaded. There are no mutex/lock needs for typical code.
- No data races in single-threaded code — but async interleavings can still cause bugs.
- For CPU-intensive work, use `worker_threads` (Node.js) but document the constraint.
- **WRONG:** Using mutex/lock libraries in typical async Node.js code.
- **RIGHT:** Use async/await control flow for ordering; use `AbortController` for cancellation.

### JS-10: AbortController for cancellation

Use `AbortController` / `AbortSignal` instead of custom cancellation tokens:
```typescript
async function send(msg: Message, options?: { signal?: AbortSignal }): Promise<Result> {
  if (options?.signal?.aborted) throw new KubeMQError('Aborted');
  // Pass signal to downstream operations
}
```
- This is the standard cancellation pattern in modern JS/TS.
- gRPC-js supports `AbortSignal` via call options.

### JS-11: Resource cleanup with `Symbol.asyncDispose` and explicit `close()`

**Priority:** P1

TypeScript 5.2+ (shipped 2023) and Node.js 22+ support `Symbol.asyncDispose` natively. Specs should implement it now with a fallback for older runtimes.

```typescript
class Client implements AsyncDisposable {
  async close(): Promise<void> {
    // 1. Stop accepting new operations
    // 2. Wait for in-flight operations (with timeout)
    // 3. Close gRPC channel
    // 4. Clear timers (keepalive, reconnect)
  }

  // TypeScript 5.2+ / Node.js 22+
  async [Symbol.asyncDispose](): Promise<void> {
    await this.close();
  }
}

// Usage with explicit resource management
async function example() {
  await using client = new Client({ address: 'localhost:50000' });
  await client.send(message);
  // client.close() called automatically when scope exits
}

// Fallback for older runtimes
async function legacyExample() {
  const client = new Client({ address: 'localhost:50000' });
  try {
    await client.send(message);
  } finally {
    await client.close();
  }
}
```

- Always implement BOTH `close()` and `[Symbol.asyncDispose]()`.
- `close()` must be idempotent — calling it twice must not throw.
- Document the `await using` pattern in API docs while keeping `try/finally` as the primary example for broader compatibility.
- Document that users MUST call `close()` to prevent resource leaks.

### JS-12: EventEmitter memory leaks

If using EventEmitter for subscriptions:
- Set `maxListeners` if many concurrent subscriptions expected.
- Always provide `removeListener` / `off` cleanup guidance.
- Consider async iterators as an alternative: `for await (const msg of client.subscribe(channel)) { ... }`
- **WRONG:** Creating EventEmitter listeners without cleanup documentation.

---

## Dependency Rules

### JS-13: Optional dependencies with peer deps

For optional dependencies (OTel):
- Declare as `peerDependencies` with `"optional": true` in `peerDependenciesMeta`:
```json
{
  "peerDependencies": {
    "@opentelemetry/api": "^1.0.0"
  },
  "peerDependenciesMeta": {
    "@opentelemetry/api": { "optional": true }
  }
}
```
- Use dynamic import: `const otel = await import('@opentelemetry/api').catch(() => null);`
- **Never** make OTel a hard `dependency`.

### JS-14: Node.js version support

**Priority:** P0

**Minimum supported version: Node.js 20 LTS (Iron).**

- Node.js 18 reaches end-of-life in April 2025. Do not target it for new SDKs.
- Node.js 20 LTS: maintenance until April 2026. Minimum viable target.
- Node.js 22 LTS: maintenance until April 2027. Recommended for new features.
- Node.js 24 LTS: active from October 2025.

Feature availability by version:

| Feature | Node.js 20 | Node.js 22 | Node.js 24 |
|---------|-----------|-----------|-----------|
| `fetch` (global) | Yes | Yes | Yes |
| `AbortController` | Yes | Yes | Yes |
| `structuredClone` | Yes | Yes | Yes |
| `Symbol.dispose` | No* | Yes | Yes |
| `using` / `await using` | No* | Yes | Yes |
| `node:test` runner | Yes | Yes (stable) | Yes |

\* Requires TypeScript 5.2+ downlevel emit; runtime polyfill for `Symbol.dispose` needed.

```json
{ "engines": { "node": ">=20.0.0" } }
```

- Use feature detection or polyfills for newer APIs when targeting Node.js 20.
- **K8s ecosystem reference:** Azure SDK targets all LTS versions. AWS SDK v3 dropped Node.js 14 in late 2023, Node.js 16 in 2024.

---

## Build Rules

### JS-15: Tree-shaking support

The SDK must be tree-shakeable for bundler users:
- Use ESM exports (not CommonJS `module.exports`)
- Mark package as side-effect free: `"sideEffects": false` in `package.json`
- Avoid top-level side effects (e.g., global registrations at import time)
- **WRONG:** `import '@kubemq/sdk';` triggers side effects that register global handlers.

### JS-16: TypeScript declaration files

- Always ship `.d.ts` declaration files in the npm package.
- Set `"types"` or `"typings"` in `package.json`.
- Use `"declaration": true` and `"declarationMap": true` in `tsconfig.json`.
- Test declarations work by consuming them in a separate test project.

### JS-17: Verify gRPC generated code

Before referencing a gRPC method, verify it exists in the `.proto` file or generated code.
- JS gRPC uses `@grpc/grpc-js` and `@grpc/proto-loader` or `grpc-tools` for static generation.
- Do not assume methods exist because they'd be convenient.

### JS-18: Test framework

**Priority:** P1

**Recommended: Vitest** (3-5x faster than Jest, native ESM/TypeScript, Jest-compatible API).

- Use `vitest` for all new SDK projects — zero-config TypeScript and ESM support.
- Existing Jest projects: migration is optional but straightforward (`jest.mock` → `vi.mock`).
- `node:test` is acceptable for zero-dependency test suites (Node.js 20+).

```typescript
// vitest.config.ts
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globals: true,
    coverage: { provider: 'v8' },
    testTimeout: 30_000, // gRPC tests may be slow
  },
});
```

- Specs must NOT assume a specific test runner unless the project already uses one.
- Specs MUST use test doubles (stubs/mocks) for gRPC calls — never hit a real server in unit tests.
- **K8s ecosystem reference:** KafkaJS uses Jest; NATS.js uses Deno's built-in test runner; ioredis uses Jest; AWS SDK v3 uses Vitest.

---

## Error Handling

### JS-19: Error hierarchy with discriminated error codes

**Priority:** P0

Define a complete error hierarchy with string literal union error codes, not magic strings:

```typescript
// WRONG — magic strings, no autocomplete, no exhaustiveness checking
throw new KubeMQError('Connection failed', 'CONNECTION_ERROR');

// RIGHT — constrained error codes with discriminated subtypes
export const ErrorCode = {
  ConnectionFailed: 'CONNECTION_FAILED',
  AuthenticationFailed: 'AUTHENTICATION_FAILED',
  Timeout: 'TIMEOUT',
  InvalidArgument: 'INVALID_ARGUMENT',
  NotFound: 'NOT_FOUND',
  PermissionDenied: 'PERMISSION_DENIED',
  ResourceExhausted: 'RESOURCE_EXHAUSTED',
  Cancelled: 'CANCELLED',
  Internal: 'INTERNAL',
} as const;

export type ErrorCode = (typeof ErrorCode)[keyof typeof ErrorCode];

export class KubeMQError extends Error {
  readonly code: ErrorCode;
  // ... (see JS-4 for full implementation)
}

export class KubeMQConnectionError extends KubeMQError {
  readonly address: string;
  constructor(address: string, options?: { cause?: Error }) {
    super(`Failed to connect to ${address}`, ErrorCode.ConnectionFailed, options);
    this.name = 'KubeMQConnectionError';
    this.address = address;
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

export class KubeMQTimeoutError extends KubeMQError {
  readonly timeoutMs: number;
  constructor(operation: string, timeoutMs: number, options?: { cause?: Error }) {
    super(`${operation} timed out after ${timeoutMs}ms`, ErrorCode.Timeout, options);
    this.name = 'KubeMQTimeoutError';
    this.timeoutMs = timeoutMs;
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

export class KubeMQAuthError extends KubeMQError {
  constructor(message: string, options?: { cause?: Error }) {
    super(message, ErrorCode.AuthenticationFailed, options);
    this.name = 'KubeMQAuthError';
    Object.setPrototypeOf(this, new.target.prototype);
  }
}
```

- Every error subclass MUST call `Object.setPrototypeOf(this, new.target.prototype)`.
- Every error subclass MUST set `this.name` to the class name.
- Use `as const` + mapped type for error codes — provides autocomplete and exhaustiveness checking.
- **K8s ecosystem reference:** `@kubernetes/client-node` uses `ApiException` with numeric codes; Azure SDK uses `RestError` with string codes; AWS SDK v3 uses `ServiceException` with string `$fault` and `$metadata`.

### JS-20: Never swallow errors silently

**Priority:** P0

```typescript
// WRONG — swallows the error, caller has no idea what happened
async function send(msg: Message): Promise<void> {
  try {
    await this.grpcClient.send(msg);
  } catch (err) {
    console.error('send failed', err); // logged but not thrown
  }
}

// WRONG — catch-and-rethrow loses type information
async function send(msg: Message): Promise<void> {
  try {
    await this.grpcClient.send(msg);
  } catch (err) {
    throw new Error(String(err)); // loses cause, stack, type
  }
}

// RIGHT — wrap with cause chain, preserve original error
async function send(msg: Message): Promise<void> {
  try {
    await this.grpcClient.send(msg);
  } catch (err) {
    throw new KubeMQError(
      `Failed to send message to channel ${msg.channel}`,
      ErrorCode.Internal,
      { cause: err instanceof Error ? err : new Error(String(err)) }
    );
  }
}
```

- Errors from gRPC MUST be wrapped in `KubeMQError` subtypes with the `cause` property set.
- Never `catch` without re-throwing unless the method explicitly documents error suppression.
- The `cause` property MUST be an `Error` instance — wrap non-Error values with `new Error(String(value))`.

### JS-21: Use `unknown` in catch clauses, never `any`

**Priority:** P0

```typescript
// WRONG — catch variable typed as 'any' (TypeScript default before 'useUnknownInCatchVariables')
try {
  await client.send(msg);
} catch (err: any) {
  console.log(err.message); // No type safety
}

// RIGHT — catch variable typed as 'unknown', narrow before use
try {
  await client.send(msg);
} catch (err: unknown) {
  const message = err instanceof Error ? err.message : String(err);
  throw new KubeMQError(message, ErrorCode.Internal, {
    cause: err instanceof Error ? err : undefined,
  });
}
```

- Enable `"useUnknownInCatchVariables": true` in `tsconfig.json` (included in `strict` since TS 4.4).
- All specs MUST use `catch (err: unknown)` and narrow with `instanceof Error` before accessing `.message`, `.stack`, or `.cause`.

---

## Type System

### JS-22: Use discriminated unions for result types

**Priority:** P1

For operations that can return different shapes based on success/failure, use discriminated unions instead of optional fields:

```typescript
// WRONG — both fields optional, unclear semantics
interface SendResult {
  messageId?: string;
  error?: string;
  sentAt?: Date;
}

// RIGHT — discriminated union makes states explicit
type SendResult =
  | { success: true; messageId: string; sentAt: Date }
  | { success: false; error: KubeMQError };

function handleResult(result: SendResult): void {
  if (result.success) {
    // TypeScript knows: messageId and sentAt exist
    console.log(result.messageId);
  } else {
    // TypeScript knows: error exists
    console.error(result.error.code);
  }
}
```

- Use `assertNever` for exhaustiveness checking in switch statements over discriminant fields.
- The discriminant MUST be a literal type (`true`/`false`, string literal), not `boolean` or `string`.
- **K8s ecosystem reference:** Azure SDK uses discriminated unions for long-running operation states. AWS SDK v3 uses tagged output types.

### JS-23: Prefer `Readonly<T>` for configuration and message types

**Priority:** P1

```typescript
// WRONG — mutable config allows post-construction mutation
interface ClientConfig {
  address: string;
  clientId: string;
  authToken?: string;
}
const config: ClientConfig = { address: 'localhost:50000', clientId: 'test' };
config.address = 'hacked'; // No error

// RIGHT — frozen configuration
interface ClientConfig {
  readonly address: string;
  readonly clientId: string;
  readonly authToken?: string;
}

// Or use Readonly<T> at the usage site:
function createClient(config: Readonly<ClientConfig>): Client { ... }
```

- Configuration objects passed to the SDK MUST be treated as immutable.
- Internally, the SDK should `Object.freeze()` or spread-copy config objects to prevent mutation.
- Use `as const` for option literals: `const defaults = { maxRetries: 3, timeoutMs: 5000 } as const;`.
- Message bodies (`Uint8Array`) are inherently mutable — document that the SDK does not copy them.

### JS-24: Branded types for domain identifiers

**Priority:** P2

```typescript
// WRONG — plain strings allow accidental mixing
function subscribe(channel: string, group: string): void { ... }
subscribe(groupName, channelName); // swapped args, no compiler error!

// RIGHT — branded types prevent mixing
declare const __brand: unique symbol;
type Brand<T, TBrand extends string> = T & { readonly [__brand]: TBrand };

type ChannelName = Brand<string, 'ChannelName'>;
type GroupName = Brand<string, 'GroupName'>;

function channel(name: string): ChannelName { return name as ChannelName; }
function group(name: string): GroupName { return name as GroupName; }

function subscribe(channel: ChannelName, group: GroupName): void { ... }
subscribe(group('g1'), channel('ch1')); // Compile error! Types don't match
```

- Branded types are purely compile-time — zero runtime overhead.
- Use sparingly for identifiers that are commonly confused (channel names, client IDs, request IDs).
- Export factory functions (`channel()`, `group()`) for creating branded values.
- **K8s ecosystem reference:** Temporal SDK uses branded types for workflow/activity IDs.

### JS-25: `interface` for public API shapes, `type` for internal computations

**Priority:** P1

```typescript
// Interfaces: public API contracts — extendable, mergeable, better error messages
export interface ClientOptions {
  address: string;
  clientId?: string;
  authToken?: string;
  tls?: TlsConfig;
  reconnect?: ReconnectConfig;
}

export interface TlsConfig {
  certFile?: string;
  keyFile?: string;
  caFile?: string;
}

// Types: unions, intersections, mapped types, utility types
export type ConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'closed';
export type ErrorCode = (typeof ErrorCode)[keyof typeof ErrorCode];
type RequiredKeys<T> = { [K in keyof T]-?: undefined extends T[K] ? never : K }[keyof T];
```

- `interface` for anything users might extend or that appears in error messages (better display).
- `type` for unions, intersections, conditional types, mapped types, template literals.
- Never use `namespace` — use module-level exports.
- **K8s ecosystem reference:** Azure SDK uses `interface` for all options/config; AWS SDK v3 uses `interface` for input/output shapes.

---

## Async & Streaming

### JS-26: Subscription APIs must return `AsyncIterable<T>`

**Priority:** P0

```typescript
// WRONG — callback-based subscription
client.subscribe('channel', (msg) => {
  console.log(msg);
});

// WRONG — EventEmitter with no cleanup
client.on('message', (msg) => { ... });

// RIGHT — AsyncIterable with for-await-of
const subscription = client.subscribe('channel', { signal });

for await (const msg of subscription) {
  console.log(msg.body);
  if (shouldStop) break; // triggers cleanup
}

// RIGHT — AsyncIterable with AbortController for external cancellation
const controller = new AbortController();
const subscription = client.subscribe('channel', { signal: controller.signal });

setTimeout(() => controller.abort(), 60_000); // cancel after 1 minute

try {
  for await (const msg of subscription) {
    await processMessage(msg);
  }
} catch (err) {
  if (err instanceof Error && err.name === 'AbortError') {
    // Normal cancellation — not an error
  } else {
    throw err;
  }
}
```

- `subscribe()` MUST return `AsyncIterable<Message>` (or `AsyncGenerator<Message>`).
- Cancellation MUST be via `AbortSignal` — never via a custom `.cancel()` method on the iterable.
- Breaking out of `for await...of` MUST trigger resource cleanup (`.return()` on the iterator).
- The `AsyncIterable` MUST handle backpressure — if the consumer is slow, the iterator should buffer or drop messages based on configuration, not leak memory.
- **K8s ecosystem reference:** NATS.js uses `AsyncIterable` for subscriptions. `@kubernetes/client-node` informers use EventEmitter but the pattern is shifting to async iterators.

### JS-27: Never leave Promises unhandled

**Priority:** P0

```typescript
// WRONG — fire-and-forget Promise
client.send(message); // returned Promise ignored — rejection crashes process

// WRONG — .catch() swallows error silently
client.send(message).catch(() => {});

// RIGHT — always await or explicitly handle
await client.send(message);

// RIGHT — if intentionally fire-and-forget, document and handle
void client.send(message).catch((err) => {
  this.emit('error', err); // propagate to error handler
});
```

- All async methods MUST be awaited or the returned Promise must be stored and handled.
- Use `void` prefix to indicate intentional fire-and-forget (for `@typescript-eslint/no-floating-promises`).
- The SDK should register a global `process.on('unhandledRejection')` handler in examples/docs to catch unhandled rejections during development.
- **K8s ecosystem reference:** ioredis emits `'error'` events for background reconnection failures. KafkaJS uses `CRASH` events for consumer failures.

### JS-28: Async iteration sharp edges

**Priority:** P1

```typescript
// WRONG — no error handling in async iteration
for await (const msg of subscription) {
  processMessage(msg); // if processMessage throws, iteration stops AND resources may leak
}

// WRONG — no timeout on stuck iterators
for await (const msg of subscription) {
  // If the server stops sending, this blocks forever
  await processMessage(msg);
}

// RIGHT — error handling + timeout
const controller = new AbortController();
const timeout = setTimeout(() => controller.abort(), idleTimeoutMs);

try {
  for await (const msg of client.subscribe('channel', { signal: controller.signal })) {
    timeout.refresh(); // reset idle timeout on each message
    try {
      await processMessage(msg);
    } catch (err) {
      // Per-message error handling — don't break the loop
      logger.error('Failed to process message', { error: err });
    }
  }
} finally {
  clearTimeout(timeout);
}
```

- Document that throwing inside `for await...of` stops iteration and calls `.return()`.
- Always wrap per-message processing in `try/catch` if iteration should continue on error.
- Use `AbortSignal.timeout(ms)` (Node.js 20+) for simple timeouts.
- Warn about the "dangling iterator" problem — if an async iterator isn't fully consumed AND `.return()` isn't called, it leaks resources.
- **K8s ecosystem reference:** NATS.js had a dangling iterator bug (issue #134) that caused memory leaks.

---

## Configuration & Validation

### JS-29: Options-bag pattern for all public methods with >2 parameters

**Priority:** P0

```typescript
// WRONG — positional parameters are unreadable and fragile
function send(channel: string, body: Uint8Array, metadata: string, tags: Map<string, string>, timeout: number): Promise<SendResult>;

// WRONG — boolean flag parameters are ambiguous
function connect(address: string, useTls: boolean, autoReconnect: boolean): Promise<void>;

// RIGHT — options bag with defaults
interface SendOptions {
  channel: string;
  body: Uint8Array;
  metadata?: string;
  tags?: Record<string, string>;
  timeoutMs?: number;
  signal?: AbortSignal;
}

async function send(options: SendOptions): Promise<SendResult>;

// RIGHT — required params + options bag
async function send(channel: string, body: Uint8Array, options?: SendOptions): Promise<SendResult>;
```

- Methods with >2 parameters MUST use an options object.
- Boolean parameters MUST always be in an options object, never positional.
- Default values use spread: `const config = { timeoutMs: 5000, ...userOptions };`.
- Document all defaults in TSDoc comments.
- **K8s ecosystem reference:** Azure SDK mandates options bags for all methods. AWS SDK v3 uses `Command` objects as typed options bags. ioredis uses options objects for all configuration.

### JS-30: Validate configuration at construction time

**Priority:** P1

```typescript
// WRONG — validation at call time, unclear when errors surface
class Client {
  constructor(private config: ClientConfig) {}
  async connect(): Promise<void> {
    if (!this.config.address) throw new Error('address required'); // too late!
  }
}

// RIGHT — validate in constructor, fail fast
class Client {
  private readonly config: Readonly<Required<InternalConfig>>;

  constructor(options: ClientOptions) {
    // Validate required fields
    if (!options.address || typeof options.address !== 'string') {
      throw new KubeMQError('address is required and must be a non-empty string', ErrorCode.InvalidArgument);
    }

    // Apply defaults and freeze
    this.config = Object.freeze({
      address: options.address,
      clientId: options.clientId ?? `client-${randomUUID()}`,
      maxRetries: options.maxRetries ?? 3,
      timeoutMs: options.timeoutMs ?? 5000,
      reconnectIntervalMs: options.reconnectIntervalMs ?? 1000,
      maxReconnectIntervalMs: options.maxReconnectIntervalMs ?? 30_000,
    });
  }
}
```

- All required configuration MUST be validated in the constructor.
- Invalid configuration MUST throw `KubeMQError` with code `INVALID_ARGUMENT`, not generic `Error`.
- Use `Object.freeze()` on the internal config to prevent mutation.
- Defensive copy the options object — do not hold a reference to the caller's object.
- Consider Zod for complex validation, but only as an optional peer dependency — never a hard dep.
- **K8s ecosystem reference:** ioredis validates in constructor. Azure SDK validates in constructor with descriptive errors.

### JS-31: Retry configuration must be explicit and type-safe

**Priority:** P0

```typescript
interface RetryConfig {
  /** Maximum number of retry attempts. 0 = no retries. */
  readonly maxRetries: number;
  /** Initial delay between retries in milliseconds. */
  readonly initialDelayMs: number;
  /** Maximum delay between retries in milliseconds. */
  readonly maxDelayMs: number;
  /** Backoff multiplier. Default 2 (exponential). */
  readonly multiplier: number;
  /** Jitter factor (0-1). 0 = no jitter, 1 = full jitter. */
  readonly jitter: number;
  /** Error codes that should be retried. Default: transient errors only. */
  readonly retryableErrors?: ReadonlyArray<ErrorCode>;
}

const DEFAULT_RETRY: Readonly<RetryConfig> = {
  maxRetries: 3,
  initialDelayMs: 100,
  maxDelayMs: 30_000,
  multiplier: 2,
  jitter: 0.2,
  retryableErrors: [ErrorCode.ConnectionFailed, ErrorCode.Timeout, ErrorCode.ResourceExhausted],
};
```

- Retry logic MUST use exponential backoff with jitter.
- Non-retryable errors (auth failure, invalid argument) MUST NOT be retried.
- `AbortSignal` cancellation MUST NOT be retried — it means the caller wants to stop.
- The retry function MUST accept an `AbortSignal` and check it between retries.
- Document which operations are idempotent and safe to retry vs. which are not.
- **K8s ecosystem reference:** AWS SDK v3 uses `StandardRetryStrategy` with configurable backoff. Azure SDK has `defaultRetryPolicy` with exponential backoff. KafkaJS retries 5 times with multiplier 2. ioredis uses `retryStrategy` function pattern.

---

## gRPC-Specific

### JS-32: Map gRPC status codes to KubeMQ error types

**Priority:** P0

```typescript
import * as grpc from '@grpc/grpc-js';

function mapGrpcError(err: grpc.ServiceError): KubeMQError {
  const cause = err;
  switch (err.code) {
    case grpc.status.DEADLINE_EXCEEDED:
      return new KubeMQTimeoutError('gRPC call', 0, { cause });
    case grpc.status.UNAUTHENTICATED:
    case grpc.status.PERMISSION_DENIED:
      return new KubeMQAuthError(err.details || 'Authentication failed', { cause });
    case grpc.status.UNAVAILABLE:
      return new KubeMQConnectionError(this.address, { cause });
    case grpc.status.CANCELLED:
      return new KubeMQError(err.details || 'Cancelled', ErrorCode.Cancelled, { cause });
    case grpc.status.INVALID_ARGUMENT:
      return new KubeMQError(err.details || 'Invalid argument', ErrorCode.InvalidArgument, { cause });
    case grpc.status.NOT_FOUND:
      return new KubeMQError(err.details || 'Not found', ErrorCode.NotFound, { cause });
    case grpc.status.RESOURCE_EXHAUSTED:
      return new KubeMQError(err.details || 'Resource exhausted', ErrorCode.ResourceExhausted, { cause });
    default:
      return new KubeMQError(err.details || 'Internal error', ErrorCode.Internal, { cause });
  }
}
```

- Every gRPC call MUST map `grpc.ServiceError` to a `KubeMQError` subtype.
- Never expose raw `grpc.ServiceError` to SDK consumers.
- The `grpc.ServiceError` type is `StatusObject & Error` — it has `.code`, `.details`, and `.metadata`.
- **WRONG:** `throw err;` where `err` is a `grpc.ServiceError` — leaks gRPC internals.
- **RIGHT:** `throw mapGrpcError(err);` — consistent error types for consumers.
- Note: `grpc-js` v1.11.1 crashed on custom status codes (>16); ensure minimum `@grpc/grpc-js@1.11.2`.
- **K8s ecosystem reference:** Azure SDK maps HTTP status codes to `RestError` in a central place. `@kubernetes/client-node` maps HTTP codes to `ApiException`.

### JS-33: gRPC channel reuse and lifecycle

**Priority:** P0

```typescript
// WRONG — creating a new client per request
async function send(address: string, msg: Message): Promise<void> {
  const client = new kubemq.KubemqClient(address, grpc.credentials.createInsecure());
  await client.sendQueueMessage(msg);
  // client leaked — never closed!
}

// WRONG — creating client without closing old one
class Client {
  reconnect(): void {
    this.grpcClient = new kubemq.KubemqClient(this.address, this.creds);
    // Old client leaked!
  }
}

// RIGHT — single client, explicit lifecycle
class Client {
  private grpcClient: kubemq.KubemqClient;

  constructor(options: ClientOptions) {
    this.grpcClient = this.createGrpcClient(options);
  }

  private createGrpcClient(options: ClientOptions): kubemq.KubemqClient {
    return new kubemq.KubemqClient(
      options.address,
      this.createCredentials(options),
      {
        'grpc.keepalive_time_ms': options.keepaliveMs ?? 30_000,
        'grpc.keepalive_timeout_ms': 20_000,
        'grpc.keepalive_permit_without_calls': 0,
        'grpc.initial_reconnect_backoff_ms': options.reconnectIntervalMs ?? 1_000,
        'grpc.max_reconnect_backoff_ms': options.maxReconnectIntervalMs ?? 30_000,
      }
    );
  }

  async close(): Promise<void> {
    this.grpcClient.close(); // grpc.Client.close() is synchronous
  }
}
```

- Create ONE gRPC client per `Client` instance. Reuse it for the application lifetime.
- `grpc.Client.close()` is synchronous — don't `await` it.
- Always close the old client BEFORE creating a new one during reconnection.
- gRPC channels handle reconnection internally (IDLE → CONNECTING → READY → TRANSIENT_FAILURE cycle). Do NOT implement custom reconnection on top of gRPC's built-in mechanism unless you need to replace the entire channel.
- **K8s ecosystem reference:** grpc-node issue #2893 documents memory leaks from not closing clients. grpc-node PR #2896 improved IDLE channel GC.

### JS-34: gRPC metadata for auth tokens

**Priority:** P0

```typescript
import * as grpc from '@grpc/grpc-js';

// WRONG — setting auth in every call manually
async function send(msg: Message, token: string): Promise<void> {
  const metadata = new grpc.Metadata();
  metadata.set('authorization', `Bearer ${token}`);
  await this.grpcClient.sendQueueMessage(msg, metadata);
}

// RIGHT — use call credentials for automatic metadata injection
function createAuthCredentials(token: string): grpc.CallCredentials {
  return grpc.credentials.createFromMetadataGenerator(
    (_params, callback) => {
      const metadata = new grpc.Metadata();
      metadata.set('authorization', `Bearer ${token}`);
      callback(null, metadata);
    }
  );
}

// RIGHT — combine with channel credentials (TLS + auth)
function createCredentials(options: ClientOptions): grpc.ChannelCredentials {
  const channelCreds = options.tls
    ? grpc.credentials.createSsl(
        options.tls.caFile ? fs.readFileSync(options.tls.caFile) : undefined,
        options.tls.keyFile ? fs.readFileSync(options.tls.keyFile) : undefined,
        options.tls.certFile ? fs.readFileSync(options.tls.certFile) : undefined,
      )
    : grpc.credentials.createInsecure();

  if (options.authToken) {
    const callCreds = createAuthCredentials(options.authToken);
    return grpc.credentials.combineChannelCredentials(channelCreds, callCreds);
  }
  return channelCreds;
}
```

- Use `grpc.credentials.createFromMetadataGenerator()` for auth — it injects metadata into every call automatically.
- Combine channel credentials (TLS) with call credentials (auth) via `combineChannelCredentials()`.
- **WRONG:** Combining call credentials with `createInsecure()` — gRPC requires TLS for call credentials in production.
- For development, use `createInsecure()` alone (no auth) or use `createSsl()` with self-signed certs.
- Metadata keys are case-insensitive. Binary metadata keys MUST end with `-bin`.

### JS-35: gRPC client interceptors for cross-cutting concerns

**Priority:** P1

```typescript
import * as grpc from '@grpc/grpc-js';

// Interceptor factory for logging + tracing
function createLoggingInterceptor(logger: Logger): grpc.Interceptor {
  return (options, nextCall) => {
    const method = options.method_definition.path;
    const startTime = performance.now();

    return new grpc.InterceptingCall(nextCall(options), {
      start(metadata, listener, next) {
        next(metadata, {
          onReceiveMessage(message, next) {
            next(message);
          },
          onReceiveStatus(status, next) {
            const durationMs = performance.now() - startTime;
            if (status.code !== grpc.status.OK) {
              logger.warn('gRPC call failed', { method, code: status.code, durationMs });
            } else {
              logger.debug('gRPC call completed', { method, durationMs });
            }
            next(status);
          },
        });
      },
    });
  };
}
```

- Use interceptors for logging, metrics, tracing — never duplicate this logic in each RPC call.
- Interceptor order matters: place logging closer to the network, auth closer to the application.
- For streaming calls, `sendMessage` and `onReceiveMessage` are called per message.
- Interceptors compose via array: `{ interceptors: [authInterceptor, loggingInterceptor] }`.
- **K8s ecosystem reference:** Azure SDK uses pipeline policies (same concept). AWS SDK v3 uses middleware stack with `initialize → serialize → build → finalize` phases.

### JS-36: gRPC streaming — prefer `AsyncIterator` wrapper over raw events

**Priority:** P1

```typescript
// WRONG — raw event listener pattern
function subscribe(channel: string): EventEmitter {
  const stream = this.grpcClient.subscribeToEvents(request);
  const emitter = new EventEmitter();
  stream.on('data', (data) => emitter.emit('message', data));
  stream.on('error', (err) => emitter.emit('error', err));
  stream.on('end', () => emitter.emit('end'));
  return emitter; // no cleanup, no backpressure, listener leak risk
}

// RIGHT — AsyncIterator wrapper with cleanup
async function* subscribe(
  channel: string,
  options?: { signal?: AbortSignal }
): AsyncGenerator<Message, void, undefined> {
  const stream = this.grpcClient.subscribeToEvents(request);

  // Wire up abort signal
  const onAbort = () => stream.cancel();
  options?.signal?.addEventListener('abort', onAbort, { once: true });

  try {
    for await (const data of stream) {
      yield this.mapMessage(data);
    }
  } finally {
    options?.signal?.removeEventListener('abort', onAbort);
    stream.cancel();
  }
}
```

- gRPC server-streaming calls in `@grpc/grpc-js` implement `AsyncIterable` — use `for await...of` directly.
- Wrap the raw stream in an `AsyncGenerator` to map protobuf messages to SDK types.
- Always wire `AbortSignal` to `stream.cancel()` for cancellation.
- Always clean up in the `finally` block.
- Client-streaming calls use `stream.write()` + `stream.end()` — wrap in a helper that accepts an `AsyncIterable<T>` input.

---

## Security & Defensive Coding

### JS-37: Defensive copying of options objects

**Priority:** P1

```typescript
// WRONG — holding reference to caller's object
class Client {
  constructor(private options: ClientOptions) {} // caller can mutate this.options!
}

// WRONG — shallow spread doesn't copy nested objects
class Client {
  constructor(options: ClientOptions) {
    this.options = { ...options }; // options.tls is still shared!
  }
}

// RIGHT — deep defensive copy using structuredClone (Node 20+)
class Client {
  private readonly config: Readonly<InternalConfig>;

  constructor(options: ClientOptions) {
    this.config = Object.freeze({
      address: options.address,
      clientId: options.clientId ?? randomUUID(),
      tls: options.tls ? { ...options.tls } : undefined,
      retry: { ...DEFAULT_RETRY, ...options.retry },
    });
  }
}
```

- Never store a reference to the caller's options object.
- Use spread for flat objects, `structuredClone()` for deep objects (Node 20+).
- Avoid prototype pollution: never use `Object.assign({}, untrustedInput)` with untrusted input — use allowlisted key extraction instead.
- **K8s ecosystem reference:** Azure SDK and AWS SDK v3 both deep-copy configuration internally.

### JS-38: Timer cleanup — always call `.unref()` or track timers

**Priority:** P0

```typescript
// WRONG — keepalive timer prevents process from exiting
class Client {
  constructor() {
    this.keepaliveTimer = setInterval(() => this.ping(), 30_000);
    // Process will never exit naturally!
  }
}

// RIGHT — .unref() timers that shouldn't keep the process alive
class Client {
  private keepaliveTimer: ReturnType<typeof setInterval> | undefined;
  private reconnectTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    this.keepaliveTimer = setInterval(() => this.ping(), 30_000);
    this.keepaliveTimer.unref(); // won't prevent process exit
  }

  async close(): Promise<void> {
    if (this.keepaliveTimer) {
      clearInterval(this.keepaliveTimer);
      this.keepaliveTimer = undefined;
    }
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = undefined;
    }
  }
}
```

- All `setInterval` and `setTimeout` calls in the SDK MUST be `.unref()`'d.
- All timers MUST be cleared in `close()`.
- Use `ReturnType<typeof setTimeout>` for timer types — NOT `number` (that's browser) or `NodeJS.Timeout` (that's Node-specific).
- **K8s ecosystem reference:** ioredis `.unref()`s its reconnection timers. NATS.js tracks all timers for cleanup.

### JS-39: Graceful shutdown pattern

**Priority:** P1

```typescript
// WRONG — process.exit() prevents cleanup
process.on('SIGTERM', () => process.exit(0)); // close() never called!

// RIGHT — graceful shutdown example (for documentation)
const client = new Client({ address: 'localhost:50000' });

async function shutdown(signal: string): Promise<void> {
  console.log(`Received ${signal}, shutting down...`);
  const timeout = setTimeout(() => {
    console.error('Shutdown timed out, forcing exit');
    process.exit(1);
  }, 10_000);
  timeout.unref();

  try {
    await client.close(); // drains in-flight, cancels subscriptions, closes channel
  } finally {
    clearTimeout(timeout);
    process.exit(0);
  }
}

process.on('SIGTERM', () => void shutdown('SIGTERM'));
process.on('SIGINT', () => void shutdown('SIGINT'));
```

- The SDK MUST NOT register global signal handlers — that's the application's responsibility.
- The SDK MUST provide a `close()` method that performs graceful cleanup.
- `close()` MUST have a configurable timeout for draining in-flight operations.
- Document the shutdown pattern in README / API docs.
- **K8s ecosystem reference:** Azure SDK documents OTel shutdown with SIGTERM trapping for Kubernetes pods. KafkaJS provides `consumer.disconnect()` for graceful shutdown.

---

## Packaging & Build

### JS-40: TypeScript compiler configuration for libraries

**Priority:** P0

```jsonc
// tsconfig.json — recommended for SDK libraries
{
  "compilerOptions": {
    // Strict type checking
    "strict": true,
    "exactOptionalPropertyTypes": true,
    "noUncheckedIndexedAccess": true,
    "noPropertyAccessFromIndexSignature": true,
    "useUnknownInCatchVariables": true,

    // Module system
    "module": "Node16",
    "moduleResolution": "Node16",
    "target": "ES2022",
    "lib": ["ES2022"],

    // Output
    "declaration": true,
    "declarationMap": true,
    "sourceMap": true,
    "outDir": "./dist",

    // Interop
    "esModuleInterop": true,
    "forceConsistentCasingInFileNames": true,
    "isolatedModules": true,
    "skipLibCheck": true,

    // Strict output
    "noEmitOnError": true,
    "noFallthroughCasesInSwitch": true,
    "noImplicitOverride": true,
    "noImplicitReturns": true
  }
}
```

- `"target": "ES2022"` — provides `Error.cause`, top-level `await`, `Array.at()`, `Object.hasOwn()`.
- `"exactOptionalPropertyTypes": true` — distinguishes `{ x?: string }` from `{ x: string | undefined }`.
- `"noUncheckedIndexedAccess": true` — forces `undefined` checks on array/object index access.
- `"module": "Node16"` — correct resolution for Node.js with ESM support.
- Specs MUST NOT use `"module": "commonjs"` — use a build tool (tsup) for CJS output.

### JS-41: Subpath exports for modular API

**Priority:** P2

```json
{
  "exports": {
    ".": {
      "import": { "types": "./dist/esm/index.d.ts", "default": "./dist/esm/index.js" },
      "require": { "types": "./dist/cjs/index.d.cts", "default": "./dist/cjs/index.cjs" }
    },
    "./queues": {
      "import": { "types": "./dist/esm/queues/index.d.ts", "default": "./dist/esm/queues/index.js" },
      "require": { "types": "./dist/cjs/queues/index.d.cts", "default": "./dist/cjs/queues/index.cjs" }
    },
    "./pubsub": {
      "import": { "types": "./dist/esm/pubsub/index.d.ts", "default": "./dist/esm/pubsub/index.js" },
      "require": { "types": "./dist/cjs/pubsub/index.d.cts", "default": "./dist/cjs/pubsub/index.cjs" }
    },
    "./commands": {
      "import": { "types": "./dist/esm/commands/index.d.ts", "default": "./dist/esm/commands/index.js" },
      "require": { "types": "./dist/cjs/commands/index.d.cts", "default": "./dist/cjs/commands/index.cjs" }
    }
  }
}
```

- Subpath exports enable tree-shaking and modular imports: `import { QueueClient } from '@kubemq/sdk/queues';`.
- Only expose subpaths if the SDK is large enough to benefit from code splitting.
- Always include `"types"` in every condition — TypeScript won't find declarations otherwise.
- **K8s ecosystem reference:** Azure SDK uses modular subpath exports extensively. AWS SDK v3 ships each service as a separate package (`@aws-sdk/client-s3`, etc.).

### JS-42: ESLint configuration for SDK codebases

**Priority:** P2

```typescript
// eslint.config.ts — ESLint 9 flat config
import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig([
  ...tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        projectService: true,
      },
    },
    rules: {
      '@typescript-eslint/no-floating-promises': 'error',
      '@typescript-eslint/no-misused-promises': 'error',
      '@typescript-eslint/require-await': 'error',
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-unsafe-assignment': 'error',
      '@typescript-eslint/strict-boolean-expressions': 'error',
      'no-console': 'error', // SDKs should never use console.*
    },
  },
]);
```

- **Critical rules for SDKs:**
  - `no-floating-promises` — catches fire-and-forget async calls.
  - `no-misused-promises` — catches passing async to sync-expected callbacks.
  - `no-explicit-any` — forces use of `unknown` instead.
  - `no-console` — SDKs must use a logger abstraction, never `console.*`.
- Use ESLint 9 flat config format (`.eslintrc` is deprecated).
- Specs should not prescribe specific ESLint config but should note which lint rules the code must pass.

---

## Testing

### JS-43: gRPC test doubles — mock the client, not the server

**Priority:** P1

```typescript
// WRONG — starting a real gRPC server for unit tests
describe('Client', () => {
  let server: grpc.Server;
  beforeAll(async () => {
    server = new grpc.Server();
    server.addService(kubemqService, { sendQueueMessage: mockHandler });
    await new Promise((resolve) => server.bindAsync('0.0.0.0:0', creds, resolve));
  });
  // Slow, flaky, port conflicts
});

// RIGHT — mock the gRPC client methods directly
import { vi, describe, it, expect } from 'vitest';

describe('QueueClient.send', () => {
  it('should send a message and return the result', async () => {
    const mockGrpcClient = {
      sendQueueMessage: vi.fn((_req, _meta, callback) => {
        callback(null, { messageID: 'msg-1', sentAt: Date.now() });
      }),
    };

    const client = new QueueClient({ address: 'test:50000' });
    // Inject mock — depends on implementation (constructor injection, or vi.spyOn)
    (client as any).grpcClient = mockGrpcClient;

    const result = await client.send({ channel: 'q1', body: Buffer.from('hello') });
    expect(result.messageId).toBe('msg-1');
    expect(mockGrpcClient.sendQueueMessage).toHaveBeenCalledOnce();
  });
});
```

- Unit tests MUST mock the gRPC client, not start a real server.
- Integration tests MAY start a real server but MUST be clearly separated and optional.
- Use dependency injection or factory functions to make the gRPC client swappable.
- Never hardcode ports in tests — use port 0 for dynamic assignment in integration tests.
- **K8s ecosystem reference:** `@kubernetes/client-node` uses nock for HTTP mocking. AWS SDK v3 provides `aws-sdk-client-mock` library. KafkaJS mocks brokers in tests.

### JS-44: Test async error paths explicitly

**Priority:** P1

```typescript
describe('error handling', () => {
  it('should throw KubeMQTimeoutError on deadline exceeded', async () => {
    const mockGrpcClient = {
      sendQueueMessage: vi.fn((_req, _meta, callback) => {
        const error = Object.assign(new Error('deadline exceeded'), {
          code: grpc.status.DEADLINE_EXCEEDED,
          details: 'deadline exceeded',
          metadata: new grpc.Metadata(),
        });
        callback(error);
      }),
    };

    const client = createClientWithMock(mockGrpcClient);
    await expect(client.send({ channel: 'q1', body: new Uint8Array() }))
      .rejects.toThrow(KubeMQTimeoutError);
  });

  it('should respect AbortSignal cancellation', async () => {
    const controller = new AbortController();
    controller.abort(); // pre-abort

    const client = new Client({ address: 'test:50000' });
    await expect(client.send({ channel: 'q1', body: new Uint8Array() }, { signal: controller.signal }))
      .rejects.toThrow(KubeMQError);
  });

  it('should include cause in wrapped errors', async () => {
    const originalError = new Error('network failure');
    // ... setup mock to throw originalError ...

    try {
      await client.send(msg);
    } catch (err) {
      expect(err).toBeInstanceOf(KubeMQError);
      expect((err as KubeMQError).cause).toBe(originalError);
    }
  });
});
```

- Specs MUST include test cases for ALL error types in the error hierarchy.
- Test the `cause` chain — verify original errors are preserved.
- Test `AbortSignal` cancellation for every async method.
- Test timeout behavior with mocked timers (`vi.useFakeTimers()`).

---

## Observability

### JS-45: OpenTelemetry integration via optional peer dependency

**Priority:** P1

```typescript
// Internal OTel wrapper — lazy-loaded, gracefully degrades
let _tracer: import('@opentelemetry/api').Tracer | null = null;
let _otelApi: typeof import('@opentelemetry/api') | null = null;

async function getTracer(): Promise<import('@opentelemetry/api').Tracer | null> {
  if (_tracer !== null) return _tracer;
  try {
    _otelApi = await import('@opentelemetry/api');
    _tracer = _otelApi.trace.getTracer('@kubemq/sdk', SDK_VERSION);
    return _tracer;
  } catch {
    return null; // OTel not installed — no-op
  }
}

// Usage in SDK methods
async function send(msg: Message): Promise<SendResult> {
  const tracer = await getTracer();
  if (!tracer) return this.sendInternal(msg); // no tracing

  return tracer.startActiveSpan('kubemq.send', {
    kind: _otelApi!.SpanKind.CLIENT,
    attributes: {
      'rpc.system': 'grpc',
      'rpc.service': 'kubemq.Kubemq',
      'rpc.method': 'SendQueueMessage',
      'messaging.system': 'kubemq',
      'messaging.destination.name': msg.channel,
      'messaging.operation.type': 'publish',
    },
  }, async (span) => {
    try {
      const result = await this.sendInternal(msg);
      span.setStatus({ code: _otelApi!.SpanStatusCode.OK });
      return result;
    } catch (err) {
      span.setStatus({ code: _otelApi!.SpanStatusCode.ERROR, message: String(err) });
      span.recordException(err instanceof Error ? err : new Error(String(err)));
      throw err;
    } finally {
      span.end();
    }
  });
}
```

- OTel MUST be an optional peer dependency (see JS-13).
- Use `import()` for lazy loading — never top-level `import` of `@opentelemetry/api`.
- Cache the tracer — don't call `trace.getTracer()` on every operation.
- Use standard [OTel semantic conventions](https://opentelemetry.io/docs/specs/semconv/) for span attributes:
  - `rpc.system`, `rpc.service`, `rpc.method` for gRPC calls.
  - `messaging.system`, `messaging.destination.name`, `messaging.operation.type` for messaging.
- Always `span.end()` in `finally` — never leave spans open.
- Set span status to `ERROR` on failure and call `recordException()`.
- **K8s ecosystem reference:** `@opentelemetry/instrumentation-grpc` auto-instruments gRPC. Azure SDK has built-in tracing via `@azure/core-tracing`. AWS SDK v3 has middleware-based OTel integration.

### JS-46: Logging abstraction — never use `console.*`

**Priority:** P0

```typescript
// WRONG — direct console usage in SDK
export class Client {
  async connect(): Promise<void> {
    console.log('Connecting to', this.address); // pollutes consumer's stdout
  }
}

// RIGHT — pluggable logger interface
export interface Logger {
  debug(message: string, context?: Record<string, unknown>): void;
  info(message: string, context?: Record<string, unknown>): void;
  warn(message: string, context?: Record<string, unknown>): void;
  error(message: string, context?: Record<string, unknown>): void;
}

// Default no-op logger — produces no output unless user provides one
const NOOP_LOGGER: Logger = {
  debug() {},
  info() {},
  warn() {},
  error() {},
};

export interface ClientOptions {
  // ...
  logger?: Logger;
}

class Client {
  private readonly logger: Logger;

  constructor(options: ClientOptions) {
    this.logger = options.logger ?? NOOP_LOGGER;
  }

  async connect(): Promise<void> {
    this.logger.info('Connecting', { address: this.address });
  }
}
```

- SDK MUST NOT use `console.log`, `console.warn`, `console.error` anywhere.
- Provide a `Logger` interface that users can implement with their preferred logging library.
- Default to a no-op logger — SDKs should be silent by default.
- The `Logger` interface uses structured context objects, not positional args.
- **K8s ecosystem reference:** KafkaJS accepts a custom `logCreator`. ioredis accepts a custom logger function. Azure SDK uses `@azure/logger` with a pluggable interface.

---

## Resource Management & Lifecycle

### JS-47: Connection state machine with events

**Priority:** P1

```typescript
export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'closed';

export interface ConnectionStateChange {
  previous: ConnectionState;
  current: ConnectionState;
  error?: KubeMQError;
}

export interface ClientEvents {
  stateChange: (change: ConnectionStateChange) => void;
  error: (error: KubeMQError) => void;
}

class Client extends TypedEventEmitter<ClientEvents> {
  private state: ConnectionState = 'disconnected';

  private setState(newState: ConnectionState, error?: KubeMQError): void {
    const previous = this.state;
    if (previous === newState) return;
    if (previous === 'closed') return; // terminal state
    this.state = newState;
    this.emit('stateChange', { previous, current: newState, error });
  }

  getState(): ConnectionState { return this.state; }
}
```

- Expose connection state as a finite state machine with well-defined transitions.
- Emit `stateChange` events so consumers can react (e.g., pause writes during reconnection).
- `'closed'` is a terminal state — once closed, the client cannot reconnect.
- Always validate state transitions — never go from `'closed'` to `'connecting'`.
- **K8s ecosystem reference:** NATS.js exposes connection state via `status()` AsyncIterable. ioredis emits `connect`, `ready`, `close`, `reconnecting` events. gRPC channels have 5 states (IDLE, CONNECTING, READY, TRANSIENT_FAILURE, SHUTDOWN).

### JS-48: TypedEventEmitter for type-safe events

**Priority:** P1

```typescript
import { EventEmitter } from 'node:events';

// WRONG — untyped EventEmitter
class Client extends EventEmitter {
  // client.on('mesage', ...) — typo not caught!
  // client.emit('error', 'string') — wrong payload type, not caught!
}

// RIGHT — typed event map
interface ClientEventMap {
  stateChange: [ConnectionStateChange];
  error: [KubeMQError];
}

class TypedEventEmitter<T extends Record<string, unknown[]>> extends EventEmitter {
  override on<K extends keyof T & string>(event: K, listener: (...args: T[K]) => void): this {
    return super.on(event, listener as (...args: unknown[]) => void);
  }
  override emit<K extends keyof T & string>(event: K, ...args: T[K]): boolean {
    return super.emit(event, ...args);
  }
  override off<K extends keyof T & string>(event: K, listener: (...args: T[K]) => void): this {
    return super.off(event, listener as (...args: unknown[]) => void);
  }
}

class Client extends TypedEventEmitter<ClientEventMap> { ... }
```

- All EventEmitter usage MUST be type-safe — use a `TypedEventEmitter` wrapper.
- Event names are constrained to the event map — typos cause compile errors.
- Event payloads are typed — wrong data shapes cause compile errors.
- Consider using strict-event-emitter or eventemitter3 for better typed alternatives.

---

## Cross-Platform Compatibility

### JS-49: Use `Uint8Array` instead of `Buffer` in public API

**Priority:** P1

```typescript
// WRONG — Buffer is Node.js-only
export interface Message {
  channel: string;
  body: Buffer; // breaks in browsers, Deno, Bun without polyfill
}

// RIGHT — Uint8Array is universal
export interface Message {
  channel: string;
  body: Uint8Array; // works everywhere
}

// Internal conversion (when gRPC requires Buffer)
function toBuffer(data: Uint8Array): Buffer {
  return Buffer.from(data.buffer, data.byteOffset, data.byteLength);
}
```

- Public API types MUST use `Uint8Array`, not `Buffer`.
- Internal code may use `Buffer` where required by `@grpc/grpc-js`.
- **WRONG:** `Buffer.from(uint8array)` — copies the entire data.
- **RIGHT:** `Buffer.from(uint8array.buffer, uint8array.byteOffset, uint8array.byteLength)` — zero-copy view.
- Note: `Buffer.slice()` and `Uint8Array.slice()` have different semantics (shared vs. copy). Never rely on `Buffer.slice()` behavior.
- **K8s ecosystem reference:** Node.js core is moving toward `Uint8Array` in new APIs (node issue #41588). The `file-type` library migrated from Buffer to Uint8Array in 2024.

### JS-50: Use `crypto.randomUUID()` instead of `uuid` package

**Priority:** P2

```typescript
// WRONG — unnecessary dependency
import { v4 as uuidv4 } from 'uuid';
const id = uuidv4();

// RIGHT — built-in (Node 19+, all modern browsers)
import { randomUUID } from 'node:crypto';
const id = randomUUID();

// Or globally available
const id = crypto.randomUUID(); // globalThis.crypto since Node 19
```

- Prefer built-in `crypto.randomUUID()` over the `uuid` npm package.
- Reduces dependency surface — fewer packages = fewer supply-chain risks.
- Available in Node.js 19+, all modern browsers, Deno, Bun.
- For Node.js 20+ (our minimum), this is always available.

---

## Priority Summary

| Priority | Rule IDs | Count |
|----------|----------|-------|
| **P0 — Must implement** | JS-1, JS-4, JS-14, JS-19, JS-20, JS-21, JS-26, JS-27, JS-29, JS-31, JS-32, JS-33, JS-34, JS-38, JS-40, JS-46 | 16 |
| **P1 — Should implement** | JS-11, JS-18, JS-22, JS-23, JS-25, JS-28, JS-30, JS-35, JS-36, JS-37, JS-39, JS-43, JS-44, JS-45, JS-47, JS-48, JS-49 | 17 |
| **P2 — Nice to have** | JS-24, JS-41, JS-42, JS-50 | 4 |
| **No priority (unchanged)** | JS-2, JS-3, JS-5, JS-6, JS-7, JS-8, JS-9, JS-10, JS-12, JS-13, JS-15, JS-16, JS-17 | 13 |

## K8s Ecosystem SDK Pattern Summary

| Pattern | kubernetes-client/js | @grpc/grpc-js | nats.js | kafkajs | ioredis | Azure SDK | AWS SDK v3 |
|---------|---------------------|---------------|---------|---------|---------|-----------|-----------|
| Custom error classes | `ApiException` | `ServiceError` | Yes | Yes | Yes | `RestError` | `ServiceException` |
| Error codes | HTTP status | gRPC status | String | String | String | String | String + `$fault` |
| Reconnection | Informer-level | Channel-level | Client-level | Consumer-level | Client-level | Pipeline retry | Middleware retry |
| Async pattern | Promise | Callback + AsyncIterable | AsyncIterable | Promise + EventEmitter | Promise + EventEmitter | Promise | Promise |
| Config pattern | Options object | Channel options | Options object | Options object | Options object | Options object | Command objects |
| OTel integration | External | `instrumentation-grpc` | External | External | External | `@azure/core-tracing` | Middleware |
| `instanceof` safety | Re-exports | N/A | N/A | N/A | N/A | N/A | N/A |
| Symbol.hasInstance | No | No | No | No | No | No | No (Temporal SDK does) |
| TypedEventEmitter | No | No | No | No | No | No | N/A |
| AsyncIterable subs | No (EventEmitter) | Yes (streams) | Yes | No (EventEmitter) | No (EventEmitter) | Yes (paging) | Yes (paging) |
