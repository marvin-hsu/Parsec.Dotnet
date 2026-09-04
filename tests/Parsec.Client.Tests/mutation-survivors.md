# Surviving mutants in Parsec.Client, and why they stay

Score 95.79% over 1069 mutants, between 8 and 11 surviving depending on the run. The ones that
come and go are the equivalent mutants below, which Stryker reaches through different tests each
time it picks a covering set. Each one below was read and judged. None of them
is a missing test that would catch a real defect.

## Equivalent: the mutant changes nothing a caller can observe

| File | Line | Mutation | Why it survives |
|---|---|---|---|
| `KeyAttributesCodec` | 110 | `GroupFamily = ToWireDh(...)` dropped from the initializer | RFC 7919 is the only Diffie-Hellman family the specification defines and it is the zero value, so proto3 leaves the field out whether it is assigned or not. The bytes are identical. The assignment stays because a second family would make it matter. |
| `KeyAttributesCodec` | 224 | `flags \|= flag` becomes `flags ^= flag` | `Set` runs once per flag over a value that starts at `None`, so no flag is ever set twice. Exclusive or and or agree on every input the method can receive. |
| `AuthenticationField` | 45 | the `length == 0` block removed | Without the shortcut the method allocates an empty array, asks the authentication to write nothing into it and hands back an empty memory. Same answer, one allocation. |
| `ParsecFrameReader` | 76 | `<` becomes `<=` | The buffer is resized to the length it already has. Wasteful for one call, identical in behaviour. |
| `ParsecFrameReader` | 98 | `ContentLength == 0 ? [] : new byte[n]` always allocates | `new byte[0]` and `[]` are the same array. The branch is an allocation shortcut. |
| `UnixDomainSocketConnection` | 29 | `ownsSocket: false` becomes `true` | `DisposeAsync` disposes the stream and then the socket. `Socket.Dispose` is idempotent, so a second dispose from the stream changes nothing. |
| `UnixDomainSocketConnection` | 57 | `??=` becomes `=` | The reader keeps no state between frames. `FillAsync` reads exactly the bytes it asks for, so it never over-reads and never leaves a partial frame in the buffer. A new reader only costs one allocation. |
| `UnixDomainSocketTransport` | 54 | `ArgumentNullException.ThrowIfNull(endpoint)` removed | `ParsecEndpoint.GetSocketPath` guards again on the next line and throws the same type, so a caller sees no difference. The guard stays because it names the right parameter at the right level. |

## Not reachable from a test on this machine

| File | Line | Mutation | Why it survives |
|---|---|---|---|
| `UnixPeerCredentialsAuthentication` | 39 | `\|\|` becomes `&&` | Both operands are false on Linux and macOS, so the branch behaves the same. Only a Windows or browser host separates them, and CI covers those. |
| `ParsecEndpoint` | 52 | `IsLinux() \|\| IsWindows()` becomes `&&`, and the conditional forced to its second branch | The socket path field is 108 bytes on Linux and on Windows and 104 on macOS and the BSDs. On this machine only the macOS branch runs, so nothing separates the two operands. CI covers the other side: `TheAcceptedPathLengthMatchesThePlatform` checks the constant against `UnixDomainSocketEndPoint` on all four runners, and it is the test that caught the wrong Windows value in the first place. |

## Left alone on purpose

`UnixDomainSocketTransport` line 123, `socket?.Dispose()` in the `finally` of a failed connect.
Removing it leaks one descriptor per failed connect. The test
`AConnectThatFailsClosesTheSocketItOpened` counts entries under `/dev/fd` across forty failed
connects, which is the only way to see the leak from outside the type. The count moves for
reasons of its own, so the check leaves room and the mutant survives it. A tighter threshold
would make the test flaky, which costs more than the mutant is worth.

## Checks removed rather than tested

Mutation testing found four guards that no input can reach, and removing them was the honest
answer in each case.

Step 21 added a range check to `AlgorithmCodec.ToWireAead` and another to `ToWireAgreement`.
`AeadAlgorithm` and `KeyAgreementAlgorithm` have private constructors and every factory rejects a
value the specification does not define, so the field can never hold one. No test could reach the
throw, and a check no input reaches is not defence but noise.

Step 22 added `ArgumentNullException.ThrowIfNull(attributes)` to `GenerateKeyAsync` and
`ImportKeyAsync`. `KeyAttributesCodec.ToWire` guards on the next line, raises the same type and
names the same parameter, so no caller could tell the two apart. The guard on the key name stays:
without it a generated setter raises the same type but names a protobuf field, which tells the
caller nothing about the argument they passed. That difference is worth a line of code, and
`EveryOperationRefusesANullName` asserts the parameter name so the guard cannot quietly go away.

Each removal carries a comment saying why, and the public factories keep the tests that prove
they refuse an undefined value.
