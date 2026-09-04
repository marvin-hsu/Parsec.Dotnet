namespace Parsec.Client.Algorithms;

/// <summary>
/// Names an asymmetric signature algorithm.
/// </summary>
public enum SignatureKind
{
    /// <summary>No algorithm. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>RSA PKCS#1 v1.5 over a hash that the algorithm names.</summary>
    RsaPkcs1v15Sign = 1,

    /// <summary>RSA PKCS#1 v1.5 over bytes that the caller already prepared.</summary>
    RsaPkcs1v15SignRaw = 2,

    /// <summary>RSA PSS.</summary>
    RsaPss = 3,

    /// <summary>ECDSA over a hash that the algorithm names.</summary>
    Ecdsa = 4,

    /// <summary>ECDSA over bytes that the caller already prepared.</summary>
    EcdsaAny = 5,

    /// <summary>Deterministic ECDSA, which draws the nonce from the message instead of at random.</summary>
    DeterministicEcdsa = 6,
}
