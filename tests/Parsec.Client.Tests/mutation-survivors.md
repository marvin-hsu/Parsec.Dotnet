# Surviving mutants in Parsec.Client, and why they stay

Score 96.31% over 271 mutants, 8 surviving. Each one below was read and judged. None of them
is a missing test that would catch a real defect.

## Equivalent: the mutant changes nothing a caller can observe

| File | Line | Mutation | Why it survives |
|---|---|---|---|
| `ParsecFrameReader` | 76 | `<` becomes `<=` | The buffer is resized to the length it already has. Wasteful for one call, identical in behaviour. |
| `ParsecFrameReader` | 98 | `ContentLength == 0 ? [] : new byte[n]` always allocates | `new byte[0]` and `[]` are the same array. The branch is an allocation shortcut. |
| `ParsecFrameReader` | 70 | block removal | The block only reports a prefix error that the following code reports again. |
| `UnixDomainSocketConnection` | 28 | `ownsSocket: false` becomes `true` | `DisposeAsync` disposes the stream and then the socket. `Socket.Dispose` is idempotent, so a second dispose from the stream changes nothing. |
| `UnixDomainSocketConnection` | 56 | `??=` becomes `=` | The reader keeps no state between frames. `FillAsync` reads exactly the bytes it asks for, so it never over-reads and never leaves a partial frame in the buffer. A new reader only costs one allocation. |
| `UnixDomainSocketTransport` | 53 | `ArgumentNullException.ThrowIfNull(endpoint)` removed | `ParsecEndpoint.GetSocketPath` guards again on the next line and throws the same type, so a caller sees no difference. The guard stays because it names the right parameter at the right level. |

## Not reachable from a test on this machine

| File | Line | Mutation | Why it survives |
|---|---|---|---|
| `UnixPeerCredentialsAuthentication` | 37 | `||` becomes `&&` | Both operands are false on Linux and macOS, so the branch behaves the same. Only a Windows or browser host separates them. |
| `UnixPeerCredentialsAuthentication` | 39 | the throw removed | Same reason: the guard body never runs here. |

## Left alone on purpose

`UnixDomainSocketTransport` line 122, `socket?.Dispose()` in the `finally` of a failed connect.
Removing it leaks one descriptor per failed connect. The test
`AConnectThatFailsClosesTheSocketItOpened` counts entries under `/dev/fd` across forty failed
connects, which is the only way to see the leak from outside the type. The count moves for
reasons of its own, so the check leaves room and the mutant survives it. A tighter threshold
would make the test flaky, which costs more than the mutant is worth.

`UnixDomainSocketConnection` line 70, the receive-side wrap of a socket fault. A short answer
from the peer is a framing fault and returns before the socket faults, so the test that closes
the peer mid-answer exercises the framing path rather than this one. Reaching it needs a peer
that faults the socket itself during a read, which no fake in this suite can arrange yet.
