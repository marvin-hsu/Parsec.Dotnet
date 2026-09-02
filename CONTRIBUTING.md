# Contributing to Parsec.Dotnet

Thank you for your interest in contributing! This project follows a strict quality bar; please read this document before opening a pull request.

## Ground rules

- All participants must follow the [Code of Conduct](CODE_OF_CONDUCT.md).
- Security issues go through [SECURITY.md](SECURITY.md), never public issues.
- Every change lands through a pull request — no direct pushes to `main`.

## Developer Certificate of Origin (DCO)

All commits must be signed off (`git commit -s`), certifying the [Developer Certificate of Origin](https://developercertificate.org/). Unsigned commits are rejected by the local `commit-msg` hook.

## Development workflow

1. Fork and create a topic branch from `main`.
2. Install the .NET SDK version pinned in `global.json` and run `git submodule update --init` (the `.proto` files live in the `external/parsec-operations` submodule).
3. Make your change, including tests. Coverage must not decrease.
4. Verify locally (the `pre-push` hook runs build + test):

   ```bash
   dotnet build            # TreatWarningsAsErrors + AnalysisMode=All + StyleCop
   dotnet test
   dotnet format --verify-no-changes
   ```

5. Open a PR using the template. Keep PRs focused and small.

## Tests and Docker

The suite has two lanes. Integration tests carry `[Trait("Category", "IntegrationTests")]`; every other test is a unit test.

- `just test-unit` runs the unit lane. It needs no Docker.
- `just test-integration` runs the Docker lane.
- `just verify` covers build, the test suite and formatting. Run it before you open a PR. Without a Docker daemon it covers the unit lane only.

The Docker lane needs a reachable Docker daemon. Each of these tests skips itself when Docker does not answer, so a missing daemon never makes them fail. The lane starts `ghcr.io/marvin-hsu/parsec-testcontainers`. The image is pulled anonymously and pinned by digest, so no registry login is necessary.

On a host that is not Linux, the package bridges the Parsec service socket over TCP. You do not have to install an extra tool.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(client): add key attestation support
fix(transport): honour socket timeout on macOS
```

## Code style

Style is machine-enforced (`.editorconfig` + StyleCop + `dotnet format`); if the build passes, the style is right. A few points tooling can't check:

- Public API changes require XML documentation and a note in the PR description.
- New dependencies require maintainer approval — this is a security-sensitive library and we keep the dependency graph minimal.
- Anything touching cryptographic material handling needs an explicit security rationale in the PR.

## Release process

Releases are tagged by maintainers and built deterministically. Versioning follows [SemVer 2.0](https://semver.org/).
