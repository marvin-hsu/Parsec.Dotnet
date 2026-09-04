namespace Parsec.Client.Algorithms;

/// <summary>
/// Names a symmetric cipher mode.
/// </summary>
/// <remarks>
/// The Mbed Crypto provider of the service supports no cipher operation, so a request that
/// names one of these answers <c>PsaErrorNotSupported</c> on the provider that the image of
/// <c>Parsec.Testcontainers</c> carries. Another provider may support them.
/// </remarks>
public enum Cipher
{
    /// <summary>No cipher. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>The stream cipher of the key type, such as ChaCha20 or ARC4.</summary>
    StreamCipher = 1,

    /// <summary>Counter mode.</summary>
    Ctr = 2,

    /// <summary>Cipher feedback mode.</summary>
    Cfb = 3,

    /// <summary>Output feedback mode.</summary>
    Ofb = 4,

    /// <summary>XEX with ciphertext stealing.</summary>
    Xts = 5,

    /// <summary>Electronic codebook mode with no padding.</summary>
    EcbNoPadding = 6,

    /// <summary>Cipher block chaining with no padding.</summary>
    CbcNoPadding = 7,

    /// <summary>Cipher block chaining with PKCS#7 padding.</summary>
    CbcPkcs7 = 8,
}
