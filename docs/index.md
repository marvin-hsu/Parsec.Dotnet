# Parsec.Dotnet

Parsec.Dotnet is a .NET client library for [Parsec](https://parsec.community/).

Parsec is the Platform AbstRaction for SECurity. It is a CNCF project. Parsec gives an
application one API for hardware-backed security services. Those services include TPM, HSM,
PKCS#11, and Trusted Applications.

This library talks to a running Parsec service over an IPC transport. Your application does
not link against a hardware driver.

## Status

The public API is under development. The only published type is
<xref:Parsec.Client.IParsecClient>.

The wire-protocol messages come from the upstream
[parsec-operations](https://github.com/parallaxsecond/parsec-operations) repository. A build
step generates C# from those `.proto` files. The generated types are internal. The public API
is hand-written.

## Supported frameworks

| Target | Notes |
|---|---|
| `net8.0` | LTS, AOT-compatible |
| `net10.0` | LTS, AOT-compatible |

## Documentation layout

This site has two locales. English is the root locale. Traditional Chinese lives under
`zh-tw/`. Conceptual pages exist in both locales.

The [API reference](api/index.md) is generated from the XML documentation comments in the source. It
is available in English only.

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
