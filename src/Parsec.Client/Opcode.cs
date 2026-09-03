using System.Diagnostics.CodeAnalysis;

namespace Parsec.Client;

/// <summary>
/// Identifies an operation in the Parsec wire protocol.
/// </summary>
/// <remarks>
/// The value goes into the opcode field of the wire header. The protocol assigns the values,
/// so they are not contiguous: 0x1D has no operation.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1027:Mark enums with FlagsAttribute",
    Justification = "An opcode names one operation. Opcodes do not combine.")]
[SuppressMessage(
    "Design",
    "CA1008:Enums should have zero value",
    Justification = "The wire protocol assigns no operation to opcode 0.")]
[SuppressMessage(
    "Design",
    "CA1028:Enum storage should be Int32",
    Justification = "The opcode field of the wire header is an unsigned 32-bit integer.")]
public enum Opcode : uint
{
    /// <summary>Asks the service for the highest wire protocol version that it supports.</summary>
    Ping = 0x01,

    /// <summary>Creates a key in the provider.</summary>
    PsaGenerateKey = 0x02,

    /// <summary>Removes a key from the provider.</summary>
    PsaDestroyKey = 0x03,

    /// <summary>Signs a hash that the caller computed.</summary>
    PsaSignHash = 0x04,

    /// <summary>Verifies a signature over a hash that the caller computed.</summary>
    PsaVerifyHash = 0x05,

    /// <summary>Imports key material into the provider.</summary>
    PsaImportKey = 0x06,

    /// <summary>Exports the public part of a key.</summary>
    PsaExportPublicKey = 0x07,

    /// <summary>Lists the providers that the service runs.</summary>
    ListProviders = 0x08,

    /// <summary>Lists the operations that one provider supports.</summary>
    ListOpcodes = 0x09,

    /// <summary>Encrypts a short message with an asymmetric key.</summary>
    PsaAsymmetricEncrypt = 0x0A,

    /// <summary>Decrypts a short message with an asymmetric key.</summary>
    PsaAsymmetricDecrypt = 0x0B,

    /// <summary>Exports key material.</summary>
    PsaExportKey = 0x0C,

    /// <summary>Asks the provider for random bytes.</summary>
    PsaGenerateRandom = 0x0D,

    /// <summary>Lists the authenticators that the service runs.</summary>
    ListAuthenticators = 0x0E,

    /// <summary>Computes the hash of a message.</summary>
    PsaHashCompute = 0x0F,

    /// <summary>Compares a message against a hash.</summary>
    PsaHashCompare = 0x10,

    /// <summary>Encrypts a message with authenticated encryption.</summary>
    PsaAeadEncrypt = 0x11,

    /// <summary>Decrypts a message with authenticated encryption.</summary>
    PsaAeadDecrypt = 0x12,

    /// <summary>Performs a raw key agreement.</summary>
    PsaRawKeyAgreement = 0x13,

    /// <summary>Encrypts a message with a symmetric cipher.</summary>
    PsaCipherEncrypt = 0x14,

    /// <summary>Decrypts a message with a symmetric cipher.</summary>
    PsaCipherDecrypt = 0x15,

    /// <summary>Computes a message authentication code.</summary>
    PsaMacCompute = 0x16,

    /// <summary>Verifies a message authentication code.</summary>
    PsaMacVerify = 0x17,

    /// <summary>Signs a message, and hashes it in the provider.</summary>
    PsaSignMessage = 0x18,

    /// <summary>Verifies a signature over a message, and hashes it in the provider.</summary>
    PsaVerifyMessage = 0x19,

    /// <summary>Lists the keys of the caller.</summary>
    ListKeys = 0x1A,

    /// <summary>Lists the clients that own keys. This is an administrator operation.</summary>
    ListClients = 0x1B,

    /// <summary>Removes all the keys of one client. This is an administrator operation.</summary>
    DeleteClient = 0x1C,

    /// <summary>Attests a key.</summary>
    AttestKey = 0x1E,

    /// <summary>Gets the parameters that a key attestation needs.</summary>
    PrepareKeyAttestation = 0x1F,

    /// <summary>Asks a provider if it can perform an operation with given key attributes.</summary>
    CanDoCrypto = 0x20,
}
