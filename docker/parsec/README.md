# Parsec service image

This directory builds the container image that the `Parsec.Testcontainers`
package starts for integration tests. The published image is
`ghcr.io/marvin-hsu/parsec-testcontainers`.

> [!WARNING]
> This image is for integration testing only. It uses the Direct
> authenticator, which trusts the application identity the client sends, and it
> declares an admin account. Never run it in production.

## What is inside

| Component | Version | Notes |
|---|---|---|
| parsec | 1.5.0 | Built from the upstream tag |
| parsec-tool | 0.7.0 | Built from the upstream tag |
| Providers | MbedCrypto | Software only, no hardware needed |
| Authenticators | Direct, Unix peer credentials | Config selects Direct |
| Key info manager | SQLite | Database at `/var/lib/parsec/kim.sqlite3` |
| socat | From Ubuntu | Bridges the Unix socket to TCP for non-Linux hosts |

The socket lives at `/run/parsec/parsec.sock`, and
`PARSEC_SERVICE_ENDPOINT` is set to match. The service runs as the `parsec`
user. The socket directory is mode 1777, so the service can create its socket
even when the runtime starts the process as a different user.

Excluded on purpose: `trusted-service-provider` needs the
`trusted-services-vendor` submodule, which the shallow checkout does not fetch.
`pkcs11-provider` and `tpm-provider` need hardware or SoftHSM, which is
[tracked as a later step](../../plan/implementation-plan.md).

## Build

```bash
just image-build          # native architecture, tagged parsec-testcontainers:dev
just image-test           # start it and run parsec-tool ping and list-providers
```

Or without just:

```bash
docker buildx build -t parsec-testcontainers:dev docker/parsec
```

The first build compiles Parsec and Mbed TLS from source, which takes a while.
`cargo-chef` puts the dependency build in its own layer, so a later change to
the pinned service version rebuilds only what changed.

Build a specific version:

```bash
docker buildx build \
  --build-arg PARSEC_VERSION=1.4.0 \
  --build-arg PARSEC_TOOL_VERSION=0.7.0 \
  -t parsec-testcontainers:1.4.0 \
  docker/parsec
```

## Run by hand

```bash
docker run --rm -d --name parsec parsec-testcontainers:dev
docker exec parsec parsec-tool ping
docker exec parsec parsec-tool list-providers
docker rm -f parsec
```

## Publishing

`.github/workflows/image.yml` builds `linux/amd64` on `ubuntu-latest` and
`linux/arm64` on `ubuntu-24.04-arm`, then joins them into one manifest. Native
runners are used on purpose: cross-compiling Rust under QEMU emulation is far
too slow.

Two things need doing by hand, once:

1. Set the `parsec-testcontainers` package visibility to public. GitHub
   packages default to private, and that setting is separate from the
   repository's.
2. Copy the published manifest digest into `ParsecImage.Digest` in the
   `Parsec.Testcontainers` source, then release a package version.
