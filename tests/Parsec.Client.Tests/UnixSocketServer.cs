using System.Net.Sockets;

namespace Parsec.Client.Tests;

/// <summary>
/// A Unix domain socket listener for the transport tests. It stands in for the Parsec service:
/// it accepts one connection, keeps every byte that the client sends, and answers with the bytes
/// that the test states.
/// </summary>
internal sealed class UnixSocketServer : IAsyncDisposable
{
    private readonly Socket _listener;

    public UnixSocketServer()
    {
        // A Unix socket path has a short limit, so the name stays short.
        SocketPath = Path.Combine(Path.GetTempPath(), $"psc-{Guid.NewGuid():N}"[..12] + ".sock");
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        _listener.Listen(1);
    }

    /// <summary>Gets the path of the socket file.</summary>
    public string SocketPath { get; }

    /// <summary>Gets the address of the listener.</summary>
    public Uri Endpoint => new("unix:" + SocketPath);

    /// <summary>Waits for one client.</summary>
    /// <returns>The accepted socket. The caller disposes it.</returns>
    public Task<Socket> AcceptAsync() => _listener.AcceptAsync();

    /// <summary>
    /// Accepts one client, reads a stated number of bytes, and answers with stated bytes.
    /// </summary>
    /// <param name="requestLength">The byte count to read before the answer goes out.</param>
    /// <param name="reply">The bytes to answer with.</param>
    /// <returns>The bytes that the client sent.</returns>
    public async Task<byte[]> ExchangeAsync(int requestLength, byte[] reply)
    {
        using var client = await AcceptAsync();

        var request = new byte[requestLength];
        var total = 0;
        while (total < requestLength)
        {
            var read = await client.ReceiveAsync(request.AsMemory(total));
            if (read == 0)
            {
                throw new IOException("The client closed the connection before it sent the whole request.");
            }

            total += read;
        }

        await client.SendAsync(reply);
        return request;
    }

    public ValueTask DisposeAsync()
    {
        _listener.Dispose();

        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }

        return ValueTask.CompletedTask;
    }
}
