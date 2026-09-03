namespace Parsec.Client.Protocol;

/// <summary>
/// Reads whole Parsec messages from a byte stream.
/// </summary>
/// <remarks>
/// <para>
/// A stream delivers bytes, not messages. One read of the stream can return one byte of a
/// header. The reader loops until it holds every byte that it asked for, so a message that
/// arrives in pieces still parses.
/// </para>
/// <para>
/// The reader takes three steps. It reads <see cref="WireHeader.PrefixSize"/> bytes to get the
/// magic number and the header size field. It reads as many more header bytes as that field
/// states, so a longer header of a later protocol version does not break the frame. It then
/// reads as many body bytes as the content length field states.
/// </para>
/// <para>
/// The reader checks the content length against <see cref="MaxBodyLength"/> before it reads one
/// body byte. A service that states a huge body therefore costs the caller no memory. This is
/// the rule that mitigation AS3 of the threat model asks for.
/// </para>
/// <para>
/// The reader throws nothing for a malformed message. It reports the fault in
/// <see cref="FrameReadResult.Error"/>. The stream itself can still throw, and a cancellation
/// still raises <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
/// <param name="stream">The stream to read from. The reader does not own it and does not close it.</param>
internal sealed class ParsecFrameReader(Stream stream)
{
    private readonly Stream _stream = stream;
    private byte[] _headerBuffer = new byte[WireHeader.Size];

    /// <summary>
    /// Gets or sets the largest body that the reader accepts, in bytes.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="WireHeader.DefaultMaxContentLength"/>, which is 16 MiB. The
    /// upper bound is <see cref="Array.MaxLength"/>, because the reader puts the body in one
    /// array.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is above <see cref="Array.MaxLength"/>.</exception>
    public uint MaxBodyLength
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, (uint)Array.MaxLength);
            field = value;
        }
    }

    = WireHeader.DefaultMaxContentLength;

    /// <summary>
    /// Reads one response message.
    /// </summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The message, or the cause of the failure.</returns>
    public async ValueTask<FrameReadResult> ReadResponseAsync(CancellationToken cancellationToken = default)
    {
        if (!await FillAsync(_headerBuffer.AsMemory(0, WireHeader.PrefixSize), cancellationToken).ConfigureAwait(false))
        {
            return FrameReadResult.Failed(ParsecFrameError.UnexpectedEndOfStream);
        }

        if (!WireHeader.TryReadPrefix(_headerBuffer, out var headerSize, out var prefixError))
        {
            return FrameReadResult.Failed(FromHeaderError(prefixError));
        }

        // The header size field counts the bytes after itself, so the whole header is longer.
        var headerLength = WireHeader.PrefixSize + headerSize;
        if (_headerBuffer.Length < headerLength)
        {
            // Array.Resize copies what is there, so the prefix that was read stays in place.
            Array.Resize(ref _headerBuffer, headerLength);
        }

        var rest = _headerBuffer.AsMemory(WireHeader.PrefixSize, headerSize);
        if (!await FillAsync(rest, cancellationToken).ConfigureAwait(false))
        {
            return FrameReadResult.Failed(ParsecFrameError.UnexpectedEndOfStream);
        }

        // The parse applies the body limit. It fails before any body byte leaves the stream.
        if (!WireHeader.TryParse(
            _headerBuffer.AsSpan(0, headerLength),
            MaxBodyLength,
            out var header,
            out var parseError))
        {
            return FrameReadResult.Failed(FromHeaderError(parseError));
        }

        var body = header.ContentLength == 0 ? [] : new byte[header.ContentLength];
        if (!await FillAsync(body, cancellationToken).ConfigureAwait(false))
        {
            return FrameReadResult.Failed(ParsecFrameError.UnexpectedEndOfStream);
        }

        return FrameReadResult.Succeeded(new ParsecResponse { Header = header, Body = body });
    }

    private static ParsecFrameError FromHeaderError(WireHeaderError error) => error switch
    {
        WireHeaderError.BadMagicNumber => ParsecFrameError.BadMagicNumber,
        WireHeaderError.HeaderSizeTooSmall => ParsecFrameError.HeaderSizeTooSmall,
        WireHeaderError.ContentLengthTooLarge => ParsecFrameError.BodyTooLarge,

        // The reader always gives the parse every byte that the header size field asked for, so
        // SourceTooShort cannot come from a full buffer. Report the stream as the cause.
        _ => ParsecFrameError.UnexpectedEndOfStream,
    };

    private async ValueTask<bool> FillAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < destination.Length)
        {
            var read = await _stream.ReadAsync(destination[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }
}
