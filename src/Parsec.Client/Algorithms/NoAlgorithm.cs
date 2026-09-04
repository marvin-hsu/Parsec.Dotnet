namespace Parsec.Client.Algorithms;

/// <summary>
/// The algorithm that names nothing.
/// </summary>
/// <remarks>
/// A key that binds to no algorithm carries this. Reach it through <see cref="Algorithm.None"/>
/// rather than building one, so that every mention of it is the same instance.
/// </remarks>
public sealed record NoAlgorithm : Algorithm
{
    internal NoAlgorithm()
    {
    }
}
