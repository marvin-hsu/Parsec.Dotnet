# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial project scaffolding: strict analyzer configuration, CI, and OSS governance documents.
- Compile the parsec-operations protobuf messages (git submodule) into Parsec.Client as internal types.
- DocFX documentation site with English and Traditional Chinese conceptual pages.
- `THIRD-PARTY-NOTICES.txt` crediting parsec-operations, parsec-book, Google.Protobuf and Grpc.Tools; shipped in the NuGet package.
- `Parsec.Testcontainers`, a Testcontainers module that starts a Parsec service for an
  integration test. `ParsecBuilder` builds the container, `ParsecContainer` exposes
  `SocketPath`, `Endpoint`, `PingAsync` and `ExecParsecToolAsync`, and `WithAuthType`,
  `WithLogLevel`, `WithSocketDirectory` and `WithConfigFile` shape the service.
- A Parsec service image published as `ghcr.io/marvin-hsu/parsec-testcontainers`, built for
  `linux/amd64` and `linux/arm64` from pinned upstream tags, and pinned by digest in the
  package. It carries the software Mbed Crypto provider, Direct authentication and socat.
- Two ways for a test to reach the service. A Linux host mounts the socket directory
  straight into the container. Every other host bridges the socket over TCP, so no tool
  needs installing.
- Integration tests carry the `Category=IntegrationTests` trait, skip themselves when Docker
  does not answer, and run through `just test-integration`. `just test-unit` runs the rest.
- Versions come from git tags through MinVer, with a tag prefix per package so the two
  packages release on their own cadence.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` fails the build on a public member that
  `PublicAPI.Unshipped.txt` does not declare.

### Changed

- The solution is `Parsec.slnx`. The repository keeps the platform in its name so GitHub can
  tell this implementation apart from the Rust, Go and Java clients; the solution and the
  packages do not repeat it.
