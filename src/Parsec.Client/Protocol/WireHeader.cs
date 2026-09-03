using System.Buffers.Binary;

namespace Parsec.Client.Protocol;

/// <summary>
/// The fixed-format header that starts every Parsec request and every Parsec response.
/// </summary>
/// <remarks>
/// <para>
/// The header is 36 bytes long. The header size field carries 30, which counts the bytes after
/// the magic number and the header size field. Every multi-byte field is little-endian.
/// </para>
/// <para>
/// A parse reads the header size field and does not trust a constant. A later protocol version
/// can make the header longer. The fields that this version defines keep their offsets, so a
/// longer header parses and the extra bytes stay unread.
/// </para>
/// </remarks>
internal readonly record struct WireHeader
{
    /// <summary>The byte count of a header of this protocol version.</summary>
    public const int Size = 36;

    /// <summary>The value of the header size field: the byte count of the header after that field.</summary>
    public const ushort HeaderSizeFieldValue = 30;

    /// <summary>The magic number that starts every message.</summary>
    public const uint MagicNumber = 0x5EC0A710;

    /// <summary>The byte count of the magic number and the header size field together.</summary>
    /// <remarks>Read this many bytes first. They tell you how many more bytes the header holds.</remarks>
    public const int PrefixSize = 6;

    /// <summary>The major version of the wire protocol that this library speaks.</summary>
    public const byte CurrentMajorVersion = 1;

    /// <summary>The minor version of the wire protocol that this library speaks.</summary>
    public const byte CurrentMinorVersion = 0;

    /// <summary>The content length limit that a parse applies when the caller gives no limit.</summary>
    public const uint DefaultMaxContentLength = 16 * 1024 * 1024;

    private const int MagicNumberOffset = 0;
    private const int HeaderSizeOffset = 4;
    private const int MajorVersionOffset = 6;
    private const int MinorVersionOffset = 7;
    private const int FlagsOffset = 8;
    private const int ProviderOffset = 10;
    private const int SessionOffset = 11;
    private const int ContentTypeOffset = 19;
    private const int AcceptTypeOffset = 20;
    private const int AuthTypeOffset = 21;
    private const int ContentLengthOffset = 22;
    private const int AuthLengthOffset = 26;
    private const int OpcodeOffset = 28;
    private const int StatusOffset = 32;
    private const int ReservedOffset = 34;

    /// <summary>Gets the major version of the wire protocol.</summary>
    public byte MajorVersion { get; init; }

    /// <summary>Gets the minor version of the wire protocol.</summary>
    public byte MinorVersion { get; init; }

    /// <summary>Gets the flags field. The protocol defines no flag yet, so the value is zero.</summary>
    public ushort Flags { get; init; }

    /// <summary>Gets the provider that runs the operation.</summary>
    public ProviderId Provider { get; init; }

    /// <summary>Gets the session identifier. The service ignores it today.</summary>
    public ulong Session { get; init; }

    /// <summary>Gets the encoding of the body of this message.</summary>
    public BodyType ContentType { get; init; }

    /// <summary>Gets the body encoding that a request accepts in the response.</summary>
    public BodyType AcceptType { get; init; }

    /// <summary>Gets the format of the authentication field of a request.</summary>
    public AuthType AuthType { get; init; }

    /// <summary>Gets the byte count of the body that comes after the header.</summary>
    public uint ContentLength { get; init; }

    /// <summary>Gets the byte count of the authentication field of a request.</summary>
    public ushort AuthLength { get; init; }

    /// <summary>Gets the operation that this message carries.</summary>
    public Opcode Opcode { get; init; }

    /// <summary>Gets the outcome that a response reports. A request sets zero.</summary>
    public ResponseStatus Status { get; init; }

    /// <summary>
    /// Makes the header of a request.
    /// </summary>
    /// <param name="opcode">The operation to run.</param>
    /// <param name="provider">The provider that runs the operation.</param>
    /// <param name="authType">The format of the authentication field.</param>
    /// <param name="contentLength">The byte count of the body.</param>
    /// <param name="authLength">The byte count of the authentication field.</param>
    /// <returns>A header that carries the current protocol version and a protobuf body.</returns>
    public static WireHeader CreateRequest(
        Opcode opcode,
        ProviderId provider,
        AuthType authType,
        uint contentLength,
        ushort authLength) => new()
        {
            MajorVersion = CurrentMajorVersion,
            MinorVersion = CurrentMinorVersion,
            Provider = provider,
            ContentType = BodyType.Protobuf,
            AcceptType = BodyType.Protobuf,
            AuthType = authType,
            ContentLength = contentLength,
            AuthLength = authLength,
            Opcode = opcode,
        };

    /// <summary>
    /// Reads the magic number and the header size field from the start of a message.
    /// </summary>
    /// <param name="source">The first bytes of a message. <see cref="PrefixSize"/> bytes are enough.</param>
    /// <param name="headerSize">The value of the header size field, or zero if the read failed.</param>
    /// <param name="error">The cause of a failed read, or <see cref="WireHeaderError.None"/>.</param>
    /// <returns><see langword="true"/> if the prefix is the start of a header of a known version.</returns>
    /// <remarks>
    /// A reader that works on a stream calls this method first. The header size field tells it
    /// how many more bytes to take before it calls <see cref="TryParse(ReadOnlySpan{byte}, out WireHeader, out WireHeaderError)"/>.
    /// </remarks>
    public static bool TryReadPrefix(ReadOnlySpan<byte> source, out ushort headerSize, out WireHeaderError error)
    {
        headerSize = 0;

        if (source.Length < PrefixSize)
        {
            error = WireHeaderError.SourceTooShort;
            return false;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(source[MagicNumberOffset..]) != MagicNumber)
        {
            error = WireHeaderError.BadMagicNumber;
            return false;
        }

        var value = BinaryPrimitives.ReadUInt16LittleEndian(source[HeaderSizeOffset..]);
        if (value < HeaderSizeFieldValue)
        {
            error = WireHeaderError.HeaderSizeTooSmall;
            return false;
        }

        headerSize = value;
        error = WireHeaderError.None;
        return true;
    }

    /// <summary>
    /// Parses a header and applies <see cref="DefaultMaxContentLength"/> to the content length.
    /// </summary>
    /// <param name="source">The bytes of the message, from the magic number onward.</param>
    /// <param name="header">The parsed header, or the default value if the parse failed.</param>
    /// <param name="error">The cause of a failed parse, or <see cref="WireHeaderError.None"/>.</param>
    /// <returns><see langword="true"/> if the header parsed.</returns>
    public static bool TryParse(ReadOnlySpan<byte> source, out WireHeader header, out WireHeaderError error) =>
        TryParse(source, DefaultMaxContentLength, out header, out error);

    /// <summary>
    /// Parses a header.
    /// </summary>
    /// <param name="source">The bytes of the message, from the magic number onward.</param>
    /// <param name="maxContentLength">The largest body that the caller accepts, in bytes.</param>
    /// <param name="header">The parsed header, or the default value if the parse failed.</param>
    /// <param name="error">The cause of a failed parse, or <see cref="WireHeaderError.None"/>.</param>
    /// <returns><see langword="true"/> if the header parsed.</returns>
    /// <remarks>
    /// The parse throws nothing. It rejects a bad magic number, a header size field below the
    /// value of this protocol version, a buffer that holds too few bytes, and a content length
    /// above the limit. It accepts a value that no enumeration member names, because a later
    /// service can add a provider, an operation or a status. The caller tests such a value with
    /// the IsKnown method of the enumeration.
    /// </remarks>
    public static bool TryParse(
        ReadOnlySpan<byte> source,
        uint maxContentLength,
        out WireHeader header,
        out WireHeaderError error)
    {
        header = default;

        if (!TryReadPrefix(source, out var headerSize, out error))
        {
            return false;
        }

        if (source.Length < PrefixSize + headerSize)
        {
            error = WireHeaderError.SourceTooShort;
            return false;
        }

        var contentLength = BinaryPrimitives.ReadUInt32LittleEndian(source[ContentLengthOffset..]);
        if (contentLength > maxContentLength)
        {
            error = WireHeaderError.ContentLengthTooLarge;
            return false;
        }

        header = new WireHeader
        {
            MajorVersion = source[MajorVersionOffset],
            MinorVersion = source[MinorVersionOffset],
            Flags = BinaryPrimitives.ReadUInt16LittleEndian(source[FlagsOffset..]),
            Provider = (ProviderId)source[ProviderOffset],
            Session = BinaryPrimitives.ReadUInt64LittleEndian(source[SessionOffset..]),
            ContentType = (BodyType)source[ContentTypeOffset],
            AcceptType = (BodyType)source[AcceptTypeOffset],
            AuthType = (AuthType)source[AuthTypeOffset],
            ContentLength = contentLength,
            AuthLength = BinaryPrimitives.ReadUInt16LittleEndian(source[AuthLengthOffset..]),
            Opcode = (Opcode)BinaryPrimitives.ReadUInt32LittleEndian(source[OpcodeOffset..]),
            Status = (ResponseStatus)BinaryPrimitives.ReadUInt16LittleEndian(source[StatusOffset..]),
        };

        error = WireHeaderError.None;
        return true;
    }

    /// <summary>
    /// Writes the header to a buffer.
    /// </summary>
    /// <param name="destination">The buffer. It needs <see cref="Size"/> bytes or more.</param>
    /// <returns><see langword="true"/> if the buffer was large enough and the header was written.</returns>
    /// <remarks>
    /// The method writes the magic number, the header size field and the two reserved bytes. It
    /// takes those four values from the protocol, not from the instance.
    /// </remarks>
    public bool TryWrite(Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            return false;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination[MagicNumberOffset..], MagicNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[HeaderSizeOffset..], HeaderSizeFieldValue);
        destination[MajorVersionOffset] = MajorVersion;
        destination[MinorVersionOffset] = MinorVersion;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[FlagsOffset..], Flags);
        destination[ProviderOffset] = (byte)Provider;
        BinaryPrimitives.WriteUInt64LittleEndian(destination[SessionOffset..], Session);
        destination[ContentTypeOffset] = (byte)ContentType;
        destination[AcceptTypeOffset] = (byte)AcceptType;
        destination[AuthTypeOffset] = (byte)AuthType;
        BinaryPrimitives.WriteUInt32LittleEndian(destination[ContentLengthOffset..], ContentLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[AuthLengthOffset..], AuthLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[OpcodeOffset..], (uint)Opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[StatusOffset..], (ushort)Status);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[ReservedOffset..], 0);
        return true;
    }
}
