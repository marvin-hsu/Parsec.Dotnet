namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that encrypts with an asymmetric key.
/// </summary>
public sealed record EncryptionAlgorithm : Algorithm
{
    private EncryptionAlgorithm(EncryptionKind kind, Hash hash)
    {
        Kind = kind;
        Hash = hash;
    }

    /// <summary>Gets RSA PKCS#1 v1.5.</summary>
    public static EncryptionAlgorithm RsaPkcs1v15Crypt { get; } =
        new(EncryptionKind.RsaPkcs1v15Crypt, Hash.None);

    /// <summary>Gets the algorithm that encrypts.</summary>
    public EncryptionKind Kind { get; }

    /// <summary>
    /// Gets the hash that the padding is built on, which carries a value only when
    /// <see cref="Kind"/> is <see cref="EncryptionKind.RsaOaep"/>.
    /// </summary>
    public Hash Hash { get; }

    /// <summary>
    /// Builds RSA OAEP over a hash.
    /// </summary>
    /// <param name="hash">The hash to build the padding on.</param>
    /// <returns>The algorithm that pads with <paramref name="hash"/>.</returns>
    public static EncryptionAlgorithm RsaOaep(Hash hash) => new(EncryptionKind.RsaOaep, hash);
}
