namespace Parsec.Client.Algorithms;

/// <summary>
/// Names an algorithm that encrypts and authenticates in one pass.
/// </summary>
public enum Aead
{
    /// <summary>No algorithm. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>Counter with CBC-MAC.</summary>
    Ccm = 1,

    /// <summary>Galois/counter mode.</summary>
    Gcm = 2,

    /// <summary>ChaCha20 with the Poly1305 authenticator.</summary>
    ChaCha20Poly1305 = 3,
}
