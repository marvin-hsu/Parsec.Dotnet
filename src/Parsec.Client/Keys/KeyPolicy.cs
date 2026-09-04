using Parsec.Client.Algorithms;

namespace Parsec.Client.Keys;

/// <summary>
/// States what an application may do with a key and which algorithm the key is bound to.
/// </summary>
/// <remarks>
/// A key binds to one algorithm. The service refuses a request that names any other, which is
/// what keeps a signing key from being talked into decrypting. Bind to
/// <see cref="Algorithm.None"/> only for a key that no operation names, such as raw data.
/// </remarks>
public sealed record KeyPolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyPolicy"/> class.
    /// </summary>
    /// <param name="usage">What the application may do with the key.</param>
    /// <param name="algorithm">The algorithm that the key is bound to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="algorithm"/> is <see langword="null"/>.</exception>
    public KeyPolicy(KeyUsages usage, Algorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(algorithm);

        Usage = usage;
        Algorithm = algorithm;
    }

    /// <summary>Gets what the application may do with the key.</summary>
    public KeyUsages Usage { get; }

    /// <summary>Gets the algorithm that the key is bound to.</summary>
    public Algorithm Algorithm { get; }
}
