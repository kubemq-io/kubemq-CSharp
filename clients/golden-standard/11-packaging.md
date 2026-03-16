# Category 11: Packaging & Distribution

**Tier:** 2 (Should-have)
**Current Average Score:** 2.94 / 5.0
**Target Score:** 4.0+
**Weight:** 4%

## Purpose

SDKs must be easy to install from standard package managers, versioned with SemVer, and released via automated pipelines.

---

## Requirements

### REQ-PKG-1: Package Manager Publishing

Every SDK must be published to its language's canonical package manager.

| Language | Registry | Package Name |
|----------|---------|-------------|
| Go | pkg.go.dev (Go Modules) | `github.com/kubemq-io/kubemq-go` (v2+ must use `/v2` module path suffix per Go Modules conventions) |
| Java | Maven Central | `io.kubemq:kubemq-sdk-java` |
| C# | NuGet | `KubeMQ.SDK.csharp` |
| Python | PyPI | `kubemq` |
| JS/TS | npm | `kubemq` or `@kubemq/sdk` |

**Acceptance criteria:**
- [ ] Package is published and installable via single command (`go get`, `mvn`, `dotnet add`, `pip install`, `npm install`)
- [ ] Package includes README, LICENSE, and CHANGELOG
- [ ] Package metadata (description, homepage, repository URL) is complete

### REQ-PKG-2: Semantic Versioning

All SDKs must follow [SemVer 2.0.0](https://semver.org/) strictly after reaching 1.0.0.

**Rules:**
- MAJOR: Breaking API changes
- MINOR: New backward-compatible features
- PATCH: Backward-compatible bug fixes
- Pre-release: `-alpha.1`, `-beta.1`, `-rc.1`

**Acceptance criteria:**
- [ ] Version numbers follow SemVer
- [ ] Breaking changes only occur in MAJOR releases
- [ ] Pre-release versions are clearly labeled
- [ ] Version is embedded in the package (queryable at runtime)

### REQ-PKG-3: Automated Release Pipeline

Releases must be automated via CI/CD. **CI/CD: GitHub Actions.**

**Pipeline steps:**
1. Developer tags a commit (or CI determines version from conventional commits)
2. CI builds the package
3. CI runs full test suite (unit + integration)
4. CI validates CHANGELOG entry exists (or generates from conventional commits if adopted)
5. CI publishes to package registry
6. CI creates GitHub Release with CHANGELOG

**Tools per language:**

| Language | Release Tool |
|----------|-------------|
| Go | GoReleaser or manual tag (Go Modules auto-publish) |
| Java | Maven Central via Sonatype (staging + promotion) |
| C# | `dotnet pack` + `dotnet nuget push` |
| Python | PyPI Trusted Publishing (OIDC, no API keys) |
| JS/TS | npm publish with `--provenance` flag (Trusted Publishing when available) |

**Acceptance criteria:**
- [ ] Release is triggered by a git tag or merge to release branch
- [ ] Publishing requires no manual steps after tagging
- [ ] GitHub Release is created automatically with changelog
- [ ] Failed releases don't publish partial artifacts

### REQ-PKG-4: Conventional Commits (Recommended)

Commit messages SHOULD follow [Conventional Commits](https://www.conventionalcommits.org/) format. SemVer (REQ-PKG-2) is the hard requirement; how the CHANGELOG is generated is an internal choice.

**Format:** `type(scope): description`
- `feat:` — new feature (MINOR bump)
- `fix:` — bug fix (PATCH bump)
- `BREAKING CHANGE:` — breaking change (MAJOR bump)
- `docs:`, `test:`, `chore:`, `refactor:`, `perf:` — no version bump

**Acceptance criteria:**
- [ ] Commit format is documented in CONTRIBUTING.md (if adopted)
- [ ] Commit linting is configured (commitlint or equivalent) (if adopted)
- [ ] CHANGELOG is maintained (manually or generated from conventional commits)

---

## What 4.0+ Looks Like

- `pip install kubemq` / `go get` / `npm install` — just works
- Go module path uses `/v2` suffix for major versions 2+
- SemVer strictly followed — users can trust version numbers
- One-click releases from git tag to published package via GitHub Actions
- CHANGELOG is maintained and validated by CI (manually or auto-generated)
- npm publishes with `--provenance` for supply chain transparency
