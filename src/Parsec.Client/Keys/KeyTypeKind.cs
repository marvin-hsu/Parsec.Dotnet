namespace Parsec.Client.Keys;

/// <summary>
/// Names what a key holds.
/// </summary>
public enum KeyTypeKind
{
    /// <summary>No type. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>Bytes that carry no structure of their own.</summary>
    RawData = 1,

    /// <summary>A key for a keyed hash.</summary>
    Hmac = 2,

    /// <summary>A secret that only derives other key material.</summary>
    Derive = 3,

    /// <summary>An AES key.</summary>
    Aes = 4,

    /// <summary>A DES or triple DES key. The specification deprecates it.</summary>
    Des = 5,

    /// <summary>A Camellia key.</summary>
    Camellia = 6,

    /// <summary>An ARC4 key. The specification deprecates it.</summary>
    Arc4 = 7,

    /// <summary>A ChaCha20 key.</summary>
    ChaCha20 = 8,

    /// <summary>The public half of an RSA key.</summary>
    RsaPublicKey = 9,

    /// <summary>Both halves of an RSA key.</summary>
    RsaKeyPair = 10,

    /// <summary>Both halves of an elliptic curve key, which carries a curve family.</summary>
    EccKeyPair = 11,

    /// <summary>The public half of an elliptic curve key, which carries a curve family.</summary>
    EccPublicKey = 12,

    /// <summary>Both halves of a Diffie-Hellman key, which carries a group family.</summary>
    DhKeyPair = 13,

    /// <summary>The public half of a Diffie-Hellman key, which carries a group family.</summary>
    DhPublicKey = 14,
}
