# Parsec.Dotnet

[![CI](https://github.com/marvin-hsu/Parsec.Dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/marvin-hsu/Parsec.Dotnet/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=marvin-hsu_Parsec.Dotnet&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=marvin-hsu_Parsec.Dotnet)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

A .NET client library for [Parsec](https://parsec.community/) — the **P**latform **A**bst**R**action for **SEC**urity, a CNCF project providing a platform-agnostic API to hardware-backed security services (TPM, HSM, PKCS#11, Trusted Applications, …).

> ⚠️ **Status: early development.** The API surface is not yet stable.

## Installation

```bash
dotnet add package Parsec.Client
```

## Quick start

```csharp
// API under design — see docs/ for the roadmap.
```

## Supported frameworks

| Target | Notes |
|---|---|
| `net8.0` | LTS, AOT-compatible |
| `net10.0` | LTS, AOT-compatible |

## Building from source

Requires the .NET SDK pinned in [`global.json`](global.json).

```bash
dotnet build          # warnings are errors; all analyzers on
dotnet test           # xunit v3 on net8.0 + net10.0
dotnet format --verify-no-changes
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All participants are expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Please report vulnerabilities privately — see [SECURITY.md](SECURITY.md). **Do not open public issues for security problems.**

## License

Licensed under the [Apache License 2.0](LICENSE), consistent with the upstream Parsec project.
