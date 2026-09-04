# Authentication and application identity

A Parsec key belongs to the application that created it. The service decides which application
is asking from the authentication field of each request, so the identity your client sends is
not a formality: it is the boundary between your keys and everyone else's.

## What an identity is for

Key names live in a per-application namespace. Two applications can both own a key called
`signing-key` and neither can see, use or destroy the other's. `ListKeys` returns the keys of
the application that authenticated the request and no others.

That makes the identity part of your deployment, not part of your code. It has to be

- **unique**, or two applications share a namespace and can destroy each other's keys;
- **stable across restarts**, or the application loses its keys the next time it starts;
- **not a secret**, because Direct authentication sends it in the clear and the service does
  not treat it as proof of anything.

The last point is the one that catches people out. Read [Security notes](security.md) before
choosing.

## The four types

| Type | Sends | Use when |
|---|---|---|
| <xref:Parsec.Client.Authentication.NoAuthentication> | Nothing | Asking the service about itself. This is the default. |
| <xref:Parsec.Client.Authentication.DirectAuthentication> | The application name as UTF-8 | The service is configured for `Direct` and the socket is already trusted |
| <xref:Parsec.Client.Authentication.UnixPeerCredentialsAuthentication> | The effective user id as a little-endian 32-bit integer | The service is configured for `UnixPeerCredentials` and both sides are on one machine |
| <xref:Parsec.Client.Authentication.JwtSvidAuthentication> | A SPIFFE JWT-SVID | A SPIFFE workload API issues identities |

A service runs one authenticator. Ask it which:

```csharp
var authenticators = await client.ListAuthenticatorsAsync();
```

## No authentication is a real choice

`NoAuthentication` is the default of <xref:Parsec.Client.ParsecClientOptions>, and it is right
for a client that only asks questions: Ping, ListProviders, ListOpcodes and
ListAuthenticators need no identity, and Ping is sent without one whatever you configure,
because an application calls it to find the service before it knows which authenticator the
service runs.

It is wrong for anything else. With no identity the service has no namespace to put a key in,
and `ListKeys` answers `NotAuthenticated`.

> [!NOTE]
> The API overview of the Parsec book says the core provider accepts only `None`. That is not
> what the service does: it checks the identity before it looks at the provider, so the core
> provider takes any authentication type. Individual operations choose, not providers.

## Getting it wrong fails early

A client configured with an authentication type the service does not run cannot be built at
all. <xref:Parsec.Client.ParsecClient.CreateAsync*> asks ListProviders, the service refuses it,
and the failure carries <xref:Parsec.Client.Protocol.ResponseStatus.AuthenticatorNotRegistered>
naming that operation. This is measured against a service configured for peer credentials and
sent a Direct identity, not inferred.

Ping is the exception, and deliberately so: it carries no authentication whatever the client is
configured with, which is what lets an application find a service before it knows how to
identify itself to it.

## Direct

```csharp
new DirectAuthentication("my-application")
```

The name travels as UTF-8 and the service takes it at face value. Anything that can reach the
socket can claim any name, so Direct is only as strong as the permissions on the socket. It is
what the test image uses, and it is the right choice when the socket is already restricted to
one trusted application.

## Unix peer credentials

```csharp
new UnixPeerCredentialsAuthentication()
```

The client sends its effective user id and the kernel tells the service the real one. A caller
that lies is caught, which makes this the strongest of the local options.

Two things to know. The identity is the user id, so every process running as that user shares
one namespace. And it only works when the client talks to the socket directly: anything that
forwards the connection — a relay, a port mapping, a container bridge — makes the service see
the credentials of the forwarder. That is why the bridge that `Parsec.Testcontainers` uses on
non-Linux hosts cannot carry it.

## JWT-SVID

```csharp
new JwtSvidAuthentication(token)
```

The token comes from a SPIFFE workload API, not from this library. It has a lifetime, and this
type does not refresh it: build a new client, or fetch a new token and build a new
authentication, before it expires.

## Choosing at run time

The authentication is fixed when the client is built, so a client that has to change identity
builds another one. In an application using
[`Parsec.Client.DependencyInjection`](getting-started.md), the settings are built from the
container, so the identity can come from configuration:

```csharp
builder.Services.AddParsecClient(provider => new ParsecClientOptions
{
    Authentication = new DirectAuthentication(
        provider.GetRequiredService<IConfiguration>()["Parsec:ApplicationName"]!),
});
```
