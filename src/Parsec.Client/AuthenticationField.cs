namespace Parsec.Client;

/// <summary>
/// Renders the authentication field of a request and applies the rules of the protocol to it.
/// </summary>
/// <remarks>
/// An application can supply its own <see cref="IParsecAuthentication"/>. That implementation is
/// outside the library, so this class treats it as untrusted: it checks the byte count before it
/// allocates, and it checks the byte count that the implementation reports after the write. A
/// faulty implementation must not make the client write a message that does not match its own
/// header.
/// </remarks>
internal static class AuthenticationField
{
    /// <summary>
    /// Builds the authentication field for a request.
    /// </summary>
    /// <param name="authentication">The authentication that the application chose.</param>
    /// <returns>The bytes of the authentication field. It can be empty.</returns>
    /// <exception cref="ParsecConfigurationException">
    /// The field does not fit in the header, or the implementation does not report a usable byte
    /// count.
    /// </exception>
    /// <remarks>
    /// Every provider takes every authentication type, the core provider included. The service
    /// authenticates a request before it looks at the provider, and two core operations, ListKeys
    /// and DeleteClient, need the identity of the application. An operation that needs no
    /// identity, such as Ping, chooses <see cref="NoAuthentication"/> for itself.
    /// </remarks>
    public static ReadOnlyMemory<byte> Create(IParsecAuthentication authentication)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        var length = authentication.AuthBytesLength;

        if (length is < 0 or > ushort.MaxValue)
        {
            throw new ParsecConfigurationException(
                ParsecErrorText.DescribeOversizeAuthenticationField(length));
        }

        if (length == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var buffer = new byte[length];
        var written = authentication.WriteAuthBytes(buffer);

        if (written != length)
        {
            throw new ParsecConfigurationException(
                ParsecErrorText.DescribeAuthenticationLengthMismatch(length, written));
        }

        return buffer;
    }

    /// <summary>
    /// Checks that a buffer holds the bytes of an authentication field.
    /// </summary>
    /// <param name="destination">The buffer to write to.</param>
    /// <param name="requiredLength">The byte count that the field needs.</param>
    /// <exception cref="ArgumentException">The buffer is too small.</exception>
    public static void ThrowIfDestinationTooSmall(Span<byte> destination, int requiredLength)
    {
        if (destination.Length < requiredLength)
        {
            throw new ArgumentException(
                ParsecErrorText.DescribeSmallAuthenticationBuffer(destination.Length, requiredLength),
                nameof(destination));
        }
    }
}
