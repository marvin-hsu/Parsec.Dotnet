namespace Parsec.Client.Keys;

/// <summary>
/// Names the use that a question about a set of key attributes is about.
/// </summary>
/// <remarks>
/// A provider can accept a key for one use and refuse it for another. It may hold an RSA key it
/// cannot generate, for instance, so asking about <see cref="Import"/> and about
/// <see cref="Generate"/> are two different questions.
/// </remarks>
public enum KeyCheckType
{
    /// <summary>No use. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>Running the algorithm that the policy of the key names.</summary>
    Use = 1,

    /// <summary>Creating the key inside the provider.</summary>
    Generate = 2,

    /// <summary>Bringing key material in from outside.</summary>
    Import = 3,

    /// <summary>Deriving the key from another secret.</summary>
    Derive = 4,
}
