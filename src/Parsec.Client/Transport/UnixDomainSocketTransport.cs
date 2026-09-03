using System.Net.Sockets;
using Parsec.Client.Protocol;

namespace Parsec.Client.Transport;

/// <summary>
/// Connects to the Parsec service over a Unix domain socket.
/// </summary>
/// <remarks>
/// <para>
/// This is the transport that the service specification defines. The service listens on a socket
/// file, and the file permissions decide who can talk to it.
/// </para>
/// <para>
/// A fault of the socket becomes a <see cref="ParsecTransportException"/> that holds the fault of
/// the platform as its inner exception. A connect, send or receive that passes its time limit
/// raises <see cref="TimeoutException"/>.
/// </para>
/// </remarks>
internal sealed class UnixDomainSocketTransport : IParsecTransport
{
    /// <summary>The time limit that the transport uses when the caller states none.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixDomainSocketTransport"/> class with the
    /// default time limits.
    /// </summary>
    /// <param name="endpoint">The address of the service.</param>
    public UnixDomainSocketTransport(Uri endpoint)
        : this(endpoint, DefaultTimeout, DefaultTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixDomainSocketTransport"/> class.
    /// </summary>
    /// <param name="endpoint">The address of the service.</param>
    /// <param name="connectTimeout">
    /// The time limit of one connect, or <see cref="Timeout.InfiniteTimeSpan"/> for no limit.
    /// </param>
    /// <param name="ioTimeout">
    /// The time limit of one send or one receive, or <see cref="Timeout.InfiniteTimeSpan"/> for
    /// no limit.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A time limit is zero or negative.</exception>
    /// <exception cref="ParsecConfigurationException">
    /// The endpoint is not a usable Unix socket address.
    /// </exception>
    public UnixDomainSocketTransport(Uri endpoint, TimeSpan connectTimeout, TimeSpan ioTimeout)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        RequireTimeout(connectTimeout);
        RequireTimeout(ioTimeout);

        // The endpoint is checked here, and not at the first connect, so a mistake in the
        // configuration is reported while the application is still starting.
        SocketPath = ParsecEndpoint.GetSocketPath(endpoint);
        Endpoint = endpoint;
        ConnectTimeout = connectTimeout;
        IoTimeout = ioTimeout;
    }

    /// <summary>Gets the address of the service.</summary>
    public Uri Endpoint { get; }

    /// <summary>Gets the path of the socket file that the transport connects to.</summary>
    public string SocketPath { get; }

    /// <summary>Gets the time limit of one connect.</summary>
    public TimeSpan ConnectTimeout { get; }

    /// <summary>Gets the time limit of one send or one receive.</summary>
    public TimeSpan IoTimeout { get; }

    /// <summary>
    /// Gets or sets the largest response body that a connection of this transport accepts, in
    /// bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is above <see cref="Array.MaxLength"/>.</exception>
    public uint MaxBodyLength
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, (uint)Array.MaxLength);
            field = value;
        }
    }

    = WireHeader.DefaultMaxContentLength;

    /// <inheritdoc/>
    public async ValueTask<IParsecConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // The connection takes the socket over. The local goes to null at that point, so the
        // finally block closes the socket only when the caller gets nothing back.
        Socket? socket = null;

        try
        {
            socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            await TimeoutOperation.RunAsync(
                ConnectTimeout,
                token => socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), token),
                cancellationToken)
                .ConfigureAwait(false);

            var connection = new UnixDomainSocketConnection(socket, SocketPath, IoTimeout, MaxBodyLength);
            socket = null;
            return connection;
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            throw ParsecTransportException.FromSocketFault(SocketPath, exception);
        }
        finally
        {
            socket?.Dispose();
        }
    }

    private static void RequireTimeout(TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
    }
}
