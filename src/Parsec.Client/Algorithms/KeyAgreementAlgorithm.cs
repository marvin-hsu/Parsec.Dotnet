namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that agrees a shared secret with another party.
/// </summary>
/// <remarks>
/// The raw form hands the shared secret back. <see cref="WithDerivation(KeyDerivationAlgorithm)"/>
/// feeds it into a derivation function instead, which is the form that produces a usable key.
/// </remarks>
public sealed record KeyAgreementAlgorithm : Algorithm
{
    private KeyAgreementAlgorithm(KeyAgreementKind kind) => Kind = kind;

    /// <summary>Gets finite field Diffie-Hellman.</summary>
    public static KeyAgreementAlgorithm Ffdh { get; } = new(KeyAgreementKind.Ffdh);

    /// <summary>Gets elliptic curve Diffie-Hellman.</summary>
    public static KeyAgreementAlgorithm Ecdh { get; } = new(KeyAgreementKind.Ecdh);

    /// <summary>Gets the algorithm that produces the shared secret.</summary>
    public KeyAgreementKind Kind { get; }

    /// <summary>
    /// Gets the function that the shared secret feeds into, or <see langword="null"/> when the
    /// shared secret is handed back as it is.
    /// </summary>
    public KeyDerivationAlgorithm? Derivation { get; private init; }

    /// <summary>
    /// Feeds the shared secret into a derivation function.
    /// </summary>
    /// <param name="derivation">The function to feed the shared secret into.</param>
    /// <returns>The same algorithm, reporting the function.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="derivation"/> is <see langword="null"/>.</exception>
    public KeyAgreementAlgorithm WithDerivation(KeyDerivationAlgorithm derivation)
    {
        ArgumentNullException.ThrowIfNull(derivation);

        return this with { Derivation = derivation };
    }
}
