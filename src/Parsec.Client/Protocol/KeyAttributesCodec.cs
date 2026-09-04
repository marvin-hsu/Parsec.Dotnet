using System.Globalization;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using WireAttributes = PsaKeyAttributes.KeyAttributes;
using WireKeyType = PsaKeyAttributes.KeyType;
using WirePolicy = PsaKeyAttributes.KeyPolicy;
using WireUsage = PsaKeyAttributes.UsageFlags;

namespace Parsec.Client.Protocol;

/// <summary>
/// Carries a <see cref="KeyAttributes"/> between the public model and the encoding on the wire.
/// </summary>
/// <remarks>
/// The ten usage flags travel as ten separate booleans and arrive as one
/// <see cref="KeyUsages"/> value. Everything else maps one for one, because the numbers of the
/// public enumerations were chosen to match the wire.
/// </remarks>
internal static class KeyAttributesCodec
{
    private const string TypeField = "key type";

    /// <summary>
    /// Encodes a set of attributes for the wire.
    /// </summary>
    /// <param name="attributes">The attributes to encode.</param>
    /// <returns>The message that carries <paramref name="attributes"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The attributes carry a value that the specification does not define.
    /// </exception>
    public static WireAttributes ToWire(KeyAttributes attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        return new WireAttributes
        {
            KeyType = ToWireKeyType(attributes.Type),
            KeyBits = attributes.Bits,
            KeyPolicy = new WirePolicy
            {
                KeyUsageFlags = ToWireUsage(attributes.Policy.Usage),
                KeyAlgorithm = AlgorithmCodec.ToWire(attributes.Policy.Algorithm),
            },
        };
    }

    /// <summary>
    /// Reads a set of attributes that the service sent.
    /// </summary>
    /// <param name="operation">The operation that answered, for the message of a fault.</param>
    /// <param name="wire">The message that carries the attributes.</param>
    /// <returns>The attributes that <paramref name="wire"/> carries.</returns>
    /// <exception cref="ParsecProtocolException">The client cannot read the attributes back.</exception>
    public static KeyAttributes FromWire(Opcode operation, WireAttributes? wire)
    {
        if (wire is null)
        {
            throw ParsecProtocolException.UnreadableField(
                operation,
                "key",
                "the message carries no attributes");
        }

        if (wire.KeyPolicy is null)
        {
            throw ParsecProtocolException.UnreadableField(
                operation,
                "key policy",
                "the message carries no policy");
        }

        var algorithm = wire.KeyPolicy.KeyAlgorithm is { } wireAlgorithm
            ? AlgorithmCodec.FromWire(operation, wireAlgorithm)
            : Algorithms.Algorithm.None;

        return new KeyAttributes(
            FromWireKeyType(operation, wire.KeyType),
            wire.KeyBits,
            new KeyPolicy(FromWireUsage(wire.KeyPolicy.KeyUsageFlags), algorithm));
    }

    // The generated properties for DES and ARC4 carry ObsoleteAttribute, because the
    // specification deprecates both. A client that cannot name them cannot read back a key that
    // an older application created, so the mapping keeps them and the public model documents
    // that they are deprecated rather than hiding them.
#pragma warning disable CS0612
    private static WireKeyType ToWireKeyType(KeyType type) => type.Kind switch
    {
        KeyTypeKind.RawData => new WireKeyType { RawData = new WireKeyType.Types.RawData() },
        KeyTypeKind.Hmac => new WireKeyType { Hmac = new WireKeyType.Types.Hmac() },
        KeyTypeKind.Derive => new WireKeyType { Derive = new WireKeyType.Types.Derive() },
        KeyTypeKind.Aes => new WireKeyType { Aes = new WireKeyType.Types.Aes() },
        KeyTypeKind.Des => new WireKeyType { Des = new WireKeyType.Types.Des() },
        KeyTypeKind.Camellia => new WireKeyType { Camellia = new WireKeyType.Types.Camellia() },
        KeyTypeKind.Arc4 => new WireKeyType { Arc4 = new WireKeyType.Types.Arc4() },
        KeyTypeKind.ChaCha20 => new WireKeyType { Chacha20 = new WireKeyType.Types.Chacha20() },
        KeyTypeKind.RsaPublicKey =>
            new WireKeyType { RsaPublicKey = new WireKeyType.Types.RsaPublicKey() },
        KeyTypeKind.RsaKeyPair => new WireKeyType { RsaKeyPair = new WireKeyType.Types.RsaKeyPair() },
        KeyTypeKind.EccKeyPair => new WireKeyType
        {
            EccKeyPair = new WireKeyType.Types.EccKeyPair { CurveFamily = ToWireEcc(type.EccFamily) },
        },
        KeyTypeKind.EccPublicKey => new WireKeyType
        {
            EccPublicKey = new WireKeyType.Types.EccPublicKey { CurveFamily = ToWireEcc(type.EccFamily) },
        },
        KeyTypeKind.DhKeyPair => new WireKeyType
        {
            DhKeyPair = new WireKeyType.Types.DhKeyPair { GroupFamily = ToWireDh(type.DhFamily) },
        },
        KeyTypeKind.DhPublicKey => new WireKeyType
        {
            DhPublicKey = new WireKeyType.Types.DhPublicKey { GroupFamily = ToWireDh(type.DhFamily) },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type.Kind, null),
    };
#pragma warning restore CS0612

    private static KeyType FromWireKeyType(Opcode operation, WireKeyType? type)
    {
        if (type is null)
        {
            throw ParsecProtocolException.UnreadableField(
                operation,
                TypeField,
                "the message carries no type");
        }

        return type.VariantCase switch
        {
            WireKeyType.VariantOneofCase.RawData => KeyType.RawData,
            WireKeyType.VariantOneofCase.Hmac => KeyType.Hmac,
            WireKeyType.VariantOneofCase.Derive => KeyType.Derive,
            WireKeyType.VariantOneofCase.Aes => KeyType.Aes,
            WireKeyType.VariantOneofCase.Des => KeyType.Des,
            WireKeyType.VariantOneofCase.Camellia => KeyType.Camellia,
            WireKeyType.VariantOneofCase.Arc4 => KeyType.Arc4,
            WireKeyType.VariantOneofCase.Chacha20 => KeyType.ChaCha20,
            WireKeyType.VariantOneofCase.RsaPublicKey => KeyType.RsaPublicKey,
            WireKeyType.VariantOneofCase.RsaKeyPair => KeyType.RsaKeyPair,
            WireKeyType.VariantOneofCase.EccKeyPair =>
                KeyType.EccKeyPair(FromWireEcc(operation, type.EccKeyPair.CurveFamily)),
            WireKeyType.VariantOneofCase.EccPublicKey =>
                KeyType.EccPublicKey(FromWireEcc(operation, type.EccPublicKey.CurveFamily)),
            WireKeyType.VariantOneofCase.DhKeyPair =>
                KeyType.DhKeyPair(FromWireDh(operation, type.DhKeyPair.GroupFamily)),
            WireKeyType.VariantOneofCase.DhPublicKey =>
                KeyType.DhPublicKey(FromWireDh(operation, type.DhPublicKey.GroupFamily)),
            _ => throw ParsecProtocolException.UnreadableField(
                operation,
                TypeField,
                "the message names no type"),
        };
    }

    private static WireKeyType.Types.EccFamily ToWireEcc(EccFamily family) =>
        family is >= EccFamily.None and <= EccFamily.Montgomery
            ? (WireKeyType.Types.EccFamily)family
            : throw new ArgumentOutOfRangeException(nameof(family), family, null);

    private static EccFamily FromWireEcc(Opcode operation, WireKeyType.Types.EccFamily family) =>
        family is >= WireKeyType.Types.EccFamily.None and <= WireKeyType.Types.EccFamily.Montgomery
            ? (EccFamily)family
            : throw ParsecProtocolException.UnreadableField(
                operation,
                "curve family",
                string.Create(CultureInfo.InvariantCulture, $"the value {(int)family}"));

    private static WireKeyType.Types.DhFamily ToWireDh(DhFamily family) =>
        family is DhFamily.Rfc7919
            ? WireKeyType.Types.DhFamily.Rfc7919
            : throw new ArgumentOutOfRangeException(nameof(family), family, null);

    private static DhFamily FromWireDh(Opcode operation, WireKeyType.Types.DhFamily family) =>
        family is WireKeyType.Types.DhFamily.Rfc7919
            ? DhFamily.Rfc7919
            : throw ParsecProtocolException.UnreadableField(
                operation,
                "group family",
                string.Create(CultureInfo.InvariantCulture, $"the value {(int)family}"));

    private static WireUsage ToWireUsage(KeyUsages usage) => new()
    {
        Export = usage.HasFlag(KeyUsages.Export),
        Copy = usage.HasFlag(KeyUsages.Copy),
        Cache = usage.HasFlag(KeyUsages.Cache),
        Encrypt = usage.HasFlag(KeyUsages.Encrypt),
        Decrypt = usage.HasFlag(KeyUsages.Decrypt),
        SignMessage = usage.HasFlag(KeyUsages.SignMessage),
        VerifyMessage = usage.HasFlag(KeyUsages.VerifyMessage),
        SignHash = usage.HasFlag(KeyUsages.SignHash),
        VerifyHash = usage.HasFlag(KeyUsages.VerifyHash),
        Derive = usage.HasFlag(KeyUsages.Derive),
    };

    private static KeyUsages FromWireUsage(WireUsage? usage)
    {
        if (usage is null)
        {
            return KeyUsages.None;
        }

        var flags = KeyUsages.None;

        Set(ref flags, usage.Export, KeyUsages.Export);
        Set(ref flags, usage.Copy, KeyUsages.Copy);
        Set(ref flags, usage.Cache, KeyUsages.Cache);
        Set(ref flags, usage.Encrypt, KeyUsages.Encrypt);
        Set(ref flags, usage.Decrypt, KeyUsages.Decrypt);
        Set(ref flags, usage.SignMessage, KeyUsages.SignMessage);
        Set(ref flags, usage.VerifyMessage, KeyUsages.VerifyMessage);
        Set(ref flags, usage.SignHash, KeyUsages.SignHash);
        Set(ref flags, usage.VerifyHash, KeyUsages.VerifyHash);
        Set(ref flags, usage.Derive, KeyUsages.Derive);

        return flags;
    }

    private static void Set(ref KeyUsages flags, bool granted, KeyUsages flag)
    {
        if (granted)
        {
            flags |= flag;
        }
    }
}
