namespace Parsec.Client.Algorithms;

/// <summary>
/// One algorithm of the specification, whichever family it belongs to.
/// </summary>
/// <remarks>
/// The hierarchy is closed: every type that derives from this one lives in this assembly, and a
/// key policy names exactly one of them. Match on the derived type to read the algorithm back.
/// </remarks>
public abstract record Algorithm
{
    private protected Algorithm()
    {
    }

    /// <summary>
    /// Gets the algorithm that names nothing, which is what a key policy carries when the key
    /// binds to no algorithm.
    /// </summary>
    public static Algorithm None { get; } = new NoAlgorithm();

    /// <summary>
    /// Builds the algorithm that computes one hash.
    /// </summary>
    /// <param name="hash">The hash to compute.</param>
    /// <returns>The algorithm that computes <paramref name="hash"/>.</returns>
    public static HashAlgorithm FromHash(Hash hash) => new(hash);

    /// <summary>
    /// Builds the algorithm that runs one cipher mode.
    /// </summary>
    /// <param name="cipher">The cipher mode to run.</param>
    /// <returns>The algorithm that runs <paramref name="cipher"/>.</returns>
    public static CipherAlgorithm FromCipher(Cipher cipher) => new(cipher);
}
