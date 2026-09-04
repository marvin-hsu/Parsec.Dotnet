using System.Buffers.Binary;
using Parsec.Client.Errors;
using Parsec.Client.Interop;
using Parsec.Client.Protocol;

namespace Parsec.Client.Authentication;

/// <summary>
/// Sends the user ID of the calling process.
/// </summary>
/// <remarks>
/// <para>
/// The request carries authentication type 3 and the user ID as an unsigned 32-bit
/// little-endian integer. The service asks the kernel for the credentials of the peer of the
/// Unix socket and compares them against the declared ID. A client cannot claim another user,
/// so this type does not trust the client process.
/// </para>
/// <para>
/// The class reports the effective user ID, which is the value that the kernel gives to the
/// service for the peer of the socket. The Rust reference client sends the real user ID
/// instead. The two values are the same unless the process changed its effective user, and the
/// effective value is the one that the service accepts.
/// </para>
/// </remarks>
public sealed class UnixPeerCredentialsAuthentication : IParsecAuthentication
{
    /// <summary>The byte count of the authentication field. It holds one 32-bit integer.</summary>
    internal const int UserIdByteCount = 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixPeerCredentialsAuthentication"/> class
    /// for the effective user of the current process.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// The platform has no C library that reports a Unix user ID.
    /// </exception>
    public UnixPeerCredentialsAuthentication()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(ParsecErrorText.UnavailableUserId);
        }

        UserId = LibC.GetEffectiveUserId();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnixPeerCredentialsAuthentication"/> class
    /// for a stated user ID. A test uses this to pin the byte layout to a known value.
    /// </summary>
    /// <param name="userId">The user ID to declare.</param>
    internal UnixPeerCredentialsAuthentication(uint userId) => UserId = userId;

    /// <summary>Gets the user ID that the request declares.</summary>
    public uint UserId { get; }

    /// <inheritdoc/>
    public AuthType Type => AuthType.UnixPeerCredentials;

    /// <inheritdoc/>
    public int AuthBytesLength => UserIdByteCount;

    /// <inheritdoc/>
    public int WriteAuthBytes(Span<byte> destination)
    {
        AuthenticationField.ThrowIfDestinationTooSmall(destination, UserIdByteCount);

        BinaryPrimitives.WriteUInt32LittleEndian(destination, UserId);
        return UserIdByteCount;
    }
}
