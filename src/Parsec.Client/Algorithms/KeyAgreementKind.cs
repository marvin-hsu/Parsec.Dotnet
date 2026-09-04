namespace Parsec.Client.Algorithms;

/// <summary>
/// Names the raw part of a key agreement, which is the part that produces the shared secret.
/// </summary>
public enum KeyAgreementKind
{
    /// <summary>No algorithm. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>Finite field Diffie-Hellman.</summary>
    Ffdh = 1,

    /// <summary>Elliptic curve Diffie-Hellman.</summary>
    Ecdh = 2,
}
