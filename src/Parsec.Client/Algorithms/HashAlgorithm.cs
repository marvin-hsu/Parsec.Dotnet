namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that computes one hash.
/// </summary>
/// <param name="Hash">The hash to compute.</param>
public sealed record HashAlgorithm(Hash Hash) : Algorithm;
