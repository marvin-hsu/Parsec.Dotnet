using Parsec.Client.Authentication;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client;

/// <summary>
/// The settings that <see cref="ParsecClient.CreateAsync"/> builds a client from.
/// </summary>
/// <remarks>
/// Every setting has a default that suits an application on the same machine as the service. The
/// one worth thinking about is <see cref="Authentication"/>: the default identifies nobody, which
/// is enough to ask the service what it can do and not enough to own a key.
/// </remarks>
public sealed class ParsecClientOptions
{
    /// <summary>
    /// Gets the address of the service, or <see langword="null"/> to read
    /// <see cref="ParsecEndpoint.EnvironmentVariableName"/> and fall back to the default socket.
    /// </summary>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// Gets how the application identifies itself. The default identifies nobody.
    /// </summary>
    /// <remarks>
    /// A key belongs to the application that created it, and an application with no identity owns
    /// no keys. Use <see cref="DirectAuthentication"/> or one of its siblings for anything that
    /// touches a key.
    /// </remarks>
    public IParsecAuthentication Authentication { get; init; } = NoAuthentication.Instance;

    /// <summary>
    /// Gets the provider to work with, or <see langword="null"/> to take the first one the
    /// service reports that is not the core provider.
    /// </summary>
    /// <remarks>
    /// The core provider runs no cryptography, so picking it would leave the client unable to do
    /// the work it exists for. Name a provider when the service runs more than one and the choice
    /// matters, which it does as soon as a hardware provider sits beside the software one.
    /// </remarks>
    public ProviderId? Provider { get; init; }

    /// <summary>Gets the time limit of one connect.</summary>
    public TimeSpan ConnectTimeout { get; init; } = UnixDomainSocketTransport.DefaultTimeout;

    /// <summary>Gets the time limit of one send or one receive.</summary>
    public TimeSpan IoTimeout { get; init; } = UnixDomainSocketTransport.DefaultTimeout;

    /// <summary>
    /// Gets the largest answer the client reads, in bytes. The default is 16 MiB.
    /// </summary>
    /// <remarks>
    /// The limit is checked against the length the header states, before the body is read, so a
    /// service that claims an enormous answer costs nothing.
    /// </remarks>
    public uint MaxBodyLength { get; init; } = WireHeader.DefaultMaxContentLength;
}
