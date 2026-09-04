# Security notes

This page is about the limits of what a Parsec client can promise. None of it is a defect: it
is the shape of the system, and an application that assumes otherwise is the one that gets
hurt.

## What using Parsec buys you

The private half of a key never enters your process. It is created inside a provider, it is
used inside a provider, and unless the policy of the key says otherwise it cannot be exported
at all. A memory disclosure in your application does not disclose the key.

That is the property worth having, and most of what follows is about not giving it away by
accident.

## The channel is not authenticated in either direction

A response carries no signature. The client cannot tell a real service from anything else that
answers on the same socket, and the protocol offers nothing that would let it.

The socket is the trust boundary. Whatever can write to it can act as the service, and — under
Direct authentication — whatever can write to it can claim to be any application. Getting the
permissions on that socket right is not a hardening step; it is the security of the deployment.

- Prefer <xref:Parsec.Client.Authentication.UnixPeerCredentialsAuthentication> where the
  service supports it. The kernel supplies the identity and a caller cannot lie about it.
- Under <xref:Parsec.Client.Authentication.DirectAuthentication>, restrict the socket to the
  applications entitled to the keys behind it. The name is a label, not a credential.
- Do not forward the socket over a network. A relay makes the service see the relay's
  credentials, and it puts an unauthenticated channel somewhere it can be reached.

## Sensitive data in a managed process

Bytes this library hands back — an exported key, a shared secret, a plaintext — are ordinary
managed arrays. That means:

- The garbage collector may copy them while they live. Overwriting the array you hold does not
  reach a copy the collector has already made elsewhere.
- They may be paged to disk by the operating system.
- They live until they are collected, not until you stop using them.

There is no way to fix this from inside a .NET library, and pretending otherwise with a
`Clear()` at the end of a method would be worse than saying so: it reads as a guarantee it
cannot make. Clearing what you hold is still worth doing — it shortens the window and it costs
nothing — but treat it as reducing exposure rather than removing it.

The real answer is not to have the material at all. Sign inside the service instead of
exporting a key to sign at home; keep <xref:Parsec.Client.Keys.KeyUsages.Export> off unless
something genuinely needs it.

## Key policy is the enforcement point

A key binds to one algorithm and one set of permissions, and the service checks every request
against them. A signing key cannot be talked into decrypting, and a key without
<xref:Parsec.Client.Keys.KeyUsages.Export> does not come out.

Grant only what the application uses. The factories on
<xref:Parsec.Client.Keys.KeyAttributes> default to the narrow choice — export is off unless you
pass `exportable: true` — because widening a policy later is a decision someone makes on
purpose, while narrowing one after the fact means recreating the key.

> [!NOTE]
> The Mbed Crypto provider widens a policy slightly on its own: a key asked for
> <xref:Parsec.Client.Keys.KeyUsages.SignHash> comes back also carrying
> <xref:Parsec.Client.Keys.KeyUsages.SignMessage>, because a key that may sign a hash may sign
> a message it hashes itself. Read the policy back if you need to know exactly what a key
> permits.

## Names are not secrets, and they are not private

A key name is a UTF-8 string in a per-application namespace. Another application cannot read
your keys, but the name itself is not protected: do not encode a secret in it, and do not rely
on a name being unguessable.

## Random comes from the provider

<xref:Parsec.Client.Operations.IParsecCryptoOperations.GenerateRandomAsync*> asks the provider,
which on hardware means the generator in the hardware. That is the reason to use it rather than
the platform. The client checks that the number of bytes it got back is the number it asked
for, because a short answer that went unnoticed would become a secret with fewer bits in it
than the caller believed.

## Failures you should not retry blindly

<xref:Parsec.Client.Errors.ParsecTransportException> is worth a retry.
<xref:Parsec.Client.Errors.ParsecServiceException> usually is not: a refusal is a decision, and
repeating a request that a provider declined tends to produce nothing but load. In particular
do not retry an authentication failure in a loop — some deployments count them.

## Reporting a problem

Security issues in this library go through the process in
[SECURITY.md](https://github.com/marvin-hsu/Parsec.Dotnet/blob/main/SECURITY.md) rather than a
public issue. Problems in the Parsec service itself belong upstream, with the
[Parsec project](https://parsec.community/).
