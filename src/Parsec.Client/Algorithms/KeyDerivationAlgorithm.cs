namespace Parsec.Client.Algorithms;

/// <summary>
/// The function that derives key material from a secret.
/// </summary>
public sealed record KeyDerivationAlgorithm : Algorithm
{
    private KeyDerivationAlgorithm(KeyDerivationKind kind, Hash hash)
    {
        Kind = kind;
        Hash = hash;
    }

    /// <summary>Gets the function that derives.</summary>
    public KeyDerivationKind Kind { get; }

    /// <summary>Gets the hash that the function is built on.</summary>
    public Hash Hash { get; }

    /// <summary>
    /// Builds HKDF over a hash.
    /// </summary>
    /// <param name="hash">The hash to build the function on.</param>
    /// <returns>The function built on <paramref name="hash"/>.</returns>
    public static KeyDerivationAlgorithm Hkdf(Hash hash) => new(KeyDerivationKind.Hkdf, hash);

    /// <summary>
    /// Builds the pseudorandom function of TLS 1.2 over a hash.
    /// </summary>
    /// <param name="hash">The hash to build the function on.</param>
    /// <returns>The function built on <paramref name="hash"/>.</returns>
    public static KeyDerivationAlgorithm Tls12Prf(Hash hash) =>
        new(KeyDerivationKind.Tls12Prf, hash);

    /// <summary>
    /// Builds the TLS 1.2 derivation of a master secret from a pre-shared key.
    /// </summary>
    /// <param name="hash">The hash to build the function on.</param>
    /// <returns>The function built on <paramref name="hash"/>.</returns>
    public static KeyDerivationAlgorithm Tls12PskToMs(Hash hash) =>
        new(KeyDerivationKind.Tls12PskToMs, hash);
}
