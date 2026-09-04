namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that encrypts and authenticates in one pass.
/// </summary>
/// <remarks>
/// The tag runs at the default length of the algorithm unless
/// <see cref="WithTagLength(uint)"/> shortens it.
/// </remarks>
public sealed record AeadAlgorithm : Algorithm
{
    private AeadAlgorithm(Aead aead) => Aead = aead;

    /// <summary>Gets counter mode with CBC-MAC.</summary>
    public static AeadAlgorithm Ccm { get; } = new(Algorithms.Aead.Ccm);

    /// <summary>Gets Galois/counter mode.</summary>
    public static AeadAlgorithm Gcm { get; } = new(Algorithms.Aead.Gcm);

    /// <summary>Gets ChaCha20 with the Poly1305 authenticator.</summary>
    public static AeadAlgorithm ChaCha20Poly1305 { get; } = new(Algorithms.Aead.ChaCha20Poly1305);

    /// <summary>Gets the algorithm that encrypts and authenticates.</summary>
    public Aead Aead { get; }

    /// <summary>
    /// Gets the byte count that the tag is cut down to, or <see langword="null"/> when the tag
    /// runs at the default length of the algorithm.
    /// </summary>
    public uint? TagLength { get; private init; }

    /// <summary>
    /// Builds the algorithm that one value of <see cref="Algorithms.Aead"/> names.
    /// </summary>
    /// <param name="aead">The algorithm to build.</param>
    /// <returns>The algorithm that <paramref name="aead"/> names.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="aead"/> is not a known algorithm.</exception>
    public static AeadAlgorithm FromAead(Aead aead) => aead switch
    {
        Algorithms.Aead.Ccm => Ccm,
        Algorithms.Aead.Gcm => Gcm,
        Algorithms.Aead.ChaCha20Poly1305 => ChaCha20Poly1305,
        _ => throw new ArgumentOutOfRangeException(nameof(aead), aead, null),
    };

    /// <summary>
    /// Cuts the tag down to a shorter length.
    /// </summary>
    /// <param name="length">The byte count to cut the tag down to.</param>
    /// <returns>The same algorithm, reporting the shorter tag.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is zero.</exception>
    public AeadAlgorithm WithTagLength(uint length)
    {
        ArgumentOutOfRangeException.ThrowIfZero(length);

        return this with { TagLength = length };
    }
}
