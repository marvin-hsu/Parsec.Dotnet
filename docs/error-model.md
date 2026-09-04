# The error model

Something can go wrong in four places: in your own call, in the connection, in the bytes on the
wire, or inside the service. The exception hierarchy names which one it was, because the answer
decides what an application should do about it.

## The hierarchy

```
Exception
└── ParsecException                 abstract, catch this to catch everything
    ├── ParsecConfigurationException  the request was never sent
    ├── ParsecTransportException      the connection failed
    ├── ParsecProtocolException       the answer did not make sense
    └── ParsecServiceException        the service said no
        └── ParsecPsaException        ... and it was a cryptographic no
```

| Exception | Means | Usually |
|---|---|---|
| <xref:Parsec.Client.Errors.ParsecConfigurationException> | The client refused to send. The endpoint is not a `unix:` URI, the path is longer than the platform allows, or the service runs no provider that matches your options. | A deployment mistake. Retrying will not help. |
| <xref:Parsec.Client.Errors.ParsecTransportException> | The socket failed. The service is not running, the path is wrong, or the connection dropped mid-exchange. The inner exception is the fault the platform reported. | Worth retrying. |
| <xref:Parsec.Client.Errors.ParsecProtocolException> | The answer did not follow the protocol, or it carried something this client cannot read back. | A version mismatch or a defect. Not worth retrying. |
| <xref:Parsec.Client.Errors.ParsecServiceException> | The service answered, and the answer was a refusal. <xref:Parsec.Client.Errors.ParsecServiceException.Status> carries which. | Depends entirely on the status. |
| <xref:Parsec.Client.Errors.ParsecPsaException> | The same, for the statuses the cryptography specification defines. | Depends on the status. |

Every one of them carries the operation that failed, so a message names the request as well as
the reason.

## Statuses

The service reports two families of status. Service statuses run from 1 to 21 and describe the
request itself: a provider that is not registered, a body larger than the service accepts, an
authentication that did not check out. PSA statuses start at 1132 and describe the
cryptography: a key that already exists, a signature that did not verify, an algorithm the
provider will not run.

The split matters when you are deciding whether another provider would help.
<xref:Parsec.Client.Protocol.ResponseStatus.PsaErrorNotSupported> came from a provider that
reached the operation and would not run it, so a different provider might. A service status
such as <xref:Parsec.Client.Protocol.ResponseStatus.OpcodeDoesNotExist> came from the service
before any provider was asked, so no provider on that service will run it.

```csharp
try
{
    await client.Keys.GenerateKeyAsync(name, attributes);
}
catch (ParsecPsaException fault) when (fault.Status is ResponseStatus.PsaErrorAlreadyExists)
{
    // The name is taken. This is a normal outcome, not a failure.
}
```

A status this client does not know still arrives as a
<xref:Parsec.Client.Errors.ParsecServiceException> carrying the number. The service may add
statuses, and refusing to parse an answer over a value the client had not heard of would break
a client the moment the service was upgraded ahead of it.

## What answers instead of raising

Three operations return a `bool` where you might expect an exception:

- <xref:Parsec.Client.Operations.IParsecCryptoOperations.VerifyHashAsync*> and
  <xref:Parsec.Client.Operations.IParsecCryptoOperations.VerifyMessageAsync*>
- <xref:Parsec.Client.Operations.IParsecCryptoOperations.HashCompareAsync*>
- <xref:Parsec.Client.IParsecClient.CanDoCryptoAsync*>

A signature that does not match is the answer to the question that was asked, not a failure of
the request. A caller forced to catch an exception to learn it would sooner or later catch one
that meant something else — a broken connection, a missing key — and read it as a failed check.
That is the shape of bug that lets a bad signature through, so these three answer `false` and
raise for everything else.

<xref:Parsec.Client.Operations.IParsecCryptoOperations.AeadDecryptAsync*> is the exception that
proves the rule. A tag that does not match raises, because there is no plaintext to hand back
and returning nothing alongside a `false` invites a caller to read the nothing.

## What the client refuses to read

An unknown opcode or status is carried through. An unknown *algorithm* or *key type* is not: it
raises <xref:Parsec.Client.Errors.ParsecProtocolException>.

The difference is that a status is a number the caller can inspect, while an algorithm has to
become a value in the model before anything can be done with it. There is no way to say "an
algorithm the specification added after this client was built", and folding an unknown value
into the nearest one the client does know would report a key policy that nobody set. Saying so
is the smaller harm.

## Arguments

Bad arguments raise the exceptions the platform defines, not Parsec ones:
`ArgumentNullException` for a missing name or algorithm, `ArgumentOutOfRangeException` for a
value the specification does not define. Those are defects in the calling code, and they never
reach the service.
