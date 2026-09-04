namespace Parsec.Client.Algorithms;

/// <summary>
/// States which hash a signature algorithm signs over.
/// </summary>
/// <remarks>
/// A signature algorithm either names the hash it signs over, or accepts whichever hash the
/// caller brings. The second form is what the specification calls <c>Any</c>, and it is the
/// default value of this type.
/// </remarks>
public readonly record struct SignHash
{
    private SignHash(Hash hash) => Hash = hash;

    /// <summary>
    /// Gets the value that accepts whichever hash the caller brings.
    /// </summary>
    public static SignHash Any => default;

    /// <summary>
    /// Gets the hash that the algorithm signs over, or <see langword="null"/> when the algorithm
    /// accepts whichever hash the caller brings.
    /// </summary>
    public Hash? Hash { get; }

    /// <summary>
    /// Builds a value that names one hash.
    /// </summary>
    /// <param name="hash">The hash that the algorithm signs over.</param>
    public static implicit operator SignHash(Hash hash) => FromHash(hash);

    /// <summary>
    /// Builds a value that names one hash.
    /// </summary>
    /// <param name="hash">The hash that the algorithm signs over.</param>
    /// <returns>A value that names <paramref name="hash"/>.</returns>
    public static SignHash FromHash(Hash hash) => new(hash);
}
