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
- `Parsec.Client`, a client for the Parsec wire protocol. `ParsecClient.CreateAsync` finds the
  service, agrees a protocol version and binds to a provider, so a service that is absent or
  unusable fails where the application can still act on it. The operations sit under `Keys`,
  `Crypto` and `Attestation`, and `IParsecClient` carries the ones that belong to the service
  as a whole.
- Every operation the protocol defines: key management, signing and verifying a hash and a
  message, hashing, random, asymmetric encryption, authenticated encryption, symmetric ciphers,
  message authentication codes, raw key agreement and key attestation.
- A hand-written model of the PSA key attributes and algorithms, closed and validated in both
  directions. Private constructors make a combination the specification does not define
  unrepresentable rather than merely wrong, and four factories on `KeyAttributes` cover the
  shapes most callers want. Export permission is off unless a caller asks for it.
- An exception hierarchy that says where a failure happened: configuration, transport, protocol
  or the service. Verification and the capability check answer `false` rather than raising,
  because a signature that does not match is the answer to the question and a caller forced to
  catch an exception to learn it will eventually catch one that means something else.
- Four authentication strategies: none, direct, Unix peer credentials and JWT-SVID.
- `Parsec.Client.DependencyInjection`, which registers an `IParsecClientFactory` with
  `Microsoft.Extensions.DependencyInjection`. It is a package of its own so that
  `Parsec.Client` keeps the protobuf runtime as its only dependency. Registration touches no
  network, the client connects on first use and is shared afterwards, and a connect that failed
  is not remembered.
- Conceptual documentation in English and Traditional Chinese: getting started, authentication
  and application identity, the error model, the wire protocol, testing against a real service
  and security notes. The quick start of the README is run against a real service by a test, so
  it cannot go stale unnoticed. CI builds the site with warnings as errors.

### Changed

- The solution is `Parsec.slnx`. The repository keeps the platform in its name so GitHub can
  tell this implementation apart from the Rust, Go and Java clients; the solution and the
  packages do not repeat it.
