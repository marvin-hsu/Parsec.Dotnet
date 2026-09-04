using System.Diagnostics.CodeAnalysis;

namespace Parsec.Client.Protocol;

/// <summary>
/// Identifies a provider of the Parsec service.
/// </summary>
/// <remarks>
/// The value goes into the provider field of the wire header. The core provider answers the
/// operations that describe the service itself. Every other provider answers cryptographic
/// operations.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1028:Enum storage should be Int32",
    Justification = "The provider field of the wire header is one unsigned byte.")]
public enum ProviderId : byte
{
    /// <summary>The core provider. It answers the operations that describe the service.</summary>
    Core = 0,

    /// <summary>The provider that uses the Mbed Crypto software library.</summary>
    MbedCrypto = 1,

    /// <summary>The provider that uses a PKCS #11 library.</summary>
    Pkcs11 = 2,

    /// <summary>The provider that uses a TPM through the TSS 2.0 Enhanced System API.</summary>
    Tpm = 3,

    /// <summary>The provider that uses the crypto Trusted Service in TrustZone.</summary>
    TrustedService = 4,

    /// <summary>The provider that uses the Microchip ATECCx08 CryptoAuthentication library.</summary>
    CryptoAuthLib = 5,
}
