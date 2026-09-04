using Parsec.Client.Protocol;

namespace Parsec.Client.Authentication;

/// <summary>
/// Supplies the authentication field of a request.
/// </summary>
/// <remarks>
/// <para>
/// The service reads the authentication type field of the header, then reads the authentication
/// field of the request in the format that the type defines. An implementation of this interface
/// supplies both parts.
/// </para>
/// <para>
/// The library has one implementation for each authentication type that the service accepts:
/// <see cref="NoAuthentication"/>, <see cref="DirectAuthentication"/>,
/// <see cref="UnixPeerCredentialsAuthentication"/> and <see cref="JwtSvidAuthentication"/>.
/// </para>
/// <para>
/// An implementation must be safe for use from more than one thread, because the client sends
/// requests in parallel. It must also report the same byte count for each call, because the
/// client writes the byte count into the header before it writes the bytes.
/// </para>
/// </remarks>
public interface IParsecAuthentication
{
    /// <summary>Gets the value for the authentication type field of the header.</summary>
    public AuthType Type { get; }

    /// <summary>
    /// Gets the byte count of the authentication field.
    /// </summary>
    /// <remarks>
    /// The count goes into the authentication length field of the header, which holds two bytes.
    /// A count above <see cref="ushort.MaxValue"/> does not fit on the wire.
    /// </remarks>
    public int AuthBytesLength { get; }

    /// <summary>
    /// Writes the authentication field.
    /// </summary>
    /// <param name="destination">
    /// The buffer to write to. It holds <see cref="AuthBytesLength"/> bytes or more.
    /// </param>
    /// <returns>The byte count written. It is equal to <see cref="AuthBytesLength"/>.</returns>
    /// <exception cref="ArgumentException">The buffer is too small.</exception>
    public int WriteAuthBytes(Span<byte> destination);
}
