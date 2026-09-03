namespace Parsec.Client.Tests;

/// <summary>
/// Locks the wire values of the protocol enums and the behaviour of every IsKnown method.
/// The expected values come from parsec-book (operations/README.md and status_codes.md) and
/// from the parsec-client-go and parsec-client-java reference implementations.
/// </summary>
public sealed class ProtocolPrimitivesTests
{
    /// <summary>
    /// The complete opcode table, written out independently of the enum. 0x1D is absent on
    /// purpose: the protocol assigns no operation to it.
    /// </summary>
    private static readonly Dictionary<string, uint> _expectedOpcodes = new(StringComparer.Ordinal)
    {
        ["Ping"] = 0x01,
        ["PsaGenerateKey"] = 0x02,
        ["PsaDestroyKey"] = 0x03,
        ["PsaSignHash"] = 0x04,
        ["PsaVerifyHash"] = 0x05,
        ["PsaImportKey"] = 0x06,
        ["PsaExportPublicKey"] = 0x07,
        ["ListProviders"] = 0x08,
        ["ListOpcodes"] = 0x09,
        ["PsaAsymmetricEncrypt"] = 0x0A,
        ["PsaAsymmetricDecrypt"] = 0x0B,
        ["PsaExportKey"] = 0x0C,
        ["PsaGenerateRandom"] = 0x0D,
        ["ListAuthenticators"] = 0x0E,
        ["PsaHashCompute"] = 0x0F,
        ["PsaHashCompare"] = 0x10,
        ["PsaAeadEncrypt"] = 0x11,
        ["PsaAeadDecrypt"] = 0x12,
        ["PsaRawKeyAgreement"] = 0x13,
        ["PsaCipherEncrypt"] = 0x14,
        ["PsaCipherDecrypt"] = 0x15,
        ["PsaMacCompute"] = 0x16,
        ["PsaMacVerify"] = 0x17,
        ["PsaSignMessage"] = 0x18,
        ["PsaVerifyMessage"] = 0x19,
        ["ListKeys"] = 0x1A,
        ["ListClients"] = 0x1B,
        ["DeleteClient"] = 0x1C,
        ["AttestKey"] = 0x1E,
        ["PrepareKeyAttestation"] = 0x1F,
        ["CanDoCrypto"] = 0x20,
    };

    /// <summary>
    /// The complete status table, written out independently of the enum. 1144 is absent on
    /// purpose: the protocol assigns no status to it.
    /// </summary>
    private static readonly Dictionary<string, ushort> _expectedStatuses = new(StringComparer.Ordinal)
    {
        ["Success"] = 0,
        ["WrongProviderId"] = 1,
        ["ContentTypeNotSupported"] = 2,
        ["AcceptTypeNotSupported"] = 3,
        ["WireProtocolVersionNotSupported"] = 4,
        ["ProviderNotRegistered"] = 5,
        ["ProviderDoesNotExist"] = 6,
        ["DeserializingBodyFailed"] = 7,
        ["SerializingBodyFailed"] = 8,
        ["OpcodeDoesNotExist"] = 9,
        ["ResponseTooLarge"] = 10,
        ["AuthenticationError"] = 11,
        ["AuthenticatorDoesNotExist"] = 12,
        ["AuthenticatorNotRegistered"] = 13,
        ["KeyInfoManagerError"] = 14,
        ["ConnectionError"] = 15,
        ["InvalidEncoding"] = 16,
        ["InvalidHeader"] = 17,
        ["WrongProviderUuid"] = 18,
        ["NotAuthenticated"] = 19,
        ["BodySizeExceedsLimit"] = 20,
        ["AdminOperation"] = 21,
        ["PsaErrorGenericError"] = 1132,
        ["PsaErrorNotPermitted"] = 1133,
        ["PsaErrorNotSupported"] = 1134,
        ["PsaErrorInvalidArgument"] = 1135,
        ["PsaErrorInvalidHandle"] = 1136,
        ["PsaErrorBadState"] = 1137,
        ["PsaErrorBufferTooSmall"] = 1138,
        ["PsaErrorAlreadyExists"] = 1139,
        ["PsaErrorDoesNotExist"] = 1140,
        ["PsaErrorInsufficientMemory"] = 1141,
        ["PsaErrorInsufficientStorage"] = 1142,
        ["PsaErrorInsufficientData"] = 1143,
        ["PsaErrorCommunicationFailure"] = 1145,
        ["PsaErrorStorageFailure"] = 1146,
        ["PsaErrorHardwareFailure"] = 1147,
        ["PsaErrorInsufficientEntropy"] = 1148,
        ["PsaErrorInvalidSignature"] = 1149,
        ["PsaErrorInvalidPadding"] = 1150,
        ["PsaErrorCorruptionDetected"] = 1151,
        ["PsaErrorDataCorrupt"] = 1152,
    };

    private static readonly Dictionary<string, byte> _expectedProviders = new(StringComparer.Ordinal)
    {
        ["Core"] = 0,
        ["MbedCrypto"] = 1,
        ["Pkcs11"] = 2,
        ["Tpm"] = 3,
        ["TrustedService"] = 4,
        ["CryptoAuthLib"] = 5,
    };

    private static readonly Dictionary<string, byte> _expectedAuthTypes = new(StringComparer.Ordinal)
    {
        ["None"] = 0,
        ["Direct"] = 1,
        ["Jwt"] = 2,
        ["UnixPeerCredentials"] = 3,
        ["JwtSvid"] = 4,
    };

    [Fact]
    public void OpcodeTableMatchesTheProtocol() => AssertTable<Opcode, uint>(_expectedOpcodes, v => (uint)v);

    [Fact]
    public void ResponseStatusTableMatchesTheProtocol() =>
        AssertTable<ResponseStatus, ushort>(_expectedStatuses, v => (ushort)v);

    [Fact]
    public void ProviderIdTableMatchesTheProtocol() => AssertTable<ProviderId, byte>(_expectedProviders, v => (byte)v);

    [Fact]
    public void AuthTypeTableMatchesTheProtocol() => AssertTable<AuthType, byte>(_expectedAuthTypes, v => (byte)v);

    [Fact]
    public void OpcodeUsesFourUnsignedBytesOnTheWire() =>
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Opcode)));

    [Fact]
    public void ResponseStatusUsesTwoUnsignedBytesOnTheWire() =>
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(ResponseStatus)));

    [Fact]
    public void ProviderIdUsesOneUnsignedByteOnTheWire() =>
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(ProviderId)));

    [Fact]
    public void AuthTypeUsesOneUnsignedByteOnTheWire() =>
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(AuthType)));

    [Fact]
    public void OpcodeIsKnownAgreesWithTheTableOverTheWholeByteRange()
    {
        for (uint value = 0; value <= byte.MaxValue; value++)
        {
            var expected = _expectedOpcodes.ContainsValue(value);
            Assert.Equal(expected, ((Opcode)value).IsKnown());
        }
    }

    [Fact]
    public void ResponseStatusIsKnownAgreesWithTheTableOverTheWholeWireRange()
    {
        for (uint value = 0; value <= ushort.MaxValue; value++)
        {
            var expected = _expectedStatuses.ContainsValue((ushort)value);
            Assert.Equal(expected, ((ResponseStatus)value).IsKnown());
        }
    }

    [Fact]
    public void ProviderIdIsKnownAgreesWithTheTableOverTheWholeByteRange()
    {
        for (uint value = 0; value <= byte.MaxValue; value++)
        {
            var expected = _expectedProviders.ContainsValue((byte)value);
            Assert.Equal(expected, ((ProviderId)value).IsKnown());
        }
    }

    [Fact]
    public void AuthTypeIsKnownAgreesWithTheTableOverTheWholeByteRange()
    {
        for (uint value = 0; value <= byte.MaxValue; value++)
        {
            var expected = _expectedAuthTypes.ContainsValue((byte)value);
            Assert.Equal(expected, ((AuthType)value).IsKnown());
        }
    }

    [Fact]
    public void OpcodeGapIsNotAssigned()
    {
        Assert.False(((Opcode)0x1D).IsKnown());
        Assert.True(((Opcode)0x1C).IsKnown());
        Assert.True(((Opcode)0x1E).IsKnown());
    }

    [Fact]
    public void ResponseStatusGapIsNotAssigned()
    {
        Assert.False(((ResponseStatus)1144).IsKnown());
        Assert.True(((ResponseStatus)1143).IsKnown());
        Assert.True(((ResponseStatus)1145).IsKnown());
    }

    [Theory]
    [InlineData(22u)]
    [InlineData(999u)]
    [InlineData(1000u)]
    [InlineData(1131u)]
    [InlineData(1153u)]
    [InlineData(65535u)]
    public void UnknownResponseStatusIsRejectedWithoutThrowing(uint value)
    {
        var status = (ResponseStatus)value;

        Assert.False(status.IsKnown());
        Assert.Equal(value, (uint)status);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(33u)]
    [InlineData(255u)]
    public void UnknownOpcodeIsRejectedWithoutThrowing(uint value)
    {
        var opcode = (Opcode)value;

        Assert.False(opcode.IsKnown());
        Assert.Equal(value, (uint)opcode);
    }

    [Fact]
    public void OnlyTheCoreProviderHasNoCrypto()
    {
        Assert.False(ProviderId.Core.SupportsCrypto());
        Assert.True(ProviderId.MbedCrypto.SupportsCrypto());
        Assert.True(ProviderId.CryptoAuthLib.SupportsCrypto());
        Assert.False(((ProviderId)200).SupportsCrypto());
    }

    /// <summary>
    /// An unknown value survives a round trip through its wire representation. The client must
    /// carry a value that it does not know rather than reject it.
    /// </summary>
    [Fact]
    public void UnknownValuesRoundTripThroughTheirWireRepresentation()
    {
        Assert.Equal(0xDEADu, (uint)(Opcode)0xDEADu);
        Assert.Equal((ushort)4242, (ushort)(ResponseStatus)4242);
        Assert.Equal((byte)200, (byte)(ProviderId)200);
        Assert.Equal((byte)9, (byte)(AuthType)9);
    }

    [Fact]
    public void BodyTypeOnlyKnowsProtobufAndItIsZero()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(BodyType)));
        Assert.Equal((byte)0, (byte)BodyType.Protobuf);
        Assert.True(BodyType.Protobuf.IsKnown());
        Assert.False(((BodyType)1).IsKnown());
        Assert.False(typeof(BodyType).IsPublic);
    }

    private static void AssertTable<TEnum, TValue>(
        Dictionary<string, TValue> expected,
        Func<TEnum, TValue> toValue)
        where TEnum : struct, Enum
        where TValue : struct
    {
        var actual = Enum.GetValues<TEnum>()
            .ToDictionary(v => v.ToString(), toValue, StringComparer.Ordinal);

        Assert.Equal(Rendered(expected), Rendered(actual));

        static List<string> Rendered(Dictionary<string, TValue> table) =>
        [
            .. table.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => FormattableString.Invariant($"{p.Key}={p.Value}")),
        ];
    }
}
