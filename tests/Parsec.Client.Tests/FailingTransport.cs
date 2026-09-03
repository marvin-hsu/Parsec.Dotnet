using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Tests;

/// <summary>
/// A transport that throws at a stated step of an exchange. It plays the part of
/// FailingMockIpc in the Rust client.
/// </summary>
/// <remarks>
/// The default fault is an <see cref="IOException"/>, which is what a socket raises when the
/// service closes the connection. A test that needs a different fault gives a factory, because
/// each throw must make a new exception object with its own stack trace.
/// </remarks>
/// <param name="stage">The step at which the transport throws.</param>
/// <param name="failureFactory">Makes the exception to throw, or <see langword="null"/> for the default.</param>
internal sealed class FailingTransport(
    TransportFailureStage stage,
    Func<Exception>? failureFactory = null) : IParsecTransport
{
    private readonly List<ParsecRequest> _sentRequests = [];

    /// <summary>Gets the step at which the transport throws.</summary>
    public TransportFailureStage Stage => stage;

    /// <summary>
    /// Gets the requests that reached the transport. A request is recorded only when the
    /// transport throws later than the send step.
    /// </summary>
    public IReadOnlyList<ParsecRequest> SentRequests => _sentRequests;

    /// <summary>Gets the number of connections that were opened.</summary>
    public int ConnectCount { get; private set; }

    /// <summary>Gets the number of connections that were disposed.</summary>
    public int DisposedConnectionCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask<IParsecConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stage == TransportFailureStage.Connect)
        {
            throw MakeFailure();
        }

        ConnectCount++;

        // The caller owns the connection and disposes it, so the fake does not.
#pragma warning disable CA2000
        return ValueTask.FromResult<IParsecConnection>(new FailingConnection(this));
#pragma warning restore CA2000
    }

    private Exception MakeFailure() =>
        failureFactory?.Invoke()
        ?? new IOException($"The fake transport fails at the {stage} step of the exchange.");

    private sealed class FailingConnection(FailingTransport owner) : IParsecConnection
    {
        private bool _isDisposed;

        public ValueTask SendAsync(ParsecRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (owner.Stage == TransportFailureStage.Send)
            {
                throw owner.MakeFailure();
            }

            owner._sentRequests.Add(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask<FrameReadResult> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw owner.MakeFailure();
        }

        public ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                owner.DisposedConnectionCount++;
            }

            return ValueTask.CompletedTask;
        }
    }
}
