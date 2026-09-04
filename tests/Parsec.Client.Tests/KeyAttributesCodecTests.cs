using Google.Protobuf;
using Parsec.Client.Algorithms;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;
using WireAttributes = PsaKeyAttributes.KeyAttributes;
using WireKeyType = PsaKeyAttributes.KeyType;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the mapping between the public key attributes and the encoding on the wire.
/// </summary>
public sealed class KeyAttributesCodecTests
{
    public static TheoryData<KeyType> EveryKeyType()
    {
        var data = new TheoryData<KeyType>
        {
            KeyType.RawData,
            KeyType.Hmac,
            KeyType.Derive,
            KeyType.Aes,
            KeyType.Des,
            KeyType.Camellia,
            KeyType.Arc4,
            KeyType.ChaCha20,
            KeyType.RsaPublicKey,
            KeyType.RsaKeyPair,
            KeyType.DhKeyPair(DhFamily.Rfc7919),
            KeyType.DhPublicKey(DhFamily.Rfc7919),
        };

        foreach (var family in Enum.GetValues<EccFamily>())
        {
            data.Add(KeyType.EccKeyPair(family));
            data.Add(KeyType.EccPublicKey(family));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryKeyType))]
    public void EveryKeyTypeSurvivesARoundTrip(KeyType type)
    {
        var attributes = new KeyAttributes(type, 256, new KeyPolicy(KeyUsages.None, Algorithm.None));

        var back = RoundTrip(attributes);

        Assert.Equal(type, back.Type);
    }

    [Fact]
    public void EveryUsageFlagSurvivesARoundTripOnItsOwn()
    {
        // The ten flags travel as ten separate booleans. A mapping that pairs one flag with the
        // wrong boolean still passes a test that sets them all, so each one goes on its own.
        foreach (var flag in Enum.GetValues<KeyUsages>())
        {
            var attributes = new KeyAttributes(
                KeyType.Aes,
                128,
                new KeyPolicy(flag, Algorithm.None));

            Assert.Equal(flag, RoundTrip(attributes).Policy.Usage);
        }
    }

    [Fact]
    public void TheFlagsSurviveTogether()
    {
        var every = Enum.GetValues<KeyUsages>().Aggregate(KeyUsages.None, (all, flag) => all | flag);
        var attributes = new KeyAttributes(KeyType.Aes, 128, new KeyPolicy(every, Algorithm.None));

        Assert.Equal(every, RoundTrip(attributes).Policy.Usage);
    }

    [Fact]
    public void TheWholeShapeSurvivesARoundTrip()
    {
        var attributes = new KeyAttributes(
            KeyType.EccKeyPair(EccFamily.SecpR1),
            256,
            new KeyPolicy(
                KeyUsages.SignHash | KeyUsages.VerifyHash | KeyUsages.Export,
                SignatureAlgorithm.DeterministicEcdsa(Hash.Sha384)));

        Assert.Equal(attributes, RoundTrip(attributes));
    }

    [Fact]
    public void AKeySizeOfZeroSurvivesARoundTrip()
    {
        // Proto3 leaves a zero out of the bytes, so the field arrives unset and must read back
        // as zero rather than as something the encoder invented.
        var attributes = new KeyAttributes(KeyType.Aes, 0, new KeyPolicy(KeyUsages.None, Algorithm.None));

        Assert.Equal(0u, RoundTrip(attributes).Bits);
    }

    [Fact]
    public void AMessageThatNamesNoKeyTypeIsAProtocolFault()
    {
        var wire = new WireAttributes { KeyType = new WireKeyType(), KeyPolicy = new PsaKeyAttributes.KeyPolicy() };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => KeyAttributesCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("names no type", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMessageWithNoPolicyIsAProtocolFault()
    {
        var wire = new WireAttributes { KeyType = new WireKeyType { Aes = new WireKeyType.Types.Aes() } };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => KeyAttributesCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("key policy", fault.Message, StringComparison.Ordinal);
        Assert.Contains("carries no policy", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACurveFamilyTheClientDoesNotKnowIsAProtocolFault()
    {
        var wire = new WireAttributes
        {
            KeyType = new WireKeyType
            {
                EccKeyPair = new WireKeyType.Types.EccKeyPair
                {
                    CurveFamily = (WireKeyType.Types.EccFamily)55,
                },
            },
            KeyPolicy = new PsaKeyAttributes.KeyPolicy(),
        };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => KeyAttributesCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("curve family", fault.Message, StringComparison.Ordinal);
        Assert.Contains("the value 55", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APolicyWithNoAlgorithmReadsBackAsNone()
    {
        // An unset algorithm is not the same fault as an algorithm that names nothing. The
        // service leaves the field out for a key that binds to no algorithm at all.
        var wire = new WireAttributes
        {
            KeyType = new WireKeyType { Aes = new WireKeyType.Types.Aes() },
            KeyPolicy = new PsaKeyAttributes.KeyPolicy(),
        };

        Assert.Equal(Algorithm.None, KeyAttributesCodec.FromWire(Opcode.ListKeys, wire).Policy.Algorithm);
    }

    [Fact]
    public void AKeyTypeTheSpecificationDoesNotDefineIsRejectedBeforeItReachesTheWire()
    {
        var attributes = new KeyAttributes(
            KeyType.EccKeyPair((EccFamily)77),
            256,
            new KeyPolicy(KeyUsages.None, Algorithm.None));

        Assert.Throws<ArgumentOutOfRangeException>(() => KeyAttributesCodec.ToWire(attributes));
    }

    [Fact]
    public void AGroupFamilyTheClientDoesNotKnowIsAProtocolFault()
    {
        // RFC 7919 is the only family the specification defines, and it is the zero value, so
        // anything else on the wire has to be refused rather than folded into it.
        var wire = new WireAttributes
        {
            KeyType = new WireKeyType
            {
                DhKeyPair = new WireKeyType.Types.DhKeyPair
                {
                    GroupFamily = (WireKeyType.Types.DhFamily)9,
                },
            },
            KeyPolicy = new PsaKeyAttributes.KeyPolicy(),
        };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => KeyAttributesCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("group family", fault.Message, StringComparison.Ordinal);
        Assert.Contains("the value 9", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGroupFamilyTheSpecificationDoesNotDefineIsRejectedBeforeItReachesTheWire()
    {
        var attributes = new KeyAttributes(
            KeyType.DhPublicKey((DhFamily)9),
            256,
            new KeyPolicy(KeyUsages.None, Algorithm.None));

        Assert.Throws<ArgumentOutOfRangeException>(() => KeyAttributesCodec.ToWire(attributes));
    }

    [Fact]
    public void ACurveFamilyBelowTheFirstOneTheClientKnowsIsAProtocolFault()
    {
        var wire = new WireAttributes
        {
            KeyType = new WireKeyType
            {
                EccPublicKey = new WireKeyType.Types.EccPublicKey
                {
                    CurveFamily = (WireKeyType.Types.EccFamily)(-1),
                },
            },
            KeyPolicy = new PsaKeyAttributes.KeyPolicy(),
        };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => KeyAttributesCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("the value -1", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttributesAreValueEqual()
    {
        var first = new KeyAttributes(
            KeyType.RsaKeyPair,
            2048,
            new KeyPolicy(KeyUsages.SignHash, SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256)));
        var second = new KeyAttributes(
            KeyType.RsaKeyPair,
            2048,
            new KeyPolicy(KeyUsages.SignHash, SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256)));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ANullTypeOrPolicyIsRefused()
    {
        Assert.Throws<ArgumentNullException>(
            () => new KeyAttributes(null!, 0, new KeyPolicy(KeyUsages.None, Algorithm.None)));
        Assert.Throws<ArgumentNullException>(() => new KeyAttributes(KeyType.Aes, 0, null!));
        Assert.Throws<ArgumentNullException>(() => new KeyPolicy(KeyUsages.None, null!));
    }

    private static KeyAttributes RoundTrip(KeyAttributes attributes)
    {
        var bytes = KeyAttributesCodec.ToWire(attributes).ToByteArray();

        return KeyAttributesCodec.FromWire(Opcode.ListKeys, WireAttributes.Parser.ParseFrom(bytes));
    }
}
