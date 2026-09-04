using System.Globalization;
using Parsec.Client.Algorithms;
using Parsec.Client.Errors;
using Wire = PsaAlgorithm.Algorithm.Types;
using WireAlgorithm = PsaAlgorithm.Algorithm;

namespace Parsec.Client.Protocol;

/// <summary>
/// Carries an <see cref="Algorithm"/> between the public model and the encoding on the wire.
/// </summary>
/// <remarks>
/// The encoding is a tree of one-field messages, and the public model is a small closed
/// hierarchy. Neither shape suits the other, so the mapping is written out rather than derived.
/// A value that the client cannot read back raises <see cref="ParsecProtocolException"/>: unlike
/// an opcode or a status, an algorithm the client does not know has no place to go in the model.
/// </remarks>
internal static class AlgorithmCodec
{
    private const string HashField = "hash algorithm";
    private const string AlgorithmField = "key algorithm";
    private const string NoAlgorithmNamed = "the message names no algorithm";

    /// <summary>
    /// Encodes an algorithm for the wire.
    /// </summary>
    /// <param name="algorithm">The algorithm to encode.</param>
    /// <returns>The message that carries <paramref name="algorithm"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The algorithm carries a value that the specification does not define.
    /// </exception>
    public static WireAlgorithm ToWire(Algorithm algorithm) => algorithm switch
    {
        NoAlgorithm => new WireAlgorithm { None = new Wire.None() },
        HashAlgorithm hash => new WireAlgorithm { Hash = ToWireHash(hash.Hash) },
        CipherAlgorithm cipher => new WireAlgorithm { Cipher = ToWireCipher(cipher.Cipher) },
        MacAlgorithm mac => new WireAlgorithm { Mac = ToWireMac(mac) },
        AeadAlgorithm aead => new WireAlgorithm { Aead = ToWireAead(aead) },
        SignatureAlgorithm signature =>
            new WireAlgorithm { AsymmetricSignature = ToWireSignature(signature) },
        EncryptionAlgorithm encryption =>
            new WireAlgorithm { AsymmetricEncryption = ToWireEncryption(encryption) },
        KeyAgreementAlgorithm agreement =>
            new WireAlgorithm { KeyAgreement = ToWireAgreement(agreement) },
        KeyDerivationAlgorithm derivation =>
            new WireAlgorithm { KeyDerivation = ToWireDerivation(derivation) },
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null),
    };

    /// <summary>
    /// Reads an algorithm that the service sent.
    /// </summary>
    /// <param name="operation">The operation that answered, for the message of a fault.</param>
    /// <param name="wire">The message that carries the algorithm.</param>
    /// <returns>The algorithm that <paramref name="wire"/> carries.</returns>
    /// <exception cref="ParsecProtocolException">The client cannot read the algorithm back.</exception>
    public static Algorithm FromWire(Opcode operation, WireAlgorithm? wire)
    {
        if (wire is null)
        {
            throw ParsecProtocolException.UnreadableField(
                operation,
                AlgorithmField,
                "the message carries no algorithm");
        }

        return wire.VariantCase switch
        {
            WireAlgorithm.VariantOneofCase.None_ => Algorithm.None,
            WireAlgorithm.VariantOneofCase.Hash => new HashAlgorithm(FromWireHash(operation, wire.Hash)),
            WireAlgorithm.VariantOneofCase.Cipher =>
                new CipherAlgorithm(FromWireCipher(operation, wire.Cipher)),
            WireAlgorithm.VariantOneofCase.Mac => FromWireMac(operation, wire.Mac),
            WireAlgorithm.VariantOneofCase.Aead => FromWireAead(operation, wire.Aead),
            WireAlgorithm.VariantOneofCase.AsymmetricSignature =>
                FromWireSignature(operation, wire.AsymmetricSignature),
            WireAlgorithm.VariantOneofCase.AsymmetricEncryption =>
                FromWireEncryption(operation, wire.AsymmetricEncryption),
            WireAlgorithm.VariantOneofCase.KeyAgreement =>
                FromWireAgreement(operation, wire.KeyAgreement),
            WireAlgorithm.VariantOneofCase.KeyDerivation =>
                FromWireDerivation(operation, wire.KeyDerivation),
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                AlgorithmField,
                NoAlgorithmNamed),
        };
    }

    private static Wire.Hash ToWireHash(Hash hash) =>
        hash is >= Hash.None and <= Hash.Sha3512
            ? (Wire.Hash)hash
            : throw new ArgumentOutOfRangeException(nameof(hash), hash, null);

    private static Hash FromWireHash(Opcode operation, Wire.Hash hash) =>
        hash is >= Wire.Hash.None and <= Wire.Hash.Sha3512
            ? (Hash)hash
            : throw ParsecProtocolException.UnreadableField(operation, HashField, Number(hash));

    private static Wire.Cipher ToWireCipher(Cipher cipher) =>
        cipher is >= Cipher.None and <= Cipher.CbcPkcs7
            ? (Wire.Cipher)cipher
            : throw new ArgumentOutOfRangeException(nameof(cipher), cipher, null);

    private static Cipher FromWireCipher(Opcode operation, Wire.Cipher cipher) =>
        cipher is >= Wire.Cipher.None and <= Wire.Cipher.CbcPkcs7
            ? (Cipher)cipher
            : throw ParsecProtocolException.UnreadableField(operation, "cipher mode", Number(cipher));

    private static Wire.Mac ToWireMac(MacAlgorithm mac)
    {
        var full = mac.Kind switch
        {
            MacKind.Hmac => new Wire.Mac.Types.FullLength
            {
                Hmac = new Wire.Mac.Types.FullLength.Types.Hmac { HashAlg = ToWireHash(mac.Hash) },
            },
            MacKind.CbcMac => new Wire.Mac.Types.FullLength
            {
                CbcMac = new Wire.Mac.Types.FullLength.Types.CbcMac(),
            },
            MacKind.Cmac => new Wire.Mac.Types.FullLength
            {
                Cmac = new Wire.Mac.Types.FullLength.Types.Cmac(),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mac), mac.Kind, null),
        };

        return mac.Length is { } length
            ? new Wire.Mac
            {
                Truncated = new Wire.Mac.Types.Truncated { MacAlg = full, MacLength = length },
            }
            : new Wire.Mac { FullLength = full };
    }

    private static MacAlgorithm FromWireMac(Opcode operation, Wire.Mac mac) => mac.VariantCase switch
    {
        Wire.Mac.VariantOneofCase.FullLength => FromWireFullLengthMac(operation, mac.FullLength),
        Wire.Mac.VariantOneofCase.Truncated =>
            FromWireFullLengthMac(operation, mac.Truncated.MacAlg).Truncate(mac.Truncated.MacLength),
        _ => throw ParsecProtocolException.UnreadableField(
            operation,
            "message authentication code",
            "the message names no construction"),
    };

    private static MacAlgorithm FromWireFullLengthMac(Opcode operation, Wire.Mac.Types.FullLength full) =>
        full.VariantCase switch
        {
            Wire.Mac.Types.FullLength.VariantOneofCase.Hmac =>
                MacAlgorithm.Hmac(FromWireHash(operation, full.Hmac.HashAlg)),
            Wire.Mac.Types.FullLength.VariantOneofCase.CbcMac => MacAlgorithm.CbcMac,
            Wire.Mac.Types.FullLength.VariantOneofCase.Cmac => MacAlgorithm.Cmac,
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                "message authentication code",
                "the message names no construction"),
        };

    private static Wire.Aead ToWireAead(AeadAlgorithm aead)
    {
        // The constructor of AeadAlgorithm is private and every factory rejects a value the
        // specification does not define, so the value here is always one the wire accepts. A
        // range check would be code that no input can reach.
        var kind = (Wire.Aead.Types.AeadWithDefaultLengthTag)aead.Aead;

        return aead.TagLength is { } length
            ? new Wire.Aead
            {
                AeadWithShortenedTag = new Wire.Aead.Types.AeadWithShortenedTag
                {
                    AeadAlg = kind,
                    TagLength = length,
                },
            }
            : new Wire.Aead { AeadWithDefaultLengthTag = kind };
    }

    private static AeadAlgorithm FromWireAead(Opcode operation, Wire.Aead aead) => aead.VariantCase switch
    {
        Wire.Aead.VariantOneofCase.AeadWithDefaultLengthTag =>
            FromWireAeadKind(operation, aead.AeadWithDefaultLengthTag),
        Wire.Aead.VariantOneofCase.AeadWithShortenedTag =>
            FromWireAeadKind(operation, aead.AeadWithShortenedTag.AeadAlg)
                .WithTagLength(aead.AeadWithShortenedTag.TagLength),
        _ => throw ParsecProtocolException.UnreadableField(
            operation,
            "authenticated encryption",
            NoAlgorithmNamed),
    };

    private static AeadAlgorithm FromWireAeadKind(
        Opcode operation,
        Wire.Aead.Types.AeadWithDefaultLengthTag kind) =>
        kind is > Wire.Aead.Types.AeadWithDefaultLengthTag.None
            and <= Wire.Aead.Types.AeadWithDefaultLengthTag.Chacha20Poly1305
            ? AeadAlgorithm.FromAead((Aead)kind)
            : throw ParsecProtocolException.UnreadableField(
                operation,
                "authenticated encryption",
                Number(kind));

    private static Wire.AsymmetricSignature ToWireSignature(SignatureAlgorithm signature) => signature.Kind switch
    {
        SignatureKind.RsaPkcs1v15Sign => new Wire.AsymmetricSignature
        {
            RsaPkcs1V15Sign = new Wire.AsymmetricSignature.Types.RsaPkcs1v15Sign
            {
                HashAlg = ToWireSignHash(signature.Hash),
            },
        },
        SignatureKind.RsaPkcs1v15SignRaw => new Wire.AsymmetricSignature
        {
            RsaPkcs1V15SignRaw = new Wire.AsymmetricSignature.Types.RsaPkcs1v15SignRaw(),
        },
        SignatureKind.RsaPss => new Wire.AsymmetricSignature
        {
            RsaPss = new Wire.AsymmetricSignature.Types.RsaPss { HashAlg = ToWireSignHash(signature.Hash) },
        },
        SignatureKind.Ecdsa => new Wire.AsymmetricSignature
        {
            Ecdsa = new Wire.AsymmetricSignature.Types.Ecdsa { HashAlg = ToWireSignHash(signature.Hash) },
        },
        SignatureKind.EcdsaAny => new Wire.AsymmetricSignature
        {
            EcdsaAny = new Wire.AsymmetricSignature.Types.EcdsaAny(),
        },
        SignatureKind.DeterministicEcdsa => new Wire.AsymmetricSignature
        {
            DeterministicEcdsa = new Wire.AsymmetricSignature.Types.DeterministicEcdsa
            {
                HashAlg = ToWireSignHash(signature.Hash),
            },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(signature), signature.Kind, null),
    };

    private static SignatureAlgorithm FromWireSignature(
        Opcode operation,
        Wire.AsymmetricSignature signature) => signature.VariantCase switch
        {
            Wire.AsymmetricSignature.VariantOneofCase.RsaPkcs1V15Sign => SignatureAlgorithm.RsaPkcs1v15Sign(
                FromWireSignHash(operation, signature.RsaPkcs1V15Sign.HashAlg)),
            Wire.AsymmetricSignature.VariantOneofCase.RsaPkcs1V15SignRaw =>
                SignatureAlgorithm.RsaPkcs1v15SignRaw,
            Wire.AsymmetricSignature.VariantOneofCase.RsaPss => SignatureAlgorithm.RsaPss(
                FromWireSignHash(operation, signature.RsaPss.HashAlg)),
            Wire.AsymmetricSignature.VariantOneofCase.Ecdsa => SignatureAlgorithm.Ecdsa(
                FromWireSignHash(operation, signature.Ecdsa.HashAlg)),
            Wire.AsymmetricSignature.VariantOneofCase.EcdsaAny => SignatureAlgorithm.EcdsaAny,
            Wire.AsymmetricSignature.VariantOneofCase.DeterministicEcdsa =>
                SignatureAlgorithm.DeterministicEcdsa(
                    FromWireSignHash(operation, signature.DeterministicEcdsa.HashAlg)),
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                "signature algorithm",
                NoAlgorithmNamed),
        };

    private static Wire.AsymmetricSignature.Types.SignHash ToWireSignHash(SignHash hash) => hash.Hash is { } value
        ? new Wire.AsymmetricSignature.Types.SignHash { Specific = ToWireHash(value) }
        : new Wire.AsymmetricSignature.Types.SignHash
        {
            Any = new Wire.AsymmetricSignature.Types.SignHash.Types.Any(),
        };

    private static SignHash FromWireSignHash(
        Opcode operation,
        Wire.AsymmetricSignature.Types.SignHash hash) => hash.VariantCase switch
        {
            Wire.AsymmetricSignature.Types.SignHash.VariantOneofCase.Any => SignHash.Any,
            Wire.AsymmetricSignature.Types.SignHash.VariantOneofCase.Specific =>
                SignHash.FromHash(FromWireHash(operation, hash.Specific)),
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                HashField,
                "the message names neither a hash nor the value that accepts any hash"),
        };

    private static Wire.AsymmetricEncryption ToWireEncryption(EncryptionAlgorithm encryption) => encryption.Kind switch
    {
        EncryptionKind.RsaPkcs1v15Crypt => new Wire.AsymmetricEncryption
        {
            RsaPkcs1V15Crypt = new Wire.AsymmetricEncryption.Types.RsaPkcs1v15Crypt(),
        },
        EncryptionKind.RsaOaep => new Wire.AsymmetricEncryption
        {
            RsaOaep = new Wire.AsymmetricEncryption.Types.RsaOaep { HashAlg = ToWireHash(encryption.Hash) },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(encryption), encryption.Kind, null),
    };

    private static EncryptionAlgorithm FromWireEncryption(
        Opcode operation,
        Wire.AsymmetricEncryption encryption) => encryption.VariantCase switch
        {
            Wire.AsymmetricEncryption.VariantOneofCase.RsaPkcs1V15Crypt =>
                EncryptionAlgorithm.RsaPkcs1v15Crypt,
            Wire.AsymmetricEncryption.VariantOneofCase.RsaOaep => EncryptionAlgorithm.RsaOaep(
                FromWireHash(operation, encryption.RsaOaep.HashAlg)),
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                "asymmetric encryption",
                NoAlgorithmNamed),
        };

    private static Wire.KeyAgreement ToWireAgreement(KeyAgreementAlgorithm agreement)
    {
        // As with the authenticated encryption above, the constructor is private and the two
        // factories are the only way in, so the value here is always one the wire accepts.
        var raw = (Wire.KeyAgreement.Types.Raw)agreement.Kind;

        return agreement.Derivation is { } derivation
            ? new Wire.KeyAgreement
            {
                WithKeyDerivation = new Wire.KeyAgreement.Types.WithKeyDerivation
                {
                    KaAlg = raw,
                    KdfAlg = ToWireDerivation(derivation),
                },
            }
            : new Wire.KeyAgreement { Raw = raw };
    }

    private static KeyAgreementAlgorithm FromWireAgreement(
        Opcode operation,
        Wire.KeyAgreement agreement) => agreement.VariantCase switch
        {
            Wire.KeyAgreement.VariantOneofCase.Raw => FromWireAgreementKind(operation, agreement.Raw),
            Wire.KeyAgreement.VariantOneofCase.WithKeyDerivation =>
                FromWireAgreementKind(operation, agreement.WithKeyDerivation.KaAlg)
                    .WithDerivation(FromWireDerivation(operation, agreement.WithKeyDerivation.KdfAlg)),
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                "key agreement",
                NoAlgorithmNamed),
        };

    private static KeyAgreementAlgorithm FromWireAgreementKind(
        Opcode operation,
        Wire.KeyAgreement.Types.Raw raw) => raw switch
        {
            Wire.KeyAgreement.Types.Raw.Ffdh => KeyAgreementAlgorithm.Ffdh,
            Wire.KeyAgreement.Types.Raw.Ecdh => KeyAgreementAlgorithm.Ecdh,
            _ => throw ParsecProtocolException.UnreadableField(operation, "key agreement", Number(raw)),
        };

    private static Wire.KeyDerivation ToWireDerivation(KeyDerivationAlgorithm derivation) => derivation.Kind switch
    {
        KeyDerivationKind.Hkdf => new Wire.KeyDerivation
        {
            Hkdf = new Wire.KeyDerivation.Types.Hkdf { HashAlg = ToWireHash(derivation.Hash) },
        },
        KeyDerivationKind.Tls12Prf => new Wire.KeyDerivation
        {
            Tls12Prf = new Wire.KeyDerivation.Types.Tls12Prf { HashAlg = ToWireHash(derivation.Hash) },
        },
        KeyDerivationKind.Tls12PskToMs => new Wire.KeyDerivation
        {
            Tls12PskToMs = new Wire.KeyDerivation.Types.Tls12PskToMs
            {
                HashAlg = ToWireHash(derivation.Hash),
            },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(derivation), derivation.Kind, null),
    };

    private static KeyDerivationAlgorithm FromWireDerivation(
        Opcode operation,
        Wire.KeyDerivation derivation) => derivation.VariantCase switch
        {
            Wire.KeyDerivation.VariantOneofCase.Hkdf =>
                KeyDerivationAlgorithm.Hkdf(FromWireHash(operation, derivation.Hkdf.HashAlg)),
            Wire.KeyDerivation.VariantOneofCase.Tls12Prf =>
                KeyDerivationAlgorithm.Tls12Prf(FromWireHash(operation, derivation.Tls12Prf.HashAlg)),
            Wire.KeyDerivation.VariantOneofCase.Tls12PskToMs =>
                KeyDerivationAlgorithm.Tls12PskToMs(FromWireHash(operation, derivation.Tls12PskToMs.HashAlg)),
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                "key derivation",
                "the message names no function"),
        };

    private static string Number<T>(T value)
        where T : struct, Enum =>
        string.Create(CultureInfo.InvariantCulture, $"the value {Convert.ToInt32(value, CultureInfo.InvariantCulture)}");
}
