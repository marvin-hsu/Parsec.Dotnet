# Parsec.Testcontainers

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://github.com/marvin-hsu/Parsec.Dotnet/blob/main/LICENSE)

A [Testcontainers for .NET](https://dotnet.testcontainers.org/) module that starts a
[Parsec](https://parsec.community/) service in a container. Parsec is the Platform
AbstRaction for SECurity, a CNCF project that gives a platform-agnostic API to
hardware-backed security services. This module gives your integration tests a real
service to talk to.

> **Status: early development.** The API surface is not yet stable.

> This is a community project. It is not published by the Parsec maintainers.

## Installation

```bash
dotnet add package Parsec.Testcontainers
```

## Quick start

```csharp
await using var parsec = new ParsecBuilder().Build();
await parsec.StartAsync();

// The socket a client on this machine connects to.
Console.WriteLine(parsec.SocketPath);
Console.WriteLine(parsec.Endpoint);   // unix:/tmp/parsec-ab12cd34/parsec.sock

// Ask the service to answer. Throws if it does not.
await parsec.PingAsync();
```

`StartAsync` returns after the service answers a ping, so the socket is ready to use
when the call completes. `DisposeAsync` stops the container and removes the socket
directory it made on the host.

### Settings

```csharp
await using var parsec = new ParsecBuilder()
    .WithLogLevel(ParsecLogLevel.Debug)
    .WithAuthType(ParsecAuthType.Direct)
    .Build();
```

The module writes a new service configuration only when one of these settings differs
from what the image already selects.

### A configuration of your own

`WithConfigFile` hands the service a file you wrote, in place of the one in the image.
Reach for it when you need a setting the `With` methods do not offer, such as an
administrator list, another key info manager, or another provider.

```csharp
await using var parsec = new ParsecBuilder()
    .WithConfigFile("test-parsec-config.toml")
    .Build();
```

The file replaces the whole configuration, so it has to be complete, and its schema
follows the Parsec release in the image. A provider only works when the service in the
image was built with it, so a file that names another provider usually needs another
image as well.

Your file decides where the socket goes. When that is not the directory of the image,
tell the module the same directory with `WithSocketDirectory`: the module cannot read
your file to find out.

`WithAuthType` and `WithLogLevel` write into a file this build no longer produces, so
combining them with `WithConfigFile` throws rather than dropping the setting quietly.

### Running parsec-tool

```csharp
var result = await parsec.ExecParsecToolAsync("list-providers");
Console.WriteLine(result.Stdout);
```

## The container image

The module pins the image `ghcr.io/marvin-hsu/parsec-testcontainers`, by digest, and the
constants are on `ParsecImage`. The image holds the Parsec service and `parsec-tool`,
with the software Mbed Crypto provider and Direct authentication. It is built for
`linux/amd64` and `linux/arm64`.

The image is public on the GitHub Container Registry and anonymous pulls work. Your
test machine, or your CI runner, must be able to reach `ghcr.io` without credentials.
Behind a proxy or an air-gapped network, mirror the image and point the builder at your
copy with `WithImage`.

The image exists for tests. Do not use it in production. It keeps its keys in a SQLite
database inside the container, and the container has no persistence.

## How the socket reaches your test

Parsec listens on a Unix socket. On Linux the module bind-mounts a host directory into
the container, so the socket your test opens is the service socket, with nothing in
between. On macOS and Windows a host process cannot connect to a socket a container made
in a bind mount, so the module bridges instead: `socat` in the container publishes the
socket on a TCP port, and an in-process relay on the host serves a Unix socket that
forwards to that port. `SocketPath` is a path on your machine in both cases.

## Supported frameworks

`net8.0` and `net10.0`. This package is not AOT-compatible: its Docker client serialises
with reflection.

## Links

- [Repository and documentation](https://github.com/marvin-hsu/Parsec.Dotnet)
- [Parsec project](https://parsec.community/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
