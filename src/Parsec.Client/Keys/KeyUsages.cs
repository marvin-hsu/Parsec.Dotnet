namespace Parsec.Client.Keys;

/// <summary>
/// States what an application may do with a key.
/// </summary>
/// <remarks>
/// The service checks every request against these flags and answers
/// <c>PsaErrorNotPermitted</c> when the key does not carry the one the request needs. Grant only
/// what the application uses.
/// </remarks>
[Flags]
public enum KeyUsages
{
    /// <summary>Nothing is permitted.</summary>
    None = 0,

    /// <summary>The key material may leave the service.</summary>
    Export = 1,

    /// <summary>The key may be copied to another slot.</summary>
    Copy = 1 << 1,

    /// <summary>The key may be held in a cache.</summary>
    Cache = 1 << 2,

    /// <summary>The key may encrypt.</summary>
    Encrypt = 1 << 3,

    /// <summary>The key may decrypt.</summary>
    Decrypt = 1 << 4,

    /// <summary>The key may sign a message, hashing it as part of the operation.</summary>
    SignMessage = 1 << 5,

    /// <summary>The key may verify a signature over a message.</summary>
    VerifyMessage = 1 << 6,

    /// <summary>The key may sign a hash that the caller computed.</summary>
    SignHash = 1 << 7,

    /// <summary>The key may verify a signature over a hash that the caller computed.</summary>
    VerifyHash = 1 << 8,

    /// <summary>The key may derive other key material.</summary>
    Derive = 1 << 9,
}
