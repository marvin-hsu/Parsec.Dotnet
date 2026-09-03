using System.Buffers.Binary;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Locks the byte layout of the wire header and the behaviour of the parser.
/// The golden bytes come from the observed exchange with a real Parsec 1.5.0 service and from
/// the field table of the wire protocol reference, which was checked against parsec-client-go.
/// </summary>
public sealed class WireHeaderTests
{
    /// <summary>
    /// The 36 bytes of a Ping request for the core provider with no authentication. Written out
    /// by hand from the field table: magic 0x5EC0A710, header size 30, version 1.0, flags 0,
    /// provider 0, session 0, content type 0, accept type 0, auth type 0, content length 0,
    /// auth length 0, opcode 1, status 0, reserved 0.
    /// </summary>
    private const string PingRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "01000000" + "0000" + "0000";

    /// <summary>
    /// The header of the Ping response of a real service, followed by the two body bytes that
    /// the service sent. The capture reported magic 0x5EC0A710, status 0, body length 2 and
    /// body 0801. The body holds only the major version, because protobuf3 leaves a zero-valued
    /// scalar off the wire.
    /// </summary>
    private const string PingResponseHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "02000000" + "0000" + "01000000" + "0000" + "0000" + "0801";

    /// <summary>
    /// A header with a different value in every field that this protocol version defines.
    /// It locks the offset and the width of each field. Provider 2, session 0x1122334455667788,
    /// auth type 3, content length 0xAB, auth length 4, opcode 4, status 1149 (0x047D).
    /// </summary>
    private const string DistinctFieldsHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "02" + "8877665544332211" +
        "00" + "00" + "03" + "AB000000" + "0400" + "04000000" + "7D04" + "0000";

    private static WireHeader DistinctFieldsHeader => new()
    {
        MajorVersion = 1,
        MinorVersion = 0,
        Flags = 0,
        Provider = ProviderId.Pkcs11,
        Session = 0x1122334455667788,
        ContentType = BodyType.Protobuf,
        AcceptType = BodyType.Protobuf,
        AuthType = AuthType.UnixPeerCredentials,
        ContentLength = 0xAB,
        AuthLength = 4,
        Opcode = Opcode.PsaSignHash,
        Status = ResponseStatus.PsaErrorInvalidSignature,
    };

    [Fact]
    public void WriteOfPingRequestMatchesTheGoldenBytes()
    {
        var header = WireHeader.CreateRequest(Opcode.Ping, ProviderId.Core, AuthType.None, 0, 0);
        var buffer = new byte[WireHeader.Size];

        Assert.True(header.TryWrite(buffer));
        Assert.Equal(Convert.FromHexString(PingRequestHex), buffer);
    }

    [Fact]
    public void WriteOfEveryFieldMatchesTheGoldenBytes()
    {
        var buffer = new byte[WireHeader.Size];

        Assert.True(DistinctFieldsHeader.TryWrite(buffer));
        Assert.Equal(Convert.FromHexString(DistinctFieldsHex), buffer);
    }

    [Fact]
    public void ParseOfEveryFieldReadsTheGoldenBytes()
    {
        Assert.True(WireHeader.TryParse(Convert.FromHexString(DistinctFieldsHex), out var header, out var error));

        Assert.Equal(WireHeaderError.None, error);
        Assert.Equal(DistinctFieldsHeader, header);
    }

    [Fact]
    public void ParseReadsTheHeaderOfTheObservedPingResponse()
    {
        var frame = Convert.FromHexString(PingResponseHex);

        Assert.True(WireHeader.TryParse(frame, out var header, out var error));

        Assert.Equal(WireHeaderError.None, error);
        Assert.Equal(ResponseStatus.Success, header.Status);
        Assert.Equal(Opcode.Ping, header.Opcode);
        Assert.Equal(ProviderId.Core, header.Provider);
        Assert.Equal(1, header.MajorVersion);
        Assert.Equal(0, header.MinorVersion);
        Assert.Equal(2u, header.ContentLength);
        Assert.Equal(BodyType.Protobuf, header.ContentType);

        // The content length field says where the body ends. Two bytes follow the 36-byte header.
        Assert.Equal(new byte[] { 0x08, 0x01 }, frame.AsSpan(WireHeader.Size, (int)header.ContentLength).ToArray());
    }

    [Fact]
    public void WriteAndParseRoundTripEveryField()
    {
        var buffer = new byte[WireHeader.Size];
        Assert.True(DistinctFieldsHeader.TryWrite(buffer));

        Assert.True(WireHeader.TryParse(buffer, out var parsed, out _));
        Assert.Equal(DistinctFieldsHeader, parsed);
    }

    [Fact]
    public void WriteUsesExactlyTheHeaderSizeAndLeavesTheRestOfTheBufferAlone()
    {
        var buffer = new byte[WireHeader.Size + 4];
        Array.Fill(buffer, (byte)0xCC);

        Assert.True(WireHeader.CreateRequest(Opcode.Ping, ProviderId.Core, AuthType.None, 0, 0).TryWrite(buffer));

        Assert.Equal(Convert.FromHexString(PingRequestHex), buffer.AsSpan(0, WireHeader.Size).ToArray());
        Assert.Equal(new byte[] { 0xCC, 0xCC, 0xCC, 0xCC }, buffer.AsSpan(WireHeader.Size).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(WireHeader.Size - 1)]
    public void WriteFailsAndTouchesNothingWhenTheBufferIsTooSmall(int length)
    {
        var buffer = new byte[length];
        Array.Fill(buffer, (byte)0xCC);

        Assert.False(DistinctFieldsHeader.TryWrite(buffer));
        Assert.All(buffer, b => Assert.Equal(0xCC, b));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ParseRejectsAWrongMagicNumberInAnyByte(int index)
    {
        var bytes = Convert.FromHexString(PingRequestHex);
        bytes[index] ^= 0xFF;

        Assert.False(WireHeader.TryParse(bytes, out var header, out var error));
        Assert.Equal(WireHeaderError.BadMagicNumber, error);
        Assert.Equal(default, header);
    }

    [Fact]
    public void ParseRejectsAHeaderSizeFieldBelowTheValueOfThisVersion()
    {
        var bytes = Convert.FromHexString(PingRequestHex);
        bytes[4] = WireHeader.HeaderSizeFieldValue - 1;

        Assert.False(WireHeader.TryParse(bytes, out _, out var error));
        Assert.Equal(WireHeaderError.HeaderSizeTooSmall, error);
    }

    [Fact]
    public void ParseAcceptsALongerHeaderAndReadsOnlyTheFieldsOfThisVersion()
    {
        // A later protocol version can add fields at the end. The offsets of the known fields
        // stay the same, so the header still parses.
        var bytes = Convert.FromHexString(PingRequestHex).Concat(new byte[] { 0xAA, 0xBB }).ToArray();
        bytes[4] = WireHeader.HeaderSizeFieldValue + 2;

        Assert.True(WireHeader.TryParse(bytes, out var header, out var error));
        Assert.Equal(WireHeaderError.None, error);
        Assert.Equal(Opcode.Ping, header.Opcode);
    }

    [Fact]
    public void ParseRejectsALongerHeaderWhenTheBufferStopsEarly()
    {
        var bytes = Convert.FromHexString(PingRequestHex);
        bytes[4] = WireHeader.HeaderSizeFieldValue + 2;

        Assert.False(WireHeader.TryParse(bytes, out _, out var error));
        Assert.Equal(WireHeaderError.SourceTooShort, error);
    }

    [Fact]
    public void ParseRejectsEveryTruncationOfAValidHeader()
    {
        var bytes = Convert.FromHexString(PingRequestHex);

        for (var length = 0; length < WireHeader.Size; length++)
        {
            Assert.False(WireHeader.TryParse(bytes.AsSpan(0, length), out var header, out var error));
            Assert.Equal(WireHeaderError.SourceTooShort, error);
            Assert.Equal(default, header);
        }
    }

    [Fact]
    public void ParseRejectsAContentLengthAboveTheLimitThatTheCallerGave()
    {
        var bytes = Convert.FromHexString(PingRequestHex);
        bytes[22] = 0x41; // content length 0x41 = 65

        Assert.False(WireHeader.TryParse(bytes, 64, out var header, out var error));
        Assert.Equal(WireHeaderError.ContentLengthTooLarge, error);
        Assert.Equal(default, header);

        Assert.True(WireHeader.TryParse(bytes, 65, out var accepted, out _));
        Assert.Equal(65u, accepted.ContentLength);
    }

    [Fact]
    public void ParseAppliesTheDefaultContentLengthLimitOfSixteenMebibytes()
    {
        var bytes = Convert.FromHexString(PingRequestHex);
        WriteContentLength(bytes, WireHeader.DefaultMaxContentLength);

        Assert.True(WireHeader.TryParse(bytes, out var header, out _));
        Assert.Equal(WireHeader.DefaultMaxContentLength, header.ContentLength);

        WriteContentLength(bytes, WireHeader.DefaultMaxContentLength + 1);

        Assert.False(WireHeader.TryParse(bytes, out _, out var error));
        Assert.Equal(WireHeaderError.ContentLengthTooLarge, error);
    }

    [Fact]
    public void ParseRejectsTheLargestContentLengthThatFitsTheField()
    {
        var bytes = Convert.FromHexString(PingRequestHex);
        WriteContentLength(bytes, uint.MaxValue);

        Assert.False(WireHeader.TryParse(bytes, out _, out var error));
        Assert.Equal(WireHeaderError.ContentLengthTooLarge, error);
    }

    [Fact]
    public void ParseAcceptsValuesThatNoEnumerationMemberNames()
    {
        // Threat model AS5: a value that this version does not know must not stop the parse.
        var bytes = Convert.FromHexString(PingRequestHex);
        bytes[10] = 0xFF; // provider
        bytes[21] = 0x09; // auth type
        bytes[28] = 0x1D; // opcode 0x1D, the unassigned one
        bytes[32] = 0xFF; // status
        bytes[33] = 0xFF;

        Assert.True(WireHeader.TryParse(bytes, out var header, out var error));

        Assert.Equal(WireHeaderError.None, error);
        Assert.Equal((ProviderId)0xFF, header.Provider);
        Assert.False(header.Provider.IsKnown());
        Assert.Equal((AuthType)0x09, header.AuthType);
        Assert.False(header.AuthType.IsKnown());
        Assert.Equal((Opcode)0x1D, header.Opcode);
        Assert.False(header.Opcode.IsKnown());
        Assert.Equal((ResponseStatus)0xFFFF, header.Status);
        Assert.False(header.Status.IsKnown());
    }

    [Fact]
    public void ParseIgnoresTheReservedBytes()
    {
        // The reference client rejects a non-zero reserved field. This library reads past it,
        // so a later service that uses those bytes does not break the client.
        var bytes = Convert.FromHexString(PingRequestHex);
        bytes[34] = 0xAA;
        bytes[35] = 0xBB;

        Assert.True(WireHeader.TryParse(bytes, out var header, out _));
        Assert.Equal(Opcode.Ping, header.Opcode);
    }

    [Fact]
    public void WriteAlwaysSetsTheMagicNumberTheHeaderSizeAndTheReservedBytes()
    {
        var buffer = new byte[WireHeader.Size];
        Array.Fill(buffer, (byte)0xCC);

        Assert.True(DistinctFieldsHeader.TryWrite(buffer));

        Assert.Equal(WireHeader.MagicNumber, BinaryPrimitives.ReadUInt32LittleEndian(buffer));
        Assert.Equal(WireHeader.HeaderSizeFieldValue, BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(4)));
        Assert.Equal(0, buffer[34]);
        Assert.Equal(0, buffer[35]);
    }

    [Fact]
    public void CreateRequestFillsTheVersionAndTheBodyEncoding()
    {
        var header = WireHeader.CreateRequest(Opcode.ListProviders, ProviderId.Core, AuthType.Direct, 7, 5);

        Assert.Equal(WireHeader.CurrentMajorVersion, header.MajorVersion);
        Assert.Equal(WireHeader.CurrentMinorVersion, header.MinorVersion);
        Assert.Equal(BodyType.Protobuf, header.ContentType);
        Assert.Equal(BodyType.Protobuf, header.AcceptType);
        Assert.Equal(Opcode.ListProviders, header.Opcode);
        Assert.Equal(ProviderId.Core, header.Provider);
        Assert.Equal(AuthType.Direct, header.AuthType);
        Assert.Equal(7u, header.ContentLength);
        Assert.Equal(5, header.AuthLength);
        Assert.Equal(ResponseStatus.Success, header.Status);
        Assert.Equal(0uL, header.Session);
        Assert.Equal(0, header.Flags);
    }

    [Fact]
    public void ReadPrefixTakesTheHeaderSizeFromTheFirstSixBytes()
    {
        var bytes = Convert.FromHexString(PingRequestHex);

        Assert.True(WireHeader.TryReadPrefix(bytes.AsSpan(0, WireHeader.PrefixSize), out var headerSize, out var error));
        Assert.Equal(WireHeader.HeaderSizeFieldValue, headerSize);
        Assert.Equal(WireHeaderError.None, error);
        Assert.Equal(WireHeader.Size, WireHeader.PrefixSize + headerSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ReadPrefixFailsOnFewerThanSixBytes(int length)
    {
        var bytes = Convert.FromHexString(PingRequestHex);

        Assert.False(WireHeader.TryReadPrefix(bytes.AsSpan(0, length), out var headerSize, out var error));
        Assert.Equal(WireHeaderError.SourceTooShort, error);
        Assert.Equal(0, headerSize);
    }

    [Fact]
    public void ReadPrefixReportsABadMagicNumber()
    {
        var bytes = Convert.FromHexString(PingRequestHex);
        bytes[3] = 0x00;

        Assert.False(WireHeader.TryReadPrefix(bytes, out var headerSize, out var error));
        Assert.Equal(WireHeaderError.BadMagicNumber, error);
        Assert.Equal(0, headerSize);
    }

    private static void WriteContentLength(byte[] bytes, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(22), value);
}
