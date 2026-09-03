namespace Parsec.Client.Protocol;

/// <summary>
/// Tells why a wire header did not parse.
/// </summary>
/// <remarks>
/// A parse never throws. It reports one of these values instead. The caller decides what to do
/// with a bad header: a client closes the connection, a test asserts the value.
/// </remarks>
internal enum WireHeaderError
{
    /// <summary>The header parsed.</summary>
    None = 0,

    /// <summary>The buffer holds fewer bytes than the header needs.</summary>
    SourceTooShort = 1,

    /// <summary>The first four bytes are not the magic number of the protocol.</summary>
    BadMagicNumber = 2,

    /// <summary>The header size field is below the size that this protocol version defines.</summary>
    HeaderSizeTooSmall = 3,

    /// <summary>The content length field is above the limit that the caller gave.</summary>
    ContentLengthTooLarge = 4,
}
