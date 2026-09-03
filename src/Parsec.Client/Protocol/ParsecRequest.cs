namespace Parsec.Client.Protocol;

/// <summary>
/// One request message: a wire header, a body and an authentication field.
/// </summary>
/// <remarks>
/// <para>
/// The three parts are contiguous on the wire and they come in this order: header, body,
/// authentication field. The order is the one that the Go reference client packs. The header
/// carries the byte count of each of the other two parts.
/// </para>
/// <para>
/// The body holds an encoded protobuf message. The authentication field holds the bytes that
/// the authentication type of the header defines. Both can be empty. A Ping request to the core
/// provider has an empty body and an empty authentication field.
/// </para>
/// </remarks>
internal readonly record struct ParsecRequest
{
    /// <summary>Gets the header of the request.</summary>
    public WireHeader Header { get; init; }

    /// <summary>Gets the encoded body of the request.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets the authentication field of the request.</summary>
    public ReadOnlyMemory<byte> Auth { get; init; }

    /// <summary>Gets the byte count of the whole message.</summary>
    public int Length => WireHeader.Size + Body.Length + Auth.Length;

    /// <summary>
    /// Makes a request and fills the length fields of the header from the two byte blocks.
    /// </summary>
    /// <param name="opcode">The operation to run.</param>
    /// <param name="provider">The provider that runs the operation.</param>
    /// <param name="authType">The format of the authentication field.</param>
    /// <param name="body">The encoded body. It can be empty.</param>
    /// <param name="auth">The authentication field. It can be empty.</param>
    /// <returns>A request that carries the current protocol version and a protobuf body.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The authentication field is longer than <see cref="ushort.MaxValue"/>. The header cannot
    /// state such a length, so the message cannot go on the wire.
    /// </exception>
    public static ParsecRequest Create(
        Opcode opcode,
        ProviderId provider,
        AuthType authType,
        ReadOnlyMemory<byte> body,
        ReadOnlyMemory<byte> auth)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(auth.Length, ushort.MaxValue);

        return new ParsecRequest
        {
            Header = WireHeader.CreateRequest(
                opcode,
                provider,
                authType,
                (uint)body.Length,
                (ushort)auth.Length),
            Body = body,
            Auth = auth,
        };
    }

    /// <summary>
    /// Makes a request and builds its authentication field from an authentication.
    /// </summary>
    /// <param name="opcode">The operation to run.</param>
    /// <param name="provider">The provider that runs the operation.</param>
    /// <param name="authentication">The authentication that the application chose.</param>
    /// <param name="body">The encoded body. It can be empty.</param>
    /// <returns>A request that carries the current protocol version and a protobuf body.</returns>
    /// <exception cref="ParsecConfigurationException">
    /// The authentication does not suit the provider or does not fit in the header. See
    /// <see cref="AuthenticationField.Create"/>.
    /// </exception>
    public static ParsecRequest Create(
        Opcode opcode,
        ProviderId provider,
        IParsecAuthentication authentication,
        ReadOnlyMemory<byte> body)
    {
        var auth = AuthenticationField.Create(authentication, provider);
        return Create(opcode, provider, authentication.Type, body, auth);
    }

    /// <summary>
    /// Writes the whole message to a buffer.
    /// </summary>
    /// <param name="destination">The buffer. It needs <see cref="Length"/> bytes or more.</param>
    /// <param name="written">The byte count written, or zero if the buffer was too small.</param>
    /// <returns><see langword="true"/> if the buffer was large enough and the message was written.</returns>
    public bool TryWrite(Span<byte> destination, out int written)
    {
        written = 0;

        if (destination.Length < Length || !Header.TryWrite(destination))
        {
            return false;
        }

        Body.Span.CopyTo(destination[WireHeader.Size..]);
        Auth.Span.CopyTo(destination[(WireHeader.Size + Body.Length)..]);
        written = Length;
        return true;
    }

    /// <summary>
    /// Makes a new array that holds the whole message.
    /// </summary>
    /// <returns>The bytes to send, header first.</returns>
    public byte[] ToArray()
    {
        var buffer = new byte[Length];
        TryWrite(buffer, out _);
        return buffer;
    }
}
