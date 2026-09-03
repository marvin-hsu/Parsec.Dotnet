using Parsec.Client.Protocol;

namespace Parsec.Client.Transport;

/// <summary>
/// One open connection to the Parsec service.
/// </summary>
/// <remarks>
/// A connection carries one request and one response. It is not safe for two threads at once.
/// </remarks>
internal interface IParsecConnection : IAsyncDisposable
{
    /// <summary>
    /// Sends one request.
    /// </summary>
    /// <param name="request">The message to send.</param>
    /// <param name="cancellationToken">Stops the send.</param>
    /// <returns>A task that completes when every byte of the message is written.</returns>
    public ValueTask SendAsync(ParsecRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one response.
    /// </summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The message, or the cause of the failure.</returns>
    public ValueTask<FrameReadResult> ReceiveAsync(CancellationToken cancellationToken = default);
}
