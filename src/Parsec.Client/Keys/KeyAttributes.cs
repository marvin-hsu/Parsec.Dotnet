using Parsec.Client.Algorithms;

namespace Parsec.Client.Keys;

/// <summary>
/// Describes a key: what it holds, how large it is and what may be done with it.
/// </summary>
/// <remarks>
/// The service stores these alongside the key material and answers <c>ListKeys</c> with them, so
/// the same type describes a key that is being created and a key that already exists.
/// </remarks>
public sealed record KeyAttributes
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyAttributes"/> class.
    /// </summary>
    /// <param name="type">What the key holds.</param>
    /// <param name="bits">
    /// The size of the key in bits. Zero asks the provider to choose, which it accepts only where
    /// the type leaves no choice.
    /// </param>
    /// <param name="policy">What may be done with the key.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="type"/> or <paramref name="policy"/> is <see langword="null"/>.
    /// </exception>
    public KeyAttributes(KeyType type, uint bits, KeyPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(policy);

        Type = type;
        Bits = bits;
        Policy = policy;
    }

    /// <summary>Gets what the key holds.</summary>
    public KeyType Type { get; }

    /// <summary>Gets the size of the key in bits.</summary>
    public uint Bits { get; }

    /// <summary>Gets what may be done with the key.</summary>
    public KeyPolicy Policy { get; }

    /// <summary>
    /// Makes the attributes of an RSA key pair that signs a hash the caller computed.
    /// </summary>
    /// <param name="bits">The size of the modulus. 2048 is the smallest size still considered sound.</param>
    /// <param name="algorithm">
    /// The signature algorithm to bind the key to, or <see langword="null"/> for PKCS#1 v1.5 over
    /// SHA-256.
    /// </param>
    /// <param name="exportable">
    /// <see langword="true"/> to let the private half leave the service. Leave it
    /// <see langword="false"/> unless the application has a reason, because a key the service
    /// never hands out is the point of running the service.
    /// </param>
    /// <returns>The attributes to create the key with.</returns>
    public static KeyAttributes RsaSigningKey(
        uint bits = 2048,
        SignatureAlgorithm? algorithm = null,
        bool exportable = false) =>
        new(
            KeyType.RsaKeyPair,
            bits,
            new KeyPolicy(
                SigningUsage(exportable),
                algorithm ?? SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256)));

    /// <summary>
    /// Makes the attributes of an elliptic curve key pair that signs a hash the caller computed.
    /// </summary>
    /// <param name="family">The curve family. SECP-R1 at 256 bits is the curve most callers want.</param>
    /// <param name="bits">The size of the curve.</param>
    /// <param name="algorithm">
    /// The signature algorithm to bind the key to, or <see langword="null"/> for ECDSA over
    /// SHA-256.
    /// </param>
    /// <param name="exportable">
    /// <see langword="true"/> to let the private half leave the service.
    /// </param>
    /// <returns>The attributes to create the key with.</returns>
    public static KeyAttributes EccSigningKey(
        EccFamily family = EccFamily.SecpR1,
        uint bits = 256,
        SignatureAlgorithm? algorithm = null,
        bool exportable = false) =>
        new(
            KeyType.EccKeyPair(family),
            bits,
            new KeyPolicy(
                SigningUsage(exportable),
                algorithm ?? SignatureAlgorithm.Ecdsa(Hash.Sha256)));

    /// <summary>
    /// Makes the attributes of an RSA key pair that decrypts what its public half encrypted.
    /// </summary>
    /// <param name="bits">The size of the modulus.</param>
    /// <param name="algorithm">
    /// The encryption algorithm to bind the key to, or <see langword="null"/> for OAEP over
    /// SHA-256. PKCS#1 v1.5 encryption is the other choice, and it is the weaker one.
    /// </param>
    /// <returns>The attributes to create the key with.</returns>
    public static KeyAttributes RsaEncryptionKey(
        uint bits = 2048,
        EncryptionAlgorithm? algorithm = null) =>
        new(
            KeyType.RsaKeyPair,
            bits,
            new KeyPolicy(
                KeyUsages.Encrypt | KeyUsages.Decrypt,
                algorithm ?? EncryptionAlgorithm.RsaOaep(Hash.Sha256)));

    /// <summary>
    /// Makes the attributes of an AES key that encrypts and authenticates in one pass.
    /// </summary>
    /// <param name="bits">The size of the key.</param>
    /// <param name="algorithm">
    /// The algorithm to bind the key to, or <see langword="null"/> for Galois/counter mode.
    /// </param>
    /// <returns>The attributes to create the key with.</returns>
    public static KeyAttributes AesKey(uint bits = 256, AeadAlgorithm? algorithm = null) =>
        new(
            KeyType.Aes,
            bits,
            new KeyPolicy(KeyUsages.Encrypt | KeyUsages.Decrypt, algorithm ?? AeadAlgorithm.Gcm));

    private static KeyUsages SigningUsage(bool exportable) => exportable
        ? KeyUsages.SignHash | KeyUsages.VerifyHash | KeyUsages.Export
        : KeyUsages.SignHash | KeyUsages.VerifyHash;
}
