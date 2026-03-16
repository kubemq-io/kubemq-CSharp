# Category 12: Compatibility, Lifecycle & Supply Chain

**Tier:** 2 (Should-have)
**Current Average Score:** 1.62 / 5.0
**Target Score:** 4.0+
**Weight:** 4%

## Purpose

Users must know which SDK versions work with which server versions, when APIs will be removed, and that dependencies are secure.

---

## Requirements

### REQ-COMPAT-1: Client-Server Compatibility Matrix

A published matrix showing which SDK versions are compatible with which KubeMQ server versions.

**Format:**

| SDK Version | Server ≥1.0 | Server ≥2.0 | Server ≥3.0 | Notes |
|-------------|-------------|-------------|-------------|-------|
| v1.x | ✅ | ✅ | ✅ | |
| v2.x | ❌ | ✅ | ✅ | v2 requires server ≥2.0 |
| v3.x | ❌ | ❌ | ✅ | New queue stream API |

**Acceptance criteria:**
- [ ] Compatibility matrix is maintained in the SDK repo or central docs
- [ ] Matrix is updated when SDK or server versions add features
- [ ] SDK validates server version on connection and warns if potentially incompatible
- [ ] SDK logs a warning if server version is outside the tested compatibility range. Connection proceeds normally.

### REQ-COMPAT-2: Deprecation Policy

APIs must be deprecated before removal, with minimum notice.

**Policy:**
- Deprecated APIs must be annotated with language-appropriate markers
- Minimum 2 minor versions or 6 months before removal (whichever is longer). Note: the 6-month floor is the effective guarantee for planning purposes.
- Deprecation notices must name the replacement API
- Deprecated APIs continue to function until removal

**Annotations per language:**

| Language | Annotation |
|----------|-----------|
| Go | `// Deprecated: Use X instead.` (recognized by go vet) |
| Java | `@Deprecated(since="2.0", forRemoval=true)` + `@deprecated` Javadoc |
| C# | `[Obsolete("Use X instead")]` |
| Python | `warnings.warn("Use X instead", DeprecationWarning)` |
| JS/TS | `@deprecated Use X instead` TSDoc tag |

**Acceptance criteria:**
- [ ] Deprecated APIs have language-appropriate annotations
- [ ] Deprecation notice includes the replacement API name
- [ ] CHANGELOG entries document deprecations
- [ ] Removed APIs are listed in migration guides

### REQ-COMPAT-3: Language Version Support

Each SDK must support the 2-3 most recent stable versions of its language runtime.

| Language | Minimum Version | CI Matrix |
|----------|----------------|-----------|
| Go | Latest 2 releases (e.g., 1.23, 1.24) | 2 versions |
| Java | LTS releases (11, 17, 21) | 3 versions |
| C# | .NET 8 (LTS), .NET 9 (current) | 2 versions |
| Python | Latest 3 releases (e.g., 3.11, 3.12, 3.13) | 3 versions |
| JS/TS | Active LTS + Current (e.g., 18, 20, 22) | 3 versions |

> **Note:** Language version matrix is reviewed annually when new LTS versions are released.

**Acceptance criteria:**
- [ ] Minimum language version is documented in README
- [ ] CI tests against the specified version matrix
- [ ] Dropping support for a language version is treated as a breaking change (MAJOR bump)

### REQ-COMPAT-4: Supply Chain Security

**Acceptance criteria:**
- [ ] Dependencies are scanned for vulnerabilities (Dependabot, Renovate, or equivalent)
- [ ] SBOM (Software Bill of Materials) SHOULD be generated in CycloneDX or SPDX format on release (recommended, not required)
- [ ] Direct dependencies are audited and justified
- [ ] No dependencies with known critical vulnerabilities at release time

### REQ-COMPAT-5: End-of-Life Policy

When a new major SDK version reaches GA, the previous major version has a defined support window.

**Policy:**
- When a new major SDK version reaches GA, the previous major version receives security patches for 12 months
- After that, it is end-of-life
- EOL status is documented in the SDK README

**Acceptance criteria:**
- [ ] EOL policy is documented in the SDK README
- [ ] Previous major versions receive security patches for 12 months after next major GA
- [ ] EOL status is clearly marked in the SDK repository

---

## What 4.0+ Looks Like

- Users can check one matrix to know if their SDK version works with their server
- SDK warns (but does not fail) when server version is outside tested compatibility range
- Deprecated APIs have clear replacement guidance and at least 6 months notice
- CI tests across multiple language runtime versions, reviewed annually
- Dependencies are scanned via Dependabot/Renovate; SBOMs generated when practical
- Clear EOL policy: previous major version gets 12 months of security patches after next major GA
