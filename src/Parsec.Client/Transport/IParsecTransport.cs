namespace Parsec.Client.Transport;

/// <summary>
/// Opens connections to the Parsec service.
/// </summary>
/// <remarks>
/// The service answers one request per connection, so the client opens a connection for each
/// operation. A test replaces the real transport with a scripted one, which is why the wire
/// code depends on this interface and not on a socket.
/// </remarks>
internal interface IParsecTransport
{
    /// <summary>
    /// Opens one connection to the service.
    /// </summary>
    /// <param name="cancellationToken">Stops the attempt.</param>
    /// <returns>An open connection. The caller disposes it.</returns>
    public ValueTask<IParsecConnection> ConnectAsync(CancellationToken cancellationToken = default);
}
