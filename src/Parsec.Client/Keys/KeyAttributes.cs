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
}
