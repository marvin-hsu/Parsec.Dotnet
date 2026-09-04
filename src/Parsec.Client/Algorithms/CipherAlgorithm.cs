namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that runs one symmetric cipher mode.
/// </summary>
/// <param name="Cipher">The cipher mode to run.</param>
public sealed record CipherAlgorithm(Cipher Cipher) : Algorithm;
