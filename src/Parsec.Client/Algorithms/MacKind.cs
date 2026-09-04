namespace Parsec.Client.Algorithms;

/// <summary>
/// Names the construction that a message authentication code is built from.
/// </summary>
public enum MacKind
{
    /// <summary>No construction. The service rejects this value, so it means the field was never set.</summary>
    None = 0,

    /// <summary>A keyed hash, which carries the hash it is built on.</summary>
    Hmac = 1,

    /// <summary>CBC-MAC.</summary>
    CbcMac = 2,

    /// <summary>CMAC.</summary>
    Cmac = 3,
}
