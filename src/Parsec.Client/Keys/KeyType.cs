namespace Parsec.Client.Keys;

/// <summary>
/// States what a key holds, and for a curve or group key which family it belongs to.
/// </summary>
/// <remarks>
/// Build one through a factory. The constructor is private so that a family can never be paired
/// with a type that has no family, which is a shape the service would reject.
/// </remarks>
public sealed record KeyType
{
    private KeyType(KeyTypeKind kind, EccFamily ecc, DhFamily dh)
    {
        Kind = kind;
        EccFamily = ecc;
        DhFamily = dh;
    }

    /// <summary>Gets bytes that carry no structure of their own.</summary>
    public static KeyType RawData { get; } = Plain(KeyTypeKind.RawData);

    /// <summary>Gets a key for a keyed hash.</summary>
    public static KeyType Hmac { get; } = Plain(KeyTypeKind.Hmac);

    /// <summary>Gets a secret that only derives other key material.</summary>
    public static KeyType Derive { get; } = Plain(KeyTypeKind.Derive);

    /// <summary>Gets an AES key.</summary>
    public static KeyType Aes { get; } = Plain(KeyTypeKind.Aes);

    /// <summary>Gets a DES or triple DES key. The specification deprecates it.</summary>
    public static KeyType Des { get; } = Plain(KeyTypeKind.Des);

    /// <summary>Gets a Camellia key.</summary>
    public static KeyType Camellia { get; } = Plain(KeyTypeKind.Camellia);

    /// <summary>Gets an ARC4 key. The specification deprecates it.</summary>
    public static KeyType Arc4 { get; } = Plain(KeyTypeKind.Arc4);

    /// <summary>Gets a ChaCha20 key.</summary>
    public static KeyType ChaCha20 { get; } = Plain(KeyTypeKind.ChaCha20);

    /// <summary>Gets the public half of an RSA key.</summary>
    public static KeyType RsaPublicKey { get; } = Plain(KeyTypeKind.RsaPublicKey);

    /// <summary>Gets both halves of an RSA key.</summary>
    public static KeyType RsaKeyPair { get; } = Plain(KeyTypeKind.RsaKeyPair);

    /// <summary>Gets what the key holds.</summary>
    public KeyTypeKind Kind { get; }

    /// <summary>
    /// Gets the curve family, which carries a value only when <see cref="Kind"/> is
    /// <see cref="KeyTypeKind.EccKeyPair"/> or <see cref="KeyTypeKind.EccPublicKey"/>.
    /// </summary>
    public EccFamily EccFamily { get; }

    /// <summary>
    /// Gets the group family, which is meaningful only when <see cref="Kind"/> is
    /// <see cref="KeyTypeKind.DhKeyPair"/> or <see cref="KeyTypeKind.DhPublicKey"/>.
    /// </summary>
    public DhFamily DhFamily { get; }

    /// <summary>
    /// Builds both halves of an elliptic curve key.
    /// </summary>
    /// <param name="family">The family that the curve belongs to.</param>
    /// <returns>The type of a key pair on <paramref name="family"/>.</returns>
    public static KeyType EccKeyPair(EccFamily family) =>
        new(KeyTypeKind.EccKeyPair, family, DhFamily.Rfc7919);

    /// <summary>
    /// Builds the public half of an elliptic curve key.
    /// </summary>
    /// <param name="family">The family that the curve belongs to.</param>
    /// <returns>The type of a public key on <paramref name="family"/>.</returns>
    public static KeyType EccPublicKey(EccFamily family) =>
        new(KeyTypeKind.EccPublicKey, family, DhFamily.Rfc7919);

    /// <summary>
    /// Builds both halves of a Diffie-Hellman key.
    /// </summary>
    /// <param name="family">The family that the group belongs to.</param>
    /// <returns>The type of a key pair on <paramref name="family"/>.</returns>
    public static KeyType DhKeyPair(DhFamily family) =>
        new(KeyTypeKind.DhKeyPair, Keys.EccFamily.None, family);

    /// <summary>
    /// Builds the public half of a Diffie-Hellman key.
    /// </summary>
    /// <param name="family">The family that the group belongs to.</param>
    /// <returns>The type of a public key on <paramref name="family"/>.</returns>
    public static KeyType DhPublicKey(DhFamily family) =>
        new(KeyTypeKind.DhPublicKey, Keys.EccFamily.None, family);

    private static KeyType Plain(KeyTypeKind kind) =>
        new(kind, Keys.EccFamily.None, DhFamily.Rfc7919);
}
