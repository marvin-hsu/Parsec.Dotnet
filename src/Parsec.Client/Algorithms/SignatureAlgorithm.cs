namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that signs with an asymmetric key and verifies such a signature.
/// </summary>
public sealed record SignatureAlgorithm : Algorithm
{
    private SignatureAlgorithm(SignatureKind kind, SignHash hash)
    {
        Kind = kind;
        Hash = hash;
    }

    /// <summary>
    /// Gets RSA PKCS#1 v1.5 over bytes that the caller already prepared.
    /// </summary>
    public static SignatureAlgorithm RsaPkcs1v15SignRaw { get; } =
        new(SignatureKind.RsaPkcs1v15SignRaw, SignHash.Any);

    /// <summary>
    /// Gets ECDSA over bytes that the caller already prepared.
    /// </summary>
    public static SignatureAlgorithm EcdsaAny { get; } = new(SignatureKind.EcdsaAny, SignHash.Any);

    /// <summary>Gets the algorithm that signs.</summary>
    public SignatureKind Kind { get; }

    /// <summary>
    /// Gets the hash that the algorithm signs over. The two algorithms that take bytes the
    /// caller prepared carry <see cref="SignHash.Any"/>.
    /// </summary>
    public SignHash Hash { get; }

    /// <summary>
    /// Builds RSA PKCS#1 v1.5 over a hash.
    /// </summary>
    /// <param name="hash">The hash to sign over.</param>
    /// <returns>The algorithm that signs over <paramref name="hash"/>.</returns>
    public static SignatureAlgorithm RsaPkcs1v15Sign(SignHash hash) =>
        new(SignatureKind.RsaPkcs1v15Sign, hash);

    /// <summary>
    /// Builds RSA PSS over a hash.
    /// </summary>
    /// <param name="hash">The hash to sign over.</param>
    /// <returns>The algorithm that signs over <paramref name="hash"/>.</returns>
    public static SignatureAlgorithm RsaPss(SignHash hash) => new(SignatureKind.RsaPss, hash);

    /// <summary>
    /// Builds ECDSA over a hash.
    /// </summary>
    /// <param name="hash">The hash to sign over.</param>
    /// <returns>The algorithm that signs over <paramref name="hash"/>.</returns>
    public static SignatureAlgorithm Ecdsa(SignHash hash) => new(SignatureKind.Ecdsa, hash);

    /// <summary>
    /// Builds deterministic ECDSA over a hash, which draws the nonce from the message instead of
    /// at random.
    /// </summary>
    /// <param name="hash">The hash to sign over.</param>
    /// <returns>The algorithm that signs over <paramref name="hash"/>.</returns>
    public static SignatureAlgorithm DeterministicEcdsa(SignHash hash) =>
        new(SignatureKind.DeterministicEcdsa, hash);
}
