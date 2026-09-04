using Parsec.Client.Keys;
using Parsec.Client.Protocol;

namespace Parsec.Client.Models;

/// <summary>
/// Names one key of the application.
/// </summary>
/// <remarks>
/// A key name is unique inside one provider, so the pair of the provider and the name identifies
/// the key. The service reports the keys of the application that authenticated the request, and
/// of no other application.
/// </remarks>
/// <param name="provider">The provider that holds the key.</param>
/// <param name="name">The name of the key.</param>
/// <param name="attributes">What the key holds and what may be done with it.</param>
public sealed class KeyInfo(ProviderId provider, string name, KeyAttributes attributes)
{
    /// <summary>Gets the provider that holds the key.</summary>
    public ProviderId Provider { get; } = provider;

    /// <summary>Gets the name of the key.</summary>
    public string Name { get; } = name;

    /// <summary>Gets what the key holds and what may be done with it.</summary>
    public KeyAttributes Attributes { get; } = attributes;
}
