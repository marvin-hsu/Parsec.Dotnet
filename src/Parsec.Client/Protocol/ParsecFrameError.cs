namespace Parsec.Client.Protocol;

/// <summary>
/// Tells why a read of one message from a stream did not finish.
/// </summary>
/// <remarks>
/// A read never throws for a malformed message. It reports one of these values instead. The
/// values that name a header fault repeat <see cref="WireHeaderError"/>, because that type
/// describes a fault in a buffer and this one describes a fault in a stream.
/// </remarks>
internal enum ParsecFrameError
{
    /// <summary>The message was read.</summary>
    None = 0,

    /// <summary>The stream ended before the message was complete.</summary>
    UnexpectedEndOfStream = 1,

    /// <summary>The first four bytes are not the magic number of the protocol.</summary>
    BadMagicNumber = 2,

    /// <summary>The header size field is below the size that this protocol version defines.</summary>
    HeaderSizeTooSmall = 3,

    /// <summary>The header states a body that is longer than the reader accepts.</summary>
    BodyTooLarge = 4,
}
