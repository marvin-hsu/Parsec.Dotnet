# Parsec.Dotnet

[![CI](https://github.com/marvin-hsu/Parsec.Dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/marvin-hsu/Parsec.Dotnet/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=marvin-hsu_Parsec.Dotnet&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=marvin-hsu_Parsec.Dotnet)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

A .NET client library for [Parsec](https://parsec.community/) — the **P**latform **A**bst**R**action for **SEC**urity, a CNCF project providing a platform-agnostic API to hardware-backed security services (TPM, HSM, PKCS#11, Trusted Applications, …).

> ⚠️ **Status: early development.** The API surface is not yet stable.

## Installation

```bash
dotnet add package Parsec.Client
dotnet add package Parsec.Testcontainers   # for integration tests
```

## Quick start

```csharp
// API under design — see docs/ for the roadmap.
```

## Testing against a real service

`Parsec.Testcontainers` starts a Parsec service in a container for integration
tests. The image it uses is published from this repository:

```bash
docker pull ghcr.io/marvin-hsu/parsec-testcontainers
```

That image bundles the Parsec service and `parsec-tool`, configured with the
software Mbed Crypto provider and Direct authentication. It exists for tests
and is not fit for production. See
[docker/parsec/README.md](docker/parsec/README.md) for what is inside it and
how it is built.

## Supported frameworks

| Target | Notes |
|---|---|
| `net8.0` | LTS |
| `net10.0` | LTS |

Both packages target both frameworks. `Parsec.Client` is AOT-compatible.
`Parsec.Testcontainers` is not AOT-compatible. Its Docker client serialises
with reflection.

## Building from source

Requires the .NET SDK pinned in [`global.json`](global.json). The wire-protocol `.proto` files come from the upstream [parsec-operations](https://github.com/parallaxsecond/parsec-operations) repository as a git submodule, so clone with submodules:

```bash
git clone --recurse-submodules https://github.com/marvin-hsu/Parsec.Dotnet.git
# or, in an existing clone:
git submodule update --init
```

```bash
dotnet build          # warnings are errors; all analyzers on; protoc runs here (Grpc.Tools)
dotnet test           # xunit v3 on net8.0 + net10.0
dotnet format --verify-no-changes
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). All participants are expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Please report vulnerabilities privately — see [SECURITY.md](SECURITY.md). **Do not open public issues for security problems.**

## License

Licensed under the [Apache License 2.0](LICENSE), consistent with the upstream Parsec project.

The wire-protocol definitions come from [parsec-operations](https://github.com/parallaxsecond/parsec-operations) and parts of the documentation are adapted from the [Parsec book](https://github.com/parallaxsecond/parsec-book), both Apache-2.0. Attribution and the licenses of all third-party material are listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), which also ships inside the NuGet package.
