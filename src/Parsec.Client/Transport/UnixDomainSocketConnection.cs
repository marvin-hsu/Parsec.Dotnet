using System.Net.Sockets;
using Parsec.Client.Protocol;

namespace Parsec.Client.Transport;

/// <summary>
/// One open Unix domain socket connection to the Parsec service.
/// </summary>
/// <param name="socket">The connected socket. The connection owns it and closes it.</param>
/// <param name="timeout">
/// The time limit of one send or one receive, or <see cref="Timeout.InfiniteTimeSpan"/> for no
/// limit.
/// </param>
/// <param name="maxBodyLength">The largest response body that the connection accepts, in bytes.</param>
internal sealed class UnixDomainSocketConnection(Socket socket, TimeSpan timeout, uint maxBodyLength)
    : IParsecConnection
{
    private readonly Socket _socket = socket;
    private readonly NetworkStream _stream = new(socket, ownsSocket: false);
    private readonly TimeSpan _timeout = timeout;

    private ParsecFrameReader? _reader;

    /// <inheritdoc/>
    public ValueTask SendAsync(ParsecRequest request, CancellationToken cancellationToken = default)
    {
        var bytes = request.ToArray();
        return TimeoutOperation.RunAsync(
            _timeout,
            token => _stream.WriteAsync(bytes, token),
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<FrameReadResult> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        // The reader holds the buffer of a part-read message, so one connection keeps one reader.
        _reader ??= new ParsecFrameReader(_stream) { MaxBodyLength = maxBodyLength };

        var reader = _reader;
        return TimeoutOperation.RunAsync(
            _timeout,
            reader.ReadResponseAsync,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync().ConfigureAwait(false);
        _socket.Dispose();
    }
}
