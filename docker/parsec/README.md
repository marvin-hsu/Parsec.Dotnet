# Parsec service image

```bash
docker pull ghcr.io/marvin-hsu/parsec-testcontainers
```

A Parsec service that starts with no configuration and no hardware, for
integration testing .NET code against a real service. The
[`Parsec.Testcontainers`](https://www.nuget.org/packages/Parsec.Testcontainers)
package starts it for you; run it by hand only to explore or debug.

> [!WARNING]
> Never run this image in production. It authenticates with the Direct
> authenticator, which trusts whatever application identity a client claims,
> and it declares an admin account. Both are conveniences that make it unsafe
> outside a test.

## What is inside

| Component | Version | Notes |
|---|---|---|
| Parsec service | 1.5.0 | Built from the upstream tag |
| `parsec-tool` | 0.7.0 | Built from the upstream tag |
| Provider | Mbed Crypto | Software only, no TPM or HSM needed |
| Authenticator | Direct | Unix peer credentials is compiled in but unused |
| Key info manager | SQLite | `/var/lib/parsec/kim.sqlite3` |
| `socat` | From Ubuntu | Bridges the Unix socket to TCP |

Architectures: `linux/amd64` and `linux/arm64`, both built natively.

| Path or variable | Value |
|---|---|
| Socket | `/run/parsec/parsec.sock` |
| `PARSEC_SERVICE_ENDPOINT` | `unix:/run/parsec/parsec.sock` |
| Configuration | `/etc/parsec/config.toml` |
| Service user | `parsec` |
| Admin application names | `parsec-tool`, `admin` |

`parsec-tool` is an admin because it hardcodes its application name to its own
crate name. Version 0.7.0 offers no flag and reads no environment variable to
change it, so no other value lets the bundled tool run admin operations.

## Try it

```bash
docker run --rm -d --name parsec ghcr.io/marvin-hsu/parsec-testcontainers
docker exec parsec parsec-tool ping
docker exec parsec parsec-tool list-providers
docker exec parsec parsec-tool create-rsa-key --key-name demo --for-signing
docker rm -f parsec
```

## Reaching the service from the host

The service listens on a Unix socket, and only that. Whether the host can
reach it depends on the host.

On Linux, bind mount the socket directory and connect to it directly:

```bash
mkdir -p /tmp/parsec && chmod 777 /tmp/parsec
docker run --rm -d --name parsec \
  -v /tmp/parsec:/run/parsec \
  ghcr.io/marvin-hsu/parsec-testcontainers
# /tmp/parsec/parsec.sock is now connectable
```

On macOS and Windows that does not work. The socket file appears on the host,
but connecting to it fails with `Connection refused`, because the listening
endpoint lives in the container's namespace. Bridge it to TCP instead:

```bash
docker run --rm -d --name parsec -p 5000:5000 \
  ghcr.io/marvin-hsu/parsec-testcontainers
docker exec -d parsec \
  socat TCP-LISTEN:5000,fork,reuseaddr UNIX-CONNECT:/run/parsec/parsec.sock
# the service now answers on localhost:5000
```

`Parsec.Testcontainers` does this for you, and adds a host-side relay so your
code still talks to a Unix socket.

> [!NOTE]
> Unix peer credentials authentication cannot work through the bridge. The
> service reads the credentials of the relaying process, not of your client.
> This is why the image is configured for Direct authentication.

## Changing the configuration

Replace the file the service reads:

```bash
docker run --rm -d --name parsec \
  -v ./my-config.toml:/etc/parsec/config.toml:ro \
  ghcr.io/marvin-hsu/parsec-testcontainers
```

The schema follows Parsec 1.5.0. Start from
[`config.toml`](config.toml) in this directory.

## Excluded on purpose

`trusted-service-provider` needs the `trusted-services-vendor` submodule,
which the shallow source checkout does not fetch. `pkcs11-provider` and
`tpm-provider` need hardware or SoftHSM.

---

## Building and publishing

This part is for maintainers of this repository.

```bash
just image-build   # native architecture, tagged parsec-testcontainers:dev
just image-test    # start it and check the service answers
```

The first build compiles Parsec and Mbed TLS from source, which takes a while.
`cargo-chef` puts the dependency build in its own layer, so changing the pinned
service version rebuilds only what changed.

Build a different version:

```bash
docker buildx build \
  --build-arg PARSEC_VERSION=1.4.0 \
  --build-arg PARSEC_TOOL_VERSION=0.7.0 \
  -t parsec-testcontainers:1.4.0 \
  docker/parsec
```

`.github/workflows/image.yml` builds `linux/amd64` on `ubuntu-latest` and
`linux/arm64` on `ubuntu-24.04-arm`, then joins them into one manifest. Native
runners are deliberate: cross-building Rust under QEMU emulation takes hours.

Every run publishes two tags:

| Tag | Meaning |
|---|---|
| `sha-<short sha>` | Immutable, points at one commit |
| `0.1.0-alpha.<run number>` | Numbered preview |

### Promoting a build

The workflow does not publish `latest` or a release version on its own. An
image nobody has pulled and started is not known to work, and a release tag on
such an image misleads everyone who reads it.

Promote only after a build passes this check on a machine that has never built
the image:

```bash
docker pull ghcr.io/marvin-hsu/parsec-testcontainers:0.1.0-alpha.<n>
docker run --rm -d --name parsec-verify \
  ghcr.io/marvin-hsu/parsec-testcontainers:0.1.0-alpha.<n>
docker exec parsec-verify parsec-tool ping
docker rm -f parsec-verify
```

Then run the workflow from the Actions tab with the `promote` input checked.
That run adds the release version and `latest` to the same digests.

Afterwards, copy the manifest digest into `ParsecImage.Digest` in the
`Parsec.Testcontainers` source and release a package version. The workflow
prints the digest in its run summary.
