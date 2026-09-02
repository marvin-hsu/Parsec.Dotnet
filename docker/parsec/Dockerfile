# syntax=docker/dockerfile:1
#
# Parsec service image for the Parsec.Testcontainers package.
#
# Differences from the upstream quickstart image, and why:
#   - Both source repositories are checked out at a pinned tag, so the image is
#     reproducible. The quickstart image and the Java client's Dockerfile both
#     build from a moving branch.
#   - cargo-chef caches the dependency build in its own layer. Parsec pulls in
#     psa-crypto-sys, which compiles Mbed TLS from source, so a naive build
#     recompiles every dependency whenever the service source changes.
#   - socat is installed. The package uses it to bridge the Unix socket to TCP
#     when the host cannot reach a container's Unix socket, which is every
#     host that is not Linux.
#   - The service is configured for Direct authentication. Unix peer
#     credentials cannot survive the socat bridge, because the service reads
#     the credentials of the relaying process.
#
# Build (native architecture):
#   docker buildx build -t parsec-testcontainers:dev docker/parsec
#
# The feature set deliberately excludes trusted-service-provider. That feature
# needs the trusted-services-vendor submodule, which this shallow checkout does
# not fetch.

ARG RUST_VERSION=1.98.0
ARG UBUNTU_VERSION=24.04
ARG PARSEC_VERSION=1.5.0
ARG PARSEC_TOOL_VERSION=0.7.0
ARG CARGO_CHEF_VERSION=0.1.78

# ---------------------------------------------------------------------------
# chef: toolchain plus the native dependencies Parsec needs.
# clang and libclang are for bindgen; cmake is for building Mbed TLS.
# ---------------------------------------------------------------------------
FROM rust:${RUST_VERSION}-bookworm AS chef
ARG CARGO_CHEF_VERSION
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates \
      clang \
      cmake \
      git \
      libclang-dev \
      llvm-dev \
 && rm -rf /var/lib/apt/lists/*
RUN cargo install cargo-chef --locked --version "${CARGO_CHEF_VERSION}"
WORKDIR /src

# ---------------------------------------------------------------------------
# source: shallow checkout of both repositories at their pinned tags.
# The .git directories are dropped; nothing in either build reads them.
# ---------------------------------------------------------------------------
FROM chef AS source
ARG PARSEC_VERSION
ARG PARSEC_TOOL_VERSION
RUN git clone --depth 1 --branch "${PARSEC_VERSION}" \
      https://github.com/parallaxsecond/parsec.git parsec \
 && git clone --depth 1 --branch "${PARSEC_TOOL_VERSION}" \
      https://github.com/parallaxsecond/parsec-tool.git parsec-tool \
 && rm -rf parsec/.git parsec-tool/.git

# ---------------------------------------------------------------------------
# Dependency recipes. Each crate gets its own recipe, so a change in one does
# not invalidate the other's cached dependency layer.
# ---------------------------------------------------------------------------
FROM chef AS plan-service
COPY --from=source /src/parsec /src/parsec
WORKDIR /src/parsec
RUN cargo chef prepare --recipe-path /recipe.json

FROM chef AS plan-tool
COPY --from=source /src/parsec-tool /src/parsec-tool
WORKDIR /src/parsec-tool
RUN cargo chef prepare --recipe-path /recipe.json

# ---------------------------------------------------------------------------
# build-service: cook dependencies, then build the service itself.
# The feature list must match between cook and build. If it does not, cargo
# resolves a different dependency set and the cached layer is wasted.
# ---------------------------------------------------------------------------
FROM chef AS build-service
ARG PARSEC_FEATURES=mbed-crypto-provider,direct-authenticator,unix-peer-credentials-authenticator
WORKDIR /src/parsec
COPY --from=plan-service /recipe.json recipe.json
RUN cargo chef cook --release --locked \
      --no-default-features \
      --features "${PARSEC_FEATURES}" \
      --recipe-path recipe.json
COPY --from=source /src/parsec .
RUN cargo build --release --locked \
      --no-default-features \
      --features "${PARSEC_FEATURES}" \
 && strip target/release/parsec

FROM chef AS build-tool
WORKDIR /src/parsec-tool
COPY --from=plan-tool /recipe.json recipe.json
RUN cargo chef cook --release --locked --recipe-path recipe.json
COPY --from=source /src/parsec-tool .
RUN cargo build --release --locked \
 && strip target/release/parsec-tool

# ---------------------------------------------------------------------------
# runtime
# ---------------------------------------------------------------------------
FROM ubuntu:${UBUNTU_VERSION} AS runtime
ARG PARSEC_VERSION
ARG PARSEC_TOOL_VERSION

LABEL org.opencontainers.image.title="Parsec service for Parsec.Testcontainers" \
      org.opencontainers.image.description="Parsec service and parsec-tool, configured for integration testing. Not for production use." \
      org.opencontainers.image.source="https://github.com/marvin-hsu/Parsec.Dotnet" \
      org.opencontainers.image.url="https://github.com/marvin-hsu/Parsec.Dotnet" \
      org.opencontainers.image.documentation="https://github.com/marvin-hsu/Parsec.Dotnet/blob/main/docker/parsec/README.md" \
      org.opencontainers.image.licenses="Apache-2.0" \
      org.opencontainers.image.vendor="Parsec.Dotnet contributors"

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates \
      socat \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build-service /src/parsec/target/release/parsec /usr/local/bin/parsec
COPY --from=build-tool /src/parsec-tool/target/release/parsec-tool /usr/local/bin/parsec-tool
COPY config.toml /etc/parsec/config.toml

# The socket directory belongs to the service user, which is the user the
# container runs as. It is deliberately not world-writable: any process in the
# container could otherwise plant a socket there. When a bind mount supplies
# this directory, the host directory's ownership applies instead, and
# Parsec.Testcontainers is what reconciles the user id difference.
RUN useradd --create-home --shell /bin/bash parsec \
 && mkdir -p /run/parsec /var/lib/parsec \
 && chown -R parsec:parsec /run/parsec /var/lib/parsec /etc/parsec \
 && chmod 755 /run/parsec

ENV PARSEC_SERVICE_ENDPOINT=unix:/run/parsec/parsec.sock

USER parsec
WORKDIR /var/lib/parsec

CMD ["parsec", "--config", "/etc/parsec/config.toml"]
