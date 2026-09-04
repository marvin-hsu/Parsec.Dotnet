# Parsec.Dotnet

[![CI](https://github.com/marvin-hsu/Parsec.Dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/marvin-hsu/Parsec.Dotnet/actions/workflows/ci.yml)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=marvin-hsu_Parsec.Dotnet&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=marvin-hsu_Parsec.Dotnet)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

A .NET client library for [Parsec](https://parsec.community/) — the **P**latform **A**bst**R**action for **SEC**urity, a CNCF project providing a platform-agnostic API to hardware-backed security services (TPM, HSM, PKCS#11, Trusted Applications, …).

> ⚠️ **Status: early development.** The API surface is not yet stable.

> This is a community project. It is not published by the Parsec maintainers, and the
> package ids are held in good faith: if the Parsec project ever wants them for an official
> .NET client, they can have them.

## Installation

```bash
dotnet add package Parsec.Client
dotnet add package Parsec.Client.DependencyInjection   # for Microsoft.Extensions hosts
dotnet add package Parsec.Testcontainers               # for integration tests
```

`Parsec.Client` depends on the protobuf runtime and nothing else. The other two
are optional and carry their own dependencies, so an application that wants
neither is not made to bring them along.

## Quick start

```csharp
using Parsec.Client;
using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Keys;

// The client finds the service, agrees a protocol version and picks a provider.
await using var client = await ParsecClient.CreateAsync(new ParsecClientOptions
{
    Authentication = new DirectAuthentication("my-application"),
});

// Create a signing key. The private half never leaves the service.
var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

await client.Keys.GenerateKeyAsync("my-key", KeyAttributes.RsaSigningKey(algorithm: algorithm));

// Sign a hash and check the signature.
var digest = await client.Crypto.HashComputeAsync(Hash.Sha256, "sign me"u8.ToArray());
var signature = await client.Crypto.SignHashAsync("my-key", algorithm, digest);

var ok = await client.Crypto.VerifyHashAsync("my-key", algorithm, digest, signature);
```

`ParsecClientOptions` reads `PARSEC_SERVICE_ENDPOINT` when no `Endpoint` is
given, and binds to the first provider that is not the core one unless
`Provider` names another. The default authentication identifies nobody, which
is enough to ask the service what it can do and not enough to own a key.

This sample is compiled and run against a real service by
`TheQuickStartOfTheReadmeRuns` in `tests/Parsec.Client.Tests`, so it cannot
drift out of date without a test failing.

## With Microsoft.Extensions.DependencyInjection

```csharp
builder.Services.AddParsecClient(new ParsecClientOptions
{
    Authentication = new DirectAuthentication("my-application"),
});
```

Building a client asks the service for its protocol version and its providers,
and a service collection cannot await, so what gets registered is an
`IParsecClientFactory` rather than the client itself:

```csharp
public sealed class Signer(IParsecClientFactory factory)
{
    public async Task<byte[]> SignAsync(byte[] digest, CancellationToken cancellationToken)
    {
        var client = await factory.GetAsync(cancellationToken);

        return await client.Crypto.SignHashAsync(
            "my-key",
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256),
            digest,
            cancellationToken);
    }
}
```

The client connects on the first call and is shared afterwards. Registration
touches no network, so an application still starts when the service is briefly
down, and a connect that fails is not remembered.

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
