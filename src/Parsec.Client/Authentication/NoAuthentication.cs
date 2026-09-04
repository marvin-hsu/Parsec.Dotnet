using Parsec.Client.Protocol;

namespace Parsec.Client.Authentication;

/// <summary>
/// Sends no authentication at all.
/// </summary>
/// <remarks>
/// The request carries authentication type 0 and an empty authentication field. An operation that
/// needs no application identity, such as Ping, uses it. An operation that reads or changes the
/// keys of an application, such as ListKeys or any operation of a crypto provider, needs a real
/// authentication, because the service refuses a request that carries no identity. A deployment
/// that runs with no authenticator at all accepts this type everywhere, which the threat model
/// allows for a test deployment alone.
/// </remarks>
public sealed class NoAuthentication : IParsecAuthentication
{
    /// <summary>Gets the shared instance. The type holds no state.</summary>
    public static NoAuthentication Instance { get; } = new();

    /// <inheritdoc/>
    public AuthType Type => AuthType.None;

    /// <inheritdoc/>
    public int AuthBytesLength => 0;

    /// <inheritdoc/>
    public int WriteAuthBytes(Span<byte> destination) => 0;
}
