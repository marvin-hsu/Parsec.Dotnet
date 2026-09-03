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
    /// <param name="provider">The provider that the request goes to.</param>
    /// <returns>The bytes of the authentication field. It can be empty.</returns>
    /// <exception cref="ParsecConfigurationException">
    /// The request goes to <see cref="ProviderId.Core"/> with an authentication type other than
    /// <see cref="AuthType.None"/>, or the field does not fit in the header, or the
    /// implementation does not report a usable byte count.
    /// </exception>
    public static ReadOnlyMemory<byte> Create(
        IParsecAuthentication authentication,
        ProviderId provider)
    {
        ArgumentNullException.ThrowIfNull(authentication);

        var type = authentication.Type;

        // The core provider reports the state of the service and holds no keys, so it has no
        // identity to authenticate. The service answers a core request that carries any other
        // authentication type with NotAuthenticated.
        if (provider == ProviderId.Core && type != AuthType.None)
        {
            throw new ParsecConfigurationException(
                ParsecErrorText.DescribeCoreProviderAuthentication(type));
        }

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
