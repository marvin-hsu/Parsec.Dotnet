using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Parsec.Testcontainers;

/// <summary>
/// Carries the connections of a client on this machine to the service in the container.
/// </summary>
/// <remarks>
/// <para>
/// The bridge exists for a host system that is not Linux. There the container runs in a virtual
/// machine, and a bind mount of the socket directory gives a socket file that carries no
/// connection. The bridge has two parts. In the container, <c>socat</c> accepts a connection on a
/// port and gives it to the socket of the service. On this machine, this class accepts a
/// connection on a Unix socket and gives it to the mapped port of the container.
/// </para>
/// <para>
/// The client under test then speaks only to a Unix socket, which is the transport of the
/// service. The bridge stays in the test infrastructure.
/// </para>
/// </remarks>
internal sealed class ParsecSocketBridge : IAsyncDisposable
{
    /// <summary>
    /// The port in the container that <c>socat</c> listens on. The container maps the port to a
    /// free port of this machine.
    /// </summary>
    internal const int PortInContainer = 5000;

    /// <summary>
    /// The number of connections that the socket holds while the accept loop is busy.
    /// </summary>
    private const int Backlog = 16;

    /// <summary>
    /// The number of bytes that one read of the copy loop can move.
    /// </summary>
    private const int BufferLength = 8192;

    private readonly Socket _listener;
    private readonly string _host;
    private readonly int _port;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<long, Task> _connections = new();
    private readonly Task _acceptLoop;
    private long _lastConnectionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecSocketBridge"/> class.
    /// </summary>
    /// <param name="listener">A Unix socket that listens.</param>
    /// <param name="socketPath">The path of the socket of <paramref name="listener"/>.</param>
    /// <param name="host">The host name of the mapped port of the container.</param>
    /// <param name="port">The mapped port of the container.</param>
    private ParsecSocketBridge(Socket listener, string socketPath, string host, int port)
    {
        _listener = listener;
        _host = host;
        _port = port;
        SocketPath = socketPath;
        _acceptLoop = AcceptLoopAsync();
    }

    /// <summary>
    /// Gets the path of the socket that a client on this machine connects to.
    /// </summary>
    internal string SocketPath { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// The bridge holds no open connection when the method completes. It cancels the copy loops,
    /// closes the socket and waits for the accept loop and for every connection.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);

        _listener.Dispose();

        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The accept loop ends with the cancel of the token.
        }

        // The connections read the token of this class, so they end before the token goes away.
        while (!_connections.IsEmpty)
        {
            foreach (var connection in _connections)
            {
                try
                {
                    await connection.Value.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The connection ends with the cancel of the token.
                }
                catch (SocketException)
                {
                    // One of the two sides went away.
                }
                catch (ObjectDisposedException)
                {
                    // The bridge closed the socket under the copy loop.
                }

                _ = _connections.TryRemove(connection.Key, out _);
            }
        }

        _stop.Dispose();
    }

    /// <summary>
    /// Starts a bridge. The socket is ready for a connection when the method gives the instance.
    /// </summary>
    /// <param name="socketPath">The path of the socket to make. The directory must exist.</param>
    /// <param name="host">The host name of the mapped port of the container.</param>
    /// <param name="port">The mapped port of the container.</param>
    /// <returns>A bridge that listens. Dispose it to close the socket.</returns>
    internal static ParsecSocketBridge Start(string socketPath, string host, int port)
    {
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen(Backlog);
        }
        catch
        {
            listener.Dispose();

            throw;
        }

        return new ParsecSocketBridge(listener, socketPath, host, port);
    }

    /// <summary>
    /// Cancels the token source of a connection.
    /// </summary>
    /// <param name="connection">The token source of the connection.</param>
    /// <returns>A task that completes when the cancel is done.</returns>
    private static async Task CancelAsync(CancellationTokenSource connection)
    {
        try
        {
            await connection.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The connection is already at its end.
        }
    }

    /// <summary>
    /// Moves the bytes of one direction, until the sender closes its side.
    /// </summary>
    /// <param name="from">The socket to read.</param>
    /// <param name="to">The socket to write.</param>
    /// <param name="connection">The token source of the connection. A failure cancels it.</param>
    /// <returns>A task that completes when the read gives no more bytes.</returns>
    private static async Task CopyAsync(Socket from, Socket to, CancellationTokenSource connection)
    {
        var buffer = new byte[BufferLength];

        try
        {
            while (true)
            {
                var read = await from.ReceiveAsync(buffer, SocketFlags.None, connection.Token).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                var sent = 0;

                while (sent < read)
                {
                    sent += await to.SendAsync(buffer.AsMemory(sent, read - sent), SocketFlags.None, connection.Token).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // The other direction waits on a peer that gives no more bytes, so it ends now too.
            await CancelAsync(connection).ConfigureAwait(false);

            throw;
        }

        try
        {
            // The other side must see the end of the answer, and not a connection that stays
            // open. A request and its answer then end together.
            to.Shutdown(SocketShutdown.Send);
        }
        catch (SocketException)
        {
            // The other side is already closed.
        }
        catch (ObjectDisposedException)
        {
            // The bridge closed the socket.
        }
    }

    /// <summary>
    /// Accepts one connection after the other, until the bridge closes.
    /// </summary>
    /// <returns>A task that completes when the socket closes.</returns>
    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            Socket client;

            try
            {
                client = await _listener.AcceptAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            StartConnection(client);
        }
    }

    /// <summary>
    /// Starts the work of one connection and keeps the task, so that the dispose can wait for it.
    /// </summary>
    /// <param name="client">The connection of the client.</param>
    private void StartConnection(Socket client)
    {
        // Each connection runs on its own, so the loop can accept the next one. A failure of one
        // connection is a failure of the client under test, not of the bridge.
        var id = Interlocked.Increment(ref _lastConnectionId);
        var task = ForwardAsync(client);

        _connections[id] = task;

        if (task.IsCompleted)
        {
            _ = _connections.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Gives one connection of a client to the mapped port of the container.
    /// </summary>
    /// <param name="client">The connection of the client.</param>
    /// <returns>A task that completes when both sides close.</returns>
    private async Task ForwardAsync(Socket client)
    {
        using (client)
        {
            // One direction that fails makes the other direction useless, because the peer of
            // that direction gives no more bytes. The token of the connection ends both.
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);

            try
            {
                using var upstream = new Socket(SocketType.Stream, ProtocolType.Tcp);

                await upstream.ConnectAsync(_host, _port, connection.Token).ConfigureAwait(false);

                await Task.WhenAll(
                    CopyAsync(client, upstream, connection),
                    CopyAsync(upstream, client, connection)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The bridge closes while the connection is open.
            }
            catch (SocketException)
            {
                // One of the two sides went away.
            }
            catch (ObjectDisposedException)
            {
                // The bridge closed the socket under the copy loop.
            }
        }
    }
}
