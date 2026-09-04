namespace Parsec.Client.Algorithms;

/// <summary>
/// Names a hash algorithm.
/// </summary>
/// <remarks>
/// The specification deprecates <see cref="Md2"/>, <see cref="Md4"/>, <see cref="Md5"/> and
/// <see cref="Sha1"/>. They stay here because a service may still hold a key that names one, and
/// a client that cannot read such a key back is of no use. Do not choose them for new work.
/// </remarks>
public enum Hash
{
    /// <summary>No hash. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>MD2. The specification deprecates it.</summary>
    Md2 = 1,

    /// <summary>MD4. The specification deprecates it.</summary>
    Md4 = 2,

    /// <summary>MD5. The specification deprecates it.</summary>
    Md5 = 3,

    /// <summary>RIPEMD-160.</summary>
    RipeMd160 = 4,

    /// <summary>SHA-1. The specification deprecates it.</summary>
    Sha1 = 5,

    /// <summary>SHA-224.</summary>
    Sha224 = 6,

    /// <summary>SHA-256.</summary>
    Sha256 = 7,

    /// <summary>SHA-384.</summary>
    Sha384 = 8,

    /// <summary>SHA-512.</summary>
    Sha512 = 9,

    /// <summary>SHA-512/224, which is SHA-512 truncated to 224 bits.</summary>
    Sha512Truncated224 = 10,

    /// <summary>SHA-512/256, which is SHA-512 truncated to 256 bits.</summary>
    Sha512Truncated256 = 11,

    /// <summary>SHA3-224.</summary>
    Sha3224 = 12,

    /// <summary>SHA3-256.</summary>
    Sha3256 = 13,

    /// <summary>SHA3-384.</summary>
    Sha3384 = 14,

    /// <summary>SHA3-512.</summary>
    Sha3512 = 15,
}
