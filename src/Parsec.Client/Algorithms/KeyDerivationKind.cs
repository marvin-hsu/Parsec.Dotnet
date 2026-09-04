namespace Parsec.Client.Algorithms;

/// <summary>
/// Names a key derivation function.
/// </summary>
public enum KeyDerivationKind
{
    /// <summary>No function. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>HKDF.</summary>
    Hkdf = 1,

    /// <summary>The pseudorandom function of TLS 1.2.</summary>
    Tls12Prf = 2,

    /// <summary>The TLS 1.2 derivation of a master secret from a pre-shared key.</summary>
    Tls12PskToMs = 3,
}
