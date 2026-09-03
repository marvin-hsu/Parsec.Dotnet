namespace Parsec.Client;

/// <summary>
/// Helper methods for <see cref="AuthType"/>.
/// </summary>
public static class AuthTypeExtensions
{
    /// <summary>
    /// Tells if the value is an authentication type that this protocol version defines.
    /// </summary>
    /// <param name="value">The value that came off the wire, or that the caller supplied.</param>
    /// <returns><see langword="true"/> if the protocol defines the value.</returns>
    /// <remarks>
    /// An unknown value is not an error here. The service can add authentication types, so the
    /// client must accept a value that it does not know and let the caller decide what to do.
    /// </remarks>
    public static bool IsKnown(this AuthType value) =>
        value is >= AuthType.None and <= AuthType.JwtSvid;
}
