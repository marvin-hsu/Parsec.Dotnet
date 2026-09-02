# Security Policy

Parsec.Dotnet is a client library for a security service; we treat every report seriously.

## Supported versions

Only the latest released minor version receives security fixes.

## Reporting a vulnerability

**Please do not open a public issue.** Instead:

1. Use GitHub's [private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability) on this repository, or
2. Contact the maintainers directly (see repository metadata).

You can expect an acknowledgement within **3 business days** and a status update within **14 days**. We follow coordinated disclosure: please give us 90 days before public disclosure.

## Scope

In scope: the `Parsec.Client` package, its handling of key material, IPC transport security, and the build/release pipeline.
Out of scope: vulnerabilities in the Parsec service itself — report those to the [upstream Parsec project](https://github.com/parallaxsecond/parsec/blob/main/SECURITY.md).
