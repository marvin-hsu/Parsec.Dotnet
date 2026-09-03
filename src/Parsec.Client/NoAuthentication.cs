namespace Parsec.Client;

/// <summary>
/// Sends no authentication at all.
/// </summary>
/// <remarks>
/// The request carries authentication type 0 and an empty authentication field. The core provider
/// accepts only this type, and every core operation, such as Ping and ListProviders, uses it. A
/// crypto provider accepts it only when the service runs with no authenticator, which the threat
/// model allows for a test deployment alone.
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
