using Google.Protobuf;
using Parsec.Client.Algorithms;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;
using Wire = PsaAlgorithm.Algorithm.Types;
using WireAlgorithm = PsaAlgorithm.Algorithm;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the mapping between the public algorithm model and the encoding on the wire.
/// </summary>
/// <remarks>
/// The two shapes are written out by hand, so the risk is a variant that the mapping forgets or
/// sends to the wrong place. A round trip catches both: a forgotten variant raises, and a
/// misplaced one comes back as something else.
/// </remarks>
public sealed class AlgorithmCodecTests
{
    public static TheoryData<Algorithm> EveryVariant()
    {
        var data = new TheoryData<Algorithm>
        {
            Algorithm.None,
            MacAlgorithm.CbcMac,
            MacAlgorithm.Cmac,
            MacAlgorithm.Hmac(Hash.Sha256),
            MacAlgorithm.Hmac(Hash.Sha512).Truncate(16),
            MacAlgorithm.Cmac.Truncate(8),
            AeadAlgorithm.Ccm,
            AeadAlgorithm.Gcm,
            AeadAlgorithm.ChaCha20Poly1305,
            AeadAlgorithm.Gcm.WithTagLength(12),
            SignatureAlgorithm.RsaPkcs1v15SignRaw,
            SignatureAlgorithm.EcdsaAny,
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256),
            SignatureAlgorithm.RsaPkcs1v15Sign(SignHash.Any),
            SignatureAlgorithm.RsaPss(Hash.Sha384),
            SignatureAlgorithm.Ecdsa(Hash.Sha256),
            SignatureAlgorithm.Ecdsa(SignHash.Any),
            SignatureAlgorithm.DeterministicEcdsa(Hash.Sha512),
            EncryptionAlgorithm.RsaPkcs1v15Crypt,
            EncryptionAlgorithm.RsaOaep(Hash.Sha256),
            KeyAgreementAlgorithm.Ffdh,
            KeyAgreementAlgorithm.Ecdh,
            KeyAgreementAlgorithm.Ecdh.WithDerivation(KeyDerivationAlgorithm.Hkdf(Hash.Sha256)),
            KeyDerivationAlgorithm.Hkdf(Hash.Sha256),
            KeyDerivationAlgorithm.Tls12Prf(Hash.Sha384),
            KeyDerivationAlgorithm.Tls12PskToMs(Hash.Sha256),
        };

        foreach (var hash in Enum.GetValues<Hash>())
        {
            data.Add(Algorithm.FromHash(hash));
        }

        foreach (var cipher in Enum.GetValues<Cipher>())
        {
            data.Add(Algorithm.FromCipher(cipher));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryVariant))]
    public void EveryVariantSurvivesARoundTrip(Algorithm algorithm)
    {
        var wire = AlgorithmCodec.ToWire(algorithm);

        var back = AlgorithmCodec.FromWire(Opcode.ListKeys, wire);

        Assert.Equal(algorithm, back);
    }

    [Theory]
    [MemberData(nameof(EveryVariant))]
    public void EveryVariantSurvivesTheEncoderAsWell(Algorithm algorithm)
    {
        // The round trip above never leaves memory. This one puts the message through the bytes,
        // which is where a field number that two variants share would show up.
        var bytes = AlgorithmCodec.ToWire(algorithm).ToByteArray();

        var back = AlgorithmCodec.FromWire(Opcode.ListKeys, WireAlgorithm.Parser.ParseFrom(bytes));

        Assert.Equal(algorithm, back);
    }

    [Fact]
    public void AMessageThatNamesNoAlgorithmIsAProtocolFault()
    {
        var fault = Assert.Throws<ParsecProtocolException>(
            () => AlgorithmCodec.FromWire(Opcode.ListKeys, new WireAlgorithm()));

        Assert.Contains("names no algorithm", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMissingMessageIsAProtocolFault()
    {
        var fault = Assert.Throws<ParsecProtocolException>(
            () => AlgorithmCodec.FromWire(Opcode.ListKeys, null));

        Assert.Contains("carries no algorithm", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AHashTheClientDoesNotKnowIsAProtocolFault()
    {
        // The service may add a hash. The client cannot put it in the model, so it says so rather
        // than reporting a hash that the service never named.
        var wire = new WireAlgorithm { Hash = (Wire.Hash)99 };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => AlgorithmCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("hash algorithm", fault.Message, StringComparison.Ordinal);
        Assert.Contains("the value 99", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACipherTheClientDoesNotKnowIsAProtocolFault()
    {
        var wire = new WireAlgorithm { Cipher = (Wire.Cipher)42 };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => AlgorithmCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("cipher mode", fault.Message, StringComparison.Ordinal);
        Assert.Contains("the value 42", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AKeyAgreementTheClientDoesNotKnowIsAProtocolFault()
    {
        var wire = new WireAlgorithm { KeyAgreement = new Wire.KeyAgreement { Raw = (Wire.KeyAgreement.Types.Raw)7 } };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => AlgorithmCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("key agreement", fault.Message, StringComparison.Ordinal);
        Assert.Contains("the value 7", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueBelowTheFirstOneTheClientKnowsIsAlsoAProtocolFault()
    {
        // A cast can produce a negative value as easily as a large one, and the lower end of the
        // range is the one a bounds check is likely to get wrong.
        var wire = new WireAlgorithm { Cipher = (Wire.Cipher)(-1) };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => AlgorithmCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("the value -1", fault.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void AnAuthenticatedEncryptionTheClientDoesNotKnowIsAProtocolFault(int value)
    {
        // Zero is the variant the specification tells everyone not to use, so it has to be
        // refused the same way an unknown one is.
        var wire = new WireAlgorithm
        {
            Aead = new Wire.Aead
            {
                AeadWithDefaultLengthTag = (Wire.Aead.Types.AeadWithDefaultLengthTag)value,
            },
        };

        var fault = Assert.Throws<ParsecProtocolException>(
            () => AlgorithmCodec.FromWire(Opcode.ListKeys, wire));

        Assert.Contains("authenticated encryption", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACipherTheSpecificationDoesNotDefineIsRejectedBeforeItReachesTheWire()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AlgorithmCodec.ToWire(Algorithm.FromCipher((Cipher)(-1))));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AlgorithmCodec.ToWire(Algorithm.FromCipher((Cipher)42)));
    }

    [Fact]
    public void AnAuthenticatedEncryptionTheSpecificationDoesNotDefineIsRefusedAtTheFactory()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AeadAlgorithm.FromAead(Aead.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => AeadAlgorithm.FromAead((Aead)9));
    }

    [Fact]
    public void ANullDerivationIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => KeyAgreementAlgorithm.Ecdh.WithDerivation(null!));
    }

    [Fact]
    public void AHashTheSpecificationDoesNotDefineIsRejectedBeforeItReachesTheWire()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AlgorithmCodec.ToWire(Algorithm.FromHash((Hash)99)));
    }

    [Fact]
    public void TruncatingToZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MacAlgorithm.Cmac.Truncate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AeadAlgorithm.Gcm.WithTagLength(0));
    }

    [Fact]
    public void SignHashCarriesTheHashOrAcceptsAny()
    {
        Assert.Null(SignHash.Any.Hash);
        Assert.Equal(Hash.Sha256, SignHash.FromHash(Hash.Sha256).Hash);
        Assert.Equal(SignHash.FromHash(Hash.Sha256), (SignHash)Hash.Sha256);
    }

    [Fact]
    public void TheFamilyTypesAreDistinctEvenWhenTheyCarryTheSameHash()
    {
        // Every family is its own type, so a value of one family never equals a value of
        // another. This is what lets an operation take the family it needs and nothing else.
        Assert.NotEqual<Algorithm>(
            KeyDerivationAlgorithm.Hkdf(Hash.Sha256),
            KeyDerivationAlgorithm.Tls12Prf(Hash.Sha256));
        Assert.NotEqual<Algorithm>(Algorithm.FromHash(Hash.Sha256), MacAlgorithm.Hmac(Hash.Sha256));
    }
}
