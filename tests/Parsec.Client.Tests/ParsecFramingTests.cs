using System.Buffers.Binary;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Locks the byte layout of a framed message and the behaviour of the stream reader.
/// The golden bytes come from the observed exchange with a real Parsec 1.5.0 service and from
/// the field table of the wire protocol reference, which was checked against parsec-client-go.
/// </summary>
public sealed class ParsecFramingTests
{
    /// <summary>
    /// The 36 bytes of a Ping request for the core provider with no authentication. A Ping
    /// request carries no body and no authentication field, so the message is the header alone.
    /// </summary>
    private const string PingRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "01000000" + "0000" + "0000";

    /// <summary>
    /// A signature request for the Mbed Crypto provider with direct authentication. The body is
    /// AABBCC and the authentication field is the UTF-8 text "app", which is 617070. The two
    /// blocks differ, so the constant proves that the body comes first. The header states
    /// provider 1, auth type 1, content length 3, auth length 3 and opcode 4.
    /// </summary>
    private const string BodyThenAuthHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "01" + "0000000000000000" +
        "00" + "00" + "01" + "03000000" + "0300" + "04000000" + "0000" + "0000" +
        "AABBCC" + "617070";

    /// <summary>
    /// The Ping response of a real service: the 36 header bytes, then the two body bytes 0801.
    /// The body holds only the major version, because protobuf3 leaves a zero-valued scalar off
    /// the wire.
    /// </summary>
    private const string PingResponseHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "02000000" + "0000" + "01000000" + "0000" + "0000" + "0801";

    /// <summary>
    /// A response that reports a failure: status 1149, PsaErrorInvalidSignature, and no body.
    /// </summary>
    private const string FailedResponseHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "01" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "05000000" + "7D04" + "0000";

    [Fact]
    public void PingRequestWritesTheGoldenBytes()
    {
        var request = ParsecRequest.Create(
            Opcode.Ping,
            ProviderId.Core,
            AuthType.None,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty);

        Assert.Equal(WireHeader.Size, request.Length);
        Assert.Equal(Convert.FromHexString(PingRequestHex), request.ToArray());
    }

    [Fact]
    public void RequestWritesTheBodyBeforeTheAuthenticationField()
    {
        var request = ParsecRequest.Create(
            Opcode.PsaSignHash,
            ProviderId.MbedCrypto,
            AuthType.Direct,
            new byte[] { 0xAA, 0xBB, 0xCC },
            "app"u8.ToArray());

        Assert.Equal(WireHeader.Size + 6, request.Length);
        Assert.Equal(Convert.FromHexString(BodyThenAuthHex), request.ToArray());
    }

    [Fact]
    public void CreateFillsTheLengthFieldsOfTheHeaderFromTheTwoBlocks()
    {
        var request = ParsecRequest.Create(
            Opcode.PsaImportKey,
            ProviderId.Tpm,
            AuthType.UnixPeerCredentials,
            new byte[300],
            new byte[4]);

        Assert.Equal(300u, request.Header.ContentLength);
        Assert.Equal(4, request.Header.AuthLength);
        Assert.Equal(Opcode.PsaImportKey, request.Header.Opcode);
        Assert.Equal(ProviderId.Tpm, request.Header.Provider);
        Assert.Equal(AuthType.UnixPeerCredentials, request.Header.AuthType);
    }

    [Fact]
    public void CreateRejectsAnAuthenticationFieldThatTheHeaderCannotDescribe()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ParsecRequest.Create(
            Opcode.Ping,
            ProviderId.Core,
            AuthType.Direct,
            ReadOnlyMemory<byte>.Empty,
            new byte[ushort.MaxValue + 1]));
    }

    [Fact]
    public void RequestDoesNotWriteToABufferThatIsTooSmall()
    {
        var request = ParsecRequest.Create(
            Opcode.Ping,
            ProviderId.Core,
            AuthType.None,
            new byte[] { 0x01 },
            ReadOnlyMemory<byte>.Empty);
        var buffer = new byte[request.Length - 1];
        Array.Fill(buffer, (byte)0xCC);

        Assert.False(request.TryWrite(buffer, out var written));
        Assert.Equal(0, written);
        Assert.All(buffer, b => Assert.Equal(0xCC, b));
    }

    [Fact]
    public void RequestLeavesTheRestOfALargerBufferAlone()
    {
        var request = ParsecRequest.Create(
            Opcode.Ping,
            ProviderId.Core,
            AuthType.None,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty);
        var buffer = new byte[request.Length + 3];
        Array.Fill(buffer, (byte)0xCC);

        Assert.True(request.TryWrite(buffer, out var written));
        Assert.Equal(request.Length, written);
        Assert.Equal(Convert.FromHexString(PingRequestHex), buffer[..written]);
        Assert.All(buffer[written..], b => Assert.Equal(0xCC, b));
    }

    [Fact]
    public async Task ReaderReadsTheObservedPingResponse()
    {
        var stream = new MemoryStream(Convert.FromHexString(PingResponseHex));
        var reader = new ParsecFrameReader(stream);

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParsecFrameError.None, result.Error);
        Assert.True(result.Response.IsSuccess);
        Assert.Equal(Opcode.Ping, result.Response.Header.Opcode);
        Assert.Equal(ResponseStatus.Success, result.Response.Header.Status);
        Assert.Equal(new byte[] { 0x08, 0x01 }, result.Response.Body.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(35)]
    [InlineData(37)]
    public async Task ReaderRebuildsAMessageThatArrivesInPieces(int chunkSize)
    {
        var frame = Convert.FromHexString(PingResponseHex);
        var stream = new DripStream(frame, chunkSize);
        var reader = new ParsecFrameReader(stream);

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 0x08, 0x01 }, result.Response.Body.ToArray());
        Assert.Equal(frame.Length, stream.Position);
    }

    [Fact]
    public async Task ReaderNeedsManyReadsWhenTheStreamGivesOneByteAtATime()
    {
        var frame = Convert.FromHexString(PingResponseHex);
        var stream = new DripStream(frame, 1);
        var reader = new ParsecFrameReader(stream);

        Assert.True((await reader.ReadResponseAsync(TestContext.Current.CancellationToken)).IsSuccess);

        // One read per byte of the frame. The test would pass with a single large read too, so
        // the count proves that the stream really did deliver the frame one byte at a time.
        Assert.Equal(frame.Length, stream.ReadCount);
    }

    [Fact]
    public async Task ReaderAcceptsAnEmptyBody()
    {
        // A protobuf message whose every field holds a default value encodes to no bytes.
        var stream = new MemoryStream(Convert.FromHexString(FailedResponseHex));
        var reader = new ParsecFrameReader(stream);

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Response.IsSuccess);
        Assert.Equal(ResponseStatus.PsaErrorInvalidSignature, result.Response.Header.Status);
        Assert.True(result.Response.Body.IsEmpty);
    }

    [Fact]
    public async Task ReaderReadsTwoMessagesFromOneStream()
    {
        var stream = new MemoryStream(
            [.. Convert.FromHexString(PingResponseHex), .. Convert.FromHexString(FailedResponseHex)]);
        var reader = new ParsecFrameReader(stream);

        var first = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);
        var second = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Opcode.Ping, first.Response.Header.Opcode);
        Assert.Equal(new byte[] { 0x08, 0x01 }, first.Response.Body.ToArray());
        Assert.Equal(Opcode.PsaVerifyHash, second.Response.Header.Opcode);
        Assert.Equal(ResponseStatus.PsaErrorInvalidSignature, second.Response.Header.Status);
    }

    [Fact]
    public async Task ReaderRejectsALongBodyBeforeItReadsOneBodyByte()
    {
        // The header states 16 MiB plus one byte, which is above the default limit. The stream
        // holds the header alone. A reader that tried to take the body would block or fail here.
        var header = ResponseHeaderWithContentLength(WireHeader.DefaultMaxContentLength + 1);
        var stream = new MemoryStream(header);
        var reader = new ParsecFrameReader(stream);

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParsecFrameError.BodyTooLarge, result.Error);
        Assert.Equal(WireHeader.Size, stream.Position);
    }

    [Fact]
    public async Task ReaderAcceptsABodyThatIsExactlyAtTheLimit()
    {
        var frame = new byte[WireHeader.Size + 4];
        ResponseHeaderWithContentLength(4).CopyTo(frame, 0);
        frame[^4] = 0x0A;
        var reader = new ParsecFrameReader(new MemoryStream(frame)) { MaxBodyLength = 4 };

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Response.Body.Length);
        Assert.Equal(0x0A, result.Response.Body.Span[0]);
    }

    [Fact]
    public async Task ReaderRejectsABodyOneByteAboveTheLimit()
    {
        var frame = new byte[WireHeader.Size + 4];
        ResponseHeaderWithContentLength(4).CopyTo(frame, 0);
        var reader = new ParsecFrameReader(new MemoryStream(frame)) { MaxBodyLength = 3 };

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ParsecFrameError.BodyTooLarge, result.Error);
    }

    [Fact]
    public void MaxBodyLengthRejectsAValueThatNoArrayCanHold()
    {
        var reader = new ParsecFrameReader(new MemoryStream());

        Assert.Throws<ArgumentOutOfRangeException>(() => reader.MaxBodyLength = uint.MaxValue);
        Assert.Equal(WireHeader.DefaultMaxContentLength, reader.MaxBodyLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReaderRejectsABadMagicNumberAfterSixBytes(int corruptedIndex)
    {
        var frame = Convert.FromHexString(PingResponseHex);
        frame[corruptedIndex] ^= 0xFF;
        var stream = new MemoryStream(frame);
        var reader = new ParsecFrameReader(stream);

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ParsecFrameError.BadMagicNumber, result.Error);
        Assert.Equal(WireHeader.PrefixSize, stream.Position);
    }

    [Fact]
    public async Task ReaderRejectsAHeaderSizeFieldBelowTheSizeOfThisVersion()
    {
        var frame = Convert.FromHexString(PingResponseHex);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), WireHeader.HeaderSizeFieldValue - 1);
        var reader = new ParsecFrameReader(new MemoryStream(frame));

        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ParsecFrameError.HeaderSizeTooSmall, result.Error);
    }

    [Fact]
    public async Task ReaderSkipsTheExtraBytesOfALongerHeader()
    {
        // A later protocol version can make the header longer. The fields of this version keep
        // their offsets, so the frame still parses and the body still starts after the header.
        const int extra = 10;
        var frame = new byte[WireHeader.Size + extra + 2];
        Convert.FromHexString(PingResponseHex).AsSpan(0, WireHeader.Size).CopyTo(frame);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), WireHeader.HeaderSizeFieldValue + extra);
        Array.Fill(frame, (byte)0xEE, WireHeader.Size, extra);
        frame[^2] = 0x08;
        frame[^1] = 0x01;

        var reader = new ParsecFrameReader(new MemoryStream(frame));
        var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(Opcode.Ping, result.Response.Header.Opcode);
        Assert.Equal(new byte[] { 0x08, 0x01 }, result.Response.Body.ToArray());
    }

    [Fact]
    public async Task ReaderReportsEndOfStreamForEveryTruncationOfTheFrame()
    {
        var frame = Convert.FromHexString(PingResponseHex);

        for (var length = 0; length < frame.Length; length++)
        {
            var reader = new ParsecFrameReader(new MemoryStream(frame, 0, length));

            var result = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(ParsecFrameError.UnexpectedEndOfStream, result.Error);
        }
    }

    [Fact]
    public async Task ReaderReportsEndOfStreamWhenTheSecondMessageIsMissing()
    {
        var stream = new MemoryStream(Convert.FromHexString(PingResponseHex));
        var reader = new ParsecFrameReader(stream);

        Assert.True((await reader.ReadResponseAsync(TestContext.Current.CancellationToken)).IsSuccess);
        var second = await reader.ReadResponseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ParsecFrameError.UnexpectedEndOfStream, second.Error);
    }

    /// <summary>
    /// Takes the golden Ping response header and puts another value in the content length field.
    /// The golden constant pins the offsets. Only the four bytes at offset 22 change.
    /// </summary>
    private static byte[] ResponseHeaderWithContentLength(uint contentLength)
    {
        var header = Convert.FromHexString(PingResponseHex)[..WireHeader.Size];
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(22), contentLength);
        return header;
    }
}
