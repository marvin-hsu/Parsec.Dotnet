# Getting started

This page takes you from an empty project to a signature made by a key you cannot read.

## Install

```bash
dotnet add package Parsec.Client
```

`Parsec.Client` depends on the protobuf runtime and nothing else. Two optional packages sit
beside it: `Parsec.Client.DependencyInjection` for applications built on
`Microsoft.Extensions`, and `Parsec.Testcontainers` for integration tests. An application that
wants neither is not made to carry them.

## Reach a service

A client needs a running Parsec service. On a machine that already has one, the default
endpoint is the Unix domain socket at `/run/parsec/parsec.sock`, and an administrator can move
it by setting `PARSEC_SERVICE_ENDPOINT`. The client reads that variable on its own.

If you have no service to hand, start one in a container. See [Testing against a real
service](testing.md).

## Build a client

```csharp
using Parsec.Client;
using Parsec.Client.Authentication;

await using var client = await ParsecClient.CreateAsync(new ParsecClientOptions
{
    Authentication = new DirectAuthentication("my-application"),
});
```

<xref:Parsec.Client.ParsecClient.CreateAsync*> makes two round trips before it hands anything
back. The first is a Ping, which agrees the protocol version and proves the service answers.
The second is a ListProviders, which finds the provider to bind to: the one that
<xref:Parsec.Client.ParsecClientOptions.Provider> names, or the first one that is not the core
provider.

That is deliberate. A service that is absent, unreachable, or running nothing but the core
provider fails here, where your application can still do something about it, rather than inside
whichever operation you happen to call first.

> [!IMPORTANT]
> The default authentication identifies nobody. It is enough to ask the service what it can do
> and not enough to own a key. Anything that touches a key needs an identity. See
> [Authentication and application identity](authentication.md).

## Create a key

```csharp
using Parsec.Client.Algorithms;
using Parsec.Client.Keys;

var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

await client.Keys.GenerateKeyAsync("my-key", KeyAttributes.RsaSigningKey(algorithm: algorithm));
```

<xref:Parsec.Client.Keys.KeyAttributes> describes what the key holds, how large it is and what
may be done with it. The four factories on it cover the common shapes;
<xref:Parsec.Client.Keys.KeyAttributes.RsaSigningKey*> here means a 2048 bit RSA key pair that
may sign and verify a hash and may not leave the service.

The private half never reaches your process. The name is how you reach the key afterwards, and
it belongs to the application that created it: another application authenticating as something
else cannot see it or use it.

> [!NOTE]
> A key binds to one algorithm. The service refuses a request that names any other, which is
> what stops a signing key from being talked into decrypting.

## Sign and verify

```csharp
var digest = await client.Crypto.HashComputeAsync(Hash.Sha256, "sign me"u8.ToArray());
var signature = await client.Crypto.SignHashAsync("my-key", algorithm, digest);

var ok = await client.Crypto.VerifyHashAsync("my-key", algorithm, digest, signature);
```

Verification answers with a `bool`. A signature that does not match is the answer to the
question, not a failure of the request. Every other outcome raises, so a caller that has to
catch an exception to learn a signature was wrong would sooner or later catch one that meant
something else. See [The error model](error-model.md).

## Check the public half elsewhere

Nothing about the signature ties it to Parsec. Export the public key and the platform can check
the signature without the service.

```csharp
using System.Security.Cryptography;

var publicKey = await client.Keys.ExportPublicKeyAsync("my-key");

using var rsa = RSA.Create();
rsa.ImportRSAPublicKey(publicKey, out _);

var verified = rsa.VerifyHash(digest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
```

Exporting a public key needs no permission. Exporting the private half needs
<xref:Parsec.Client.Keys.KeyUsages.Export> on the policy of the key, which is off unless you
ask for it.

## Clean up

```csharp
await client.Keys.DestroyKeyAsync("my-key");
```

Removing a key that is not there is a fault rather than a quiet success, because the two cases
mean different things to an application cleaning up after itself.

## What runs where

Not every provider runs every operation. Ask before you assume:

```csharp
var supported = await client.ListOpcodesAsync(client.Provider);
```

The software provider that ships in the test image runs sixteen operations. It has no cipher
and no message-signing operation, and it cannot attest a key, because attestation needs a
device that can speak for one.

## Next

- [Authentication and application identity](authentication.md)
- [The error model](error-model.md)
- [The wire protocol](wire-protocol.md)
- [Testing against a real service](testing.md)
- [Security notes](security.md)
