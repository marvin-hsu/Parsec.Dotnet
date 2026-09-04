namespace Parsec.Client.Algorithms;

/// <summary>
/// Names an asymmetric encryption algorithm.
/// </summary>
public enum EncryptionKind
{
    /// <summary>No algorithm. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>RSA PKCS#1 v1.5.</summary>
    RsaPkcs1v15Crypt = 1,

    /// <summary>RSA OAEP, which carries the hash it is built on.</summary>
    RsaOaep = 2,
}
