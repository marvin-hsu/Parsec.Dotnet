using System.Diagnostics.CodeAnalysis;

namespace Parsec.Client;

/// <summary>
/// Identifies the way that a request proves the identity of the application.
/// </summary>
/// <remarks>
/// The value goes into the authentication type field of the wire header. A request to
/// <see cref="ProviderId.Core"/> always uses <see cref="None"/> and carries no authentication
/// bytes.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1028:Enum storage should be Int32",
    Justification = "The authentication type field of the wire header is one unsigned byte.")]
public enum AuthType : byte
{
    /// <summary>No authentication. The request carries no authentication bytes.</summary>
    None = 0,

    /// <summary>The application identity, as plain UTF-8 text.</summary>
    Direct = 1,

    /// <summary>A JSON Web Token. The service does not support this type.</summary>
    Jwt = 2,

    /// <summary>The user ID of the caller, as an unsigned 32-bit little-endian integer.</summary>
    UnixPeerCredentials = 3,

    /// <summary>A JWT SPIFFE Verifiable Identity Document.</summary>
    JwtSvid = 4,
}
