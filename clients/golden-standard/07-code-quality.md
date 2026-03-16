# Category 7: Code Quality & Architecture

**Tier:** 1 (Critical — gate blocker)
**Current Average Score:** 2.85 / 5.0
**Target Score:** 4.0+
**Weight:** 6%

## Purpose

SDK code must be clean, well-structured, and maintainable. Architecture must separate concerns clearly. Linting must be enforced in CI. Dependencies must be minimal.

**Public API surface definition:** The public API surface is all exported types, functions, and methods not in `internal`/`_internal` packages. Changes to the public API surface require a SemVer-appropriate version bump. Types exported for technical reasons but not intended for user use must be marked accordingly per language convention.

---

## Requirements

### REQ-CQ-1: Layered Architecture

Every SDK must follow a 3-layer architecture with clear separation of concerns.

**Layers:**

```
┌─────────────────────────────────┐
│  Public API Layer               │  ← User-facing types, builders/options, convenience methods
│  (Client, Options, Messages)    │
├─────────────────────────────────┤
│  Protocol Layer                 │  ← Error mapping, retry logic, auth injection, OTel instrumentation
│  (Middleware, Interceptors)     │
├─────────────────────────────────┤
│  Transport Layer                │  ← gRPC client stubs, connection management, keepalive
│  (gRPC Channel, Streams)       │
└─────────────────────────────────┘
```

Cross-cutting concerns (logging, OTel providers, configuration) are injected at construction time and available to all layers. They are not a layer.

Connection lifecycle (connect, reconnect, keepalive, drain) is a Transport concern. Operation retry (REQ-ERR-3) is a Protocol concern. These must not be conflated.

> **OTel placement:** Transport receives MeterProvider for connection metrics. Protocol receives TracerProvider and MeterProvider for operation instrumentation.

> **Error wrapping flow:** Error wrapping flows upward: Transport wraps gRPC errors → Protocol classifies and adds context → Public API surfaces typed errors. See REQ-ERR-1 and REQ-ERR-6.

**Acceptance criteria:**
- [ ] Public API types do not directly reference gRPC/protobuf types
- [ ] Protocol layer handles error wrapping, retry, auth, and observability
- [ ] Transport layer is the only code that imports gRPC packages
- [ ] Layers communicate via interfaces (not concrete types) where practical
- [ ] Users can import the SDK without pulling in gRPC-internal types
- [ ] Dependencies flow downward only: Public API → Protocol → Transport. No upward or circular references

### REQ-CQ-2: Internal vs Public API Separation

SDK internals must not be accessible to users.

**Mechanisms per language:**

| Language | Hide Internals |
|----------|---------------|
| Go | `internal/` package (compiler-enforced) |
| Java | Package-private classes (no `public` modifier) |
| C# | `internal` access modifier |
| Python | `_` prefix convention + `__all__` exports |
| JS/TS | Barrel exports in `index.ts`, unexported internals |

**Acceptance criteria:**
- [ ] Internal implementation details are not importable/accessible by users
- [ ] Only intentional public API types are exported
- [ ] Moving internal code does not break user code (SemVer compliance)

### REQ-CQ-3: Linting and Formatting

Every SDK must have language-specific linting and formatting enforced in CI.

**Required tools:**

| Language | Linter | Formatter | Type Checker |
|----------|--------|-----------|-------------|
| Go | golangci-lint (v2+) | gofmt (built-in) | Go compiler |
| Java | Error Prone | google-java-format | javac + Error Prone |
| C# | Roslyn analyzers + StyleCop | dotnet format | C# compiler (nullable enabled) |
| Python | ruff | ruff format | mypy (strict mode) |
| JS/TS | ESLint (flat config) | Prettier | TypeScript (strict mode) |

**golangci-lint recommended linters (Go):**
`errcheck`, `govet`, `staticcheck`, `unused`, `ineffassign`, `gosimple`, `gocritic`, `revive`, `misspell`, `gofumpt`, `nolintlint`, `errorlint`, `wrapcheck`, `gosec`

**ruff minimum rule sets (Python):**
Minimum enabled rule sets: E, W, F, I. Additional sets at SDK team's discretion.

**Acceptance criteria:**
- [ ] Linter configuration file exists in repo root
- [ ] CI runs linter and blocks merge on violations
- [ ] Zero linter warnings in the codebase
- [ ] Formatting is enforced (CI check or pre-commit hook)
- [ ] Type checking is enabled at strictest practical level
- [ ] Protobuf-generated code is excluded from linter and coverage configurations. Generated code must not be manually edited

### REQ-CQ-4: Minimal Dependencies

SDK dependencies must be minimized to reduce supply chain risk and version conflicts.

**Acceptable dependencies:**

| Category | Allowed |
|----------|---------|
| gRPC runtime | Required (core transport) |
| Protobuf runtime | Required (message serialization) |
| OTel API | Optional (observability) |
| Everything else | Must be justified |

gRPC runtime, protobuf runtime, and OTel API are excluded from the dependency count. Go `golang.org/x/*` packages count. Any dependency beyond the allowed list requires written justification.

OTel API must be a non-forced dependency (Go: minimum version only, Java: `provided` or `compileOnly` scope, Python: loose lower bound, JS/TS: `peerDependency`, C#: version range). SDKs must test against both the minimum and latest supported versions of gRPC and OTel API.

No dependency injection framework. All wiring via constructors/options with sensible defaults.

**Acceptance criteria:**
- [ ] Total direct dependencies ≤ 5 (excluding gRPC, protobuf, OTel API)
- [ ] No logging framework dependency (use interface + no-op default)
- [ ] No HTTP client dependency (gRPC only)
- [ ] No utility library dependencies (write small helpers inline)
- [ ] Dependencies are pinned to specific versions
- [ ] Dependency tree is reviewed for security vulnerabilities
- [ ] CI runs language-appropriate vulnerability scanning (Go: `govulncheck`, Java: OWASP, Python: `pip-audit`, JS: `npm audit`, C#: `dotnet list package --vulnerable`)

### REQ-CQ-5: Consistent Code Organization

Every SDK must follow a predictable directory structure.

**Go:**
```
kubemq-go/
  client.go          # Public Client type and constructor
  options.go         # Functional options
  errors.go          # Error types and classification
  events.go          # Events API methods
  events_store.go    # Events Store API methods
  queues.go          # Queues API methods
  commands.go        # Commands API methods
  queries.go         # Queries API methods
  internal/
    transport/       # gRPC connection, keepalive, reconnect
    middleware/      # Retry, auth, OTel interceptors
```

**Java:**
```
src/main/java/io/kubemq/sdk/
  client/            # KubeMQClient, ClientOptions, ClientBuilder
  events/            # EventsClient, EventMessage, EventResult
  queues/            # QueuesClient, QueueMessage, QueueResult
  commands/          # CommandsClient
  queries/           # QueriesClient
  error/             # Error types, classification, exceptions
  auth/              # Authentication types, token handling
  transport/         # gRPC connection, channel management
  internal/          # Package-private implementation
```

> **Maven coordinates:** `io.kubemq:kubemq-sdk-java`. Group ID matches the package prefix.

**Python:**
```
src/kubemq/
  __init__.py        # Public API exports (__all__)
  client.py          # Public Client type and constructor
  options.py         # Configuration options
  errors.py          # Error types and classification
  events.py          # Events API methods
  events_store.py    # Events Store API methods
  queues.py          # Queues API methods
  commands.py        # Commands API methods
  queries.py         # Queries API methods
  py.typed           # PEP 561 marker for type checking support
  _internal/
    transport.py     # gRPC connection, keepalive, reconnect
    middleware.py     # Retry, auth, OTel interceptors
```

(Similar idiomatic structures for C#, JS/TS)

> See API naming convention table for cross-language method name mappings.

**Acceptance criteria:**
- [ ] Directory structure follows the language's conventions
- [ ] Each messaging pattern has its own file/module
- [ ] Shared types (errors, auth, transport) are in a common location
- [ ] No circular dependencies between packages/modules
- [ ] File names are consistent (all lowercase, underscores for Go/Python, PascalCase for C#)
- [ ] JS/TS SDKs must publish both CommonJS and ESM builds with correct `exports` field in `package.json`

### REQ-CQ-6: Code Review Standards

**Acceptance criteria:**
- [ ] All PRs require at least one review before merge
- [ ] PRs include tests for new functionality
- [ ] Breaking changes are clearly labeled in PR description
- [ ] No `TODO` or `FIXME` comments in released code (use issue tracker instead)
- [ ] Dead code is removed, not commented out

### REQ-CQ-7: Secure Defaults

SDK defaults must be secure. Security misconfigurations must be visible.

**Requirements:**
- Credentials (tokens, certificates) must never appear in log output, error messages, or OTel span attributes
- Default TLS verification to enabled. Log WARN when `InsecureSkipVerify` is set

**Acceptance criteria:**
- [ ] No credential material appears in any log output at any log level
- [ ] No credential material appears in error messages or OTel span attributes
- [ ] TLS certificate verification is enabled by default
- [ ] Disabling TLS verification produces a WARN-level log message

---

## What 4.0+ Looks Like

- Clean 3-layer architecture — users never see gRPC internals
- Cross-cutting concerns (logging, OTel, config) injected at construction, not scattered
- Connection lifecycle and operation retry are clearly separated between Transport and Protocol layers
- Dependencies flow downward only — no circular references
- Error wrapping flows upward through layers with proper classification at each level
- Zero linter warnings, formatting enforced, strict type checking
- Generated protobuf code excluded from linting and coverage
- ≤5 direct dependencies beyond gRPC/protobuf/OTel, with vulnerability scanning in CI
- OTel API is a non-forced dependency — no version conflicts for users
- No dependency injection frameworks — clean constructor/options wiring
- Predictable directory structure that a new contributor can navigate immediately
- Internal APIs are properly hidden — moving them doesn't break users
- Public API surface is clearly defined for SemVer compliance
- Secure by default — no credential leaks, TLS verification on
- PRs are reviewed, tested, and well-documented
