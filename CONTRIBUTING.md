# Contributing to KubeMQ .NET SDK

Thank you for your interest in contributing! This document provides guidelines for
contributing to the KubeMQ .NET SDK.

## Prerequisites

- .NET 8.0 SDK or later
- Docker (for running KubeMQ server during integration tests)
- Git

## Building

```bash
git clone https://github.com/kubemq-io/kubemq-CSharp.git
cd kubemq-CSharp
dotnet build src/KubeMQ.Sdk/KubeMQ.Sdk.csproj
```

## Running Tests

### Unit tests (no server required):

```bash
dotnet test tests/KubeMQ.Sdk.Tests.Unit/KubeMQ.Sdk.Tests.Unit.csproj
```

### Integration tests (requires KubeMQ server):

```bash
docker run -d -p 50000:50000 kubemq/kubemq-community:latest
dotnet test tests/KubeMQ.Sdk.Tests.Integration/KubeMQ.Sdk.Tests.Integration.csproj
```

## Code Style

- Follow the [.editorconfig](.editorconfig) rules
- All public APIs must have XML doc comments (`/// <summary>`)
- Use `ConfigureAwait(false)` on all `await` calls in library code
- All async methods must accept `CancellationToken` as the last parameter
- Use `PascalCase` for public members, `_camelCase` for private fields

## Pull Request Process

1. Create a feature branch from `main`
2. Make your changes with clear, descriptive commits
3. Ensure all tests pass: `dotnet test`
4. Ensure examples compile: `dotnet build examples/KubeMQ.Sdk.Examples.sln`
5. Add a CHANGELOG entry under `[Unreleased]`
6. Submit a pull request with a description of the changes

## Reporting Issues

Use [GitHub Issues](https://github.com/kubemq-io/kubemq-CSharp/issues) to report bugs.
Include:
- SDK version (`dotnet list package | grep KubeMQ`)
- .NET version (`dotnet --version`)
- KubeMQ server version
- Minimal reproduction code
- Expected vs actual behavior

## License

By contributing, you agree that your contributions will be licensed under the
Apache License 2.0.
