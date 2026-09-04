namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that computes a message authentication code.
/// </summary>
/// <remarks>
/// Build one through a factory. A code runs at its full length unless
/// <see cref="Truncate(uint)"/> shortens it.
/// </remarks>
public sealed record MacAlgorithm : Algorithm
{
    private MacAlgorithm(MacKind kind, Hash hash)
    {
        Kind = kind;
        Hash = hash;
    }

    /// <summary>Gets the algorithm that computes CBC-MAC.</summary>
    public static MacAlgorithm CbcMac { get; } = new(MacKind.CbcMac, Hash.None);

    /// <summary>Gets the algorithm that computes CMAC.</summary>
    public static MacAlgorithm Cmac { get; } = new(MacKind.Cmac, Hash.None);

    /// <summary>Gets the construction that the code is built from.</summary>
    public MacKind Kind { get; }

    /// <summary>
    /// Gets the hash that the code is built on, which carries a value only when
    /// <see cref="Kind"/> is <see cref="MacKind.Hmac"/>.
    /// </summary>
    public Hash Hash { get; }

    /// <summary>
    /// Gets the byte count that the code is cut down to, or <see langword="null"/> when it runs
    /// at its full length.
    /// </summary>
    public uint? Length { get; private init; }

    /// <summary>
    /// Builds the algorithm that computes a keyed hash.
    /// </summary>
    /// <param name="hash">The hash to build the code on.</param>
    /// <returns>The algorithm that computes HMAC over <paramref name="hash"/>.</returns>
    public static MacAlgorithm Hmac(Hash hash) => new(MacKind.Hmac, hash);

    /// <summary>
    /// Cuts the code down to a shorter length.
    /// </summary>
    /// <param name="length">The byte count to cut the code down to.</param>
    /// <returns>The same algorithm, reporting the shorter length.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is zero.</exception>
    public MacAlgorithm Truncate(uint length)
    {
        ArgumentOutOfRangeException.ThrowIfZero(length);

        return this with { Length = length };
    }
}
