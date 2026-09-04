# Testing against a real service

`Parsec.Testcontainers` starts a real Parsec service in a container and hands your test a
socket to talk to. Nothing is faked: the bytes your client sends are read by the same service
your application will meet.

## Install

```bash
dotnet add package Parsec.Testcontainers
```

The module needs a Docker endpoint. It pulls
`ghcr.io/marvin-hsu/parsec-testcontainers`, pinned by digest, built for `linux/amd64` and
`linux/arm64` from pinned upstream tags of the service and `parsec-tool`.

> [!WARNING]
> The image exists for tests. It runs the software Mbed Crypto provider with Direct
> authentication and no hardware behind it. It is not fit for production.

## Start one

```csharp
await using var container = new ParsecBuilder().Build();

await container.StartAsync();

await using var client = await ParsecClient.CreateAsync(new ParsecClientOptions
{
    Endpoint = container.Endpoint,
    Authentication = new DirectAuthentication("my-tests"),
});
```

<xref:Parsec.Testcontainers.ParsecContainer.Endpoint> is a `unix:` URI pointing at a socket on
the machine running the tests. <xref:Parsec.Testcontainers.ParsecContainer.SocketPath> is the
same path without the scheme, for anything that wants a path.

## How the socket reaches you

On Linux the module mounts a directory into the container and the service listens on a socket
inside it, so your test talks to the same file the service created.

Everywhere else it cannot. A Unix socket in a container is not reachable through a bind mount
from macOS or Windows, and this was measured rather than assumed: a host connect to such a
socket is refused. So on those hosts the module runs `socat` inside the container to expose the
socket over TCP, maps the port, and runs a small relay on the host that listens on a Unix
socket and forwards to it.

Two consequences worth knowing:

- The service sees the credentials of the relay, not of your test process, so
  <xref:Parsec.Client.Authentication.UnixPeerCredentialsAuthentication> cannot be tested this
  way. Use Direct, or run the test on Linux.
- <xref:Parsec.Testcontainers.ParsecBuilder.WithSocketDirectory*> only applies on the direct
  path. On a bridged host the module chooses the path.

## Shaping the service

```csharp
var container = new ParsecBuilder()
    .WithAuthType(ParsecAuthType.UnixPeerCredentials)
    .WithLogLevel(ParsecLogLevel.Debug)
    .Build();
```

<xref:Parsec.Testcontainers.ParsecBuilder.WithAuthType*> and
<xref:Parsec.Testcontainers.ParsecBuilder.WithLogLevel*> rewrite the configuration the service
starts with. For anything they do not cover,
<xref:Parsec.Testcontainers.ParsecBuilder.WithConfigFile*> replaces the file outright, and it
works whether or not you also supplied your own image.

## Running the operations that need Docker separately

The tests in this repository carry a trait, so the two lanes can be run apart:

```bash
just test-unit          # everything that needs no Docker
just test-integration   # everything that does
```

An integration test that finds no Docker endpoint skips rather than fails, so a contributor
without Docker still gets a green run.

## What the test image can and cannot do

Ask, rather than assume:

```csharp
var supported = await client.ListOpcodesAsync(client.Provider);
```

The software provider runs sixteen operations: key management, signing and verifying a hash,
hashing, random, asymmetric encryption, authenticated encryption, raw key agreement and the
capability check. It has no cipher operation and no message-signing operation, and it cannot
attest a key.

The two absences do not fail the same way, and the difference matters when you are deciding
whether another provider would help. A cipher request reaches the provider and comes back with
`PsaErrorNotSupported`. A code request never gets that far: the service answers
`OpcodeDoesNotExist` before any provider is asked.
