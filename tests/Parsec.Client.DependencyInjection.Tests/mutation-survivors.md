# Surviving mutants in Parsec.Client.DependencyInjection, and why they stay

Score 75.00% over a small class. Three mutants survive, and all three survive for the same
reason: they remove work whose only effect is to release something that costs nothing today.

| File | Line | Mutation | Why it survives |
|---|---|---|---|
| `ParsecClientFactory` | 55 | `client?.DisposeAsync() ?? ValueTask.CompletedTask` loses its left side | `ParsecClient.DisposeAsync` releases nothing: a client holds no connection between calls. Not disposing it is not observable from outside, and will stop being true the day the transport pools connections. |
| `ParsecClientFactory` | 77 | the `if (_disposed) return null;` guard removed | Without it a second disposal disposes the client and the semaphore again. `SemaphoreSlim.Dispose` is idempotent and the client's is a no-op, so nothing raises. The guard stays because neither of those is a promise. |
| `ParsecClientFactory` | 86 | `_gate.Dispose()` removed | A leaked semaphore has no finalizer and no observable effect. Nothing reaches the gate after disposal, because the check in `GetAsync` runs before it. |

Killing any of these would mean asserting on the private state of the factory, which tests the
implementation rather than the behaviour, and would have to be rewritten the moment the
implementation changed. The three lines are cheap and correct; the tests that would pin them are
neither.

The score will rise on its own if a client ever holds something between calls, because then the
first mutant becomes observable and the other two follow it.
