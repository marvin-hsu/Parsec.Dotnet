# Parsec.Dotnet

Parsec.Dotnet is a .NET client library for [Parsec](https://parsec.community/).

Parsec is the Platform AbstRaction for SECurity. It is a CNCF project. Parsec gives an
application one API for hardware-backed security services. Those services include TPM, HSM,
PKCS#11, and Trusted Applications.

This library talks to a running Parsec service over an IPC transport. Your application does
not link against a hardware driver.

## Packages

| Package | Holds | Depends on |
|---|---|---|
| `Parsec.Client` | The client. <xref:Parsec.Client.ParsecClient> builds one; <xref:Parsec.Client.IParsecClient> is what you hold. | The protobuf runtime, and nothing else |
| `Parsec.Client.DependencyInjection` | `AddParsecClient` for applications built on `Microsoft.Extensions` | The client, plus the DI abstractions |
| `Parsec.Testcontainers` | A Testcontainers module that starts a real service for tests | Testcontainers |

The two optional packages are separate so that an application wanting neither is not made to
carry them.

The wire-protocol messages come from the upstream
[parsec-operations](https://github.com/parallaxsecond/parsec-operations) repository. A build
step generates C# from those `.proto` files. The generated types are internal. The public API
is hand-written.

Nothing has been released yet. The public surface is complete for the operations the protocol
defines, and it is still free to change.

## Where to start

- [Getting started](getting-started.md) — from an empty project to a signature
- [Authentication and application identity](authentication.md) — who the service thinks you are
- [The error model](error-model.md) — what each exception means and what answers instead
- [The wire protocol](wire-protocol.md) — what travels over the socket
- [Testing against a real service](testing.md) — a container instead of a fake
- [Security notes](security.md) — the limits of what a client can promise

## Supported frameworks

| Target | Notes |
|---|---|
| `net8.0` | LTS |
| `net10.0` | LTS |

All three packages target both frameworks. `Parsec.Client` and
`Parsec.Client.DependencyInjection` are AOT-compatible: a native publish of both was measured
at zero trim and AOT warnings, and the resulting binary runs.
`Parsec.Testcontainers` is not AOT-compatible, because its Docker client serialises with
reflection. That is a test-time dependency, so it does not affect an application that ships
natively.

## Documentation layout

This site has two locales. English is the root locale. Traditional Chinese lives under
`zh-tw/`. Conceptual pages exist in both locales.

The [API reference](api/index.md) is generated from the XML documentation comments in the
source. It covers all three packages and is available in English only.

## Build from source

The `.proto` files come from a git submodule. Clone the repository with its submodules.

```bash
git clone --recurse-submodules https://github.com/marvin-hsu/Parsec.Dotnet.git
```

If you already have a clone, initialise the submodule.

```bash
git submodule update --init
```

> [!NOTE]
> If the submodule is missing, the build stops with a message that names this command.

Build and test the solution.

```bash
dotnet build
dotnet test
```

## Build this site

Both locales build from the `docs` folder.

```bash
dotnet docfx docs/docfx.json
dotnet docfx docs/docfx.zh-tw.json
```

The output goes to `artifacts/docs`.
