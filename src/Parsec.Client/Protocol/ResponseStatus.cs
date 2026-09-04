using System.Diagnostics.CodeAnalysis;

namespace Parsec.Client.Protocol;

/// <summary>
/// Reports the outcome of a request.
/// </summary>
/// <remarks>
/// The value comes from the status field of the wire header of a response. Values 1 to 999 come
/// from the service itself. Values 1000 to 1999 come from the PSA Crypto layer of a provider.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1028:Enum storage should be Int32",
    Justification = "The status field of the wire header is an unsigned 16-bit integer.")]
public enum ResponseStatus : ushort
{
    /// <summary>The operation was a success.</summary>
    Success = 0,

    /// <summary>The provider ID of the request does not match the provider.</summary>
    WrongProviderId = 1,

    /// <summary>The provider does not support the content type of the request.</summary>
    ContentTypeNotSupported = 2,

    /// <summary>The provider does not support the accept type of the request.</summary>
    AcceptTypeNotSupported = 3,

    /// <summary>The service does not support the wire protocol version of the request.</summary>
    WireProtocolVersionNotSupported = 4,

    /// <summary>No provider is registered for the provider ID of the request.</summary>
    ProviderNotRegistered = 5,

    /// <summary>No provider is defined for the provider ID of the request.</summary>
    ProviderDoesNotExist = 6,

    /// <summary>The service could not deserialize the body of the message.</summary>
    DeserializingBodyFailed = 7,

    /// <summary>The service could not serialize the body of the message.</summary>
    SerializingBodyFailed = 8,

    /// <summary>The requested operation is not defined.</summary>
    OpcodeDoesNotExist = 9,

    /// <summary>The response is larger than the service allows.</summary>
    ResponseTooLarge = 10,

    /// <summary>Authentication failed.</summary>
    AuthenticationError = 11,

    /// <summary>The service does not support the requested authenticator.</summary>
    AuthenticatorDoesNotExist = 12,

    /// <summary>The requested authenticator is not registered.</summary>
    AuthenticatorNotRegistered = 13,

    /// <summary>The key info manager of the service failed.</summary>
    KeyInfoManagerError = 14,

    /// <summary>An input or output operation failed.</summary>
    ConnectionError = 15,

    /// <summary>A value is not valid for its data type.</summary>
    InvalidEncoding = 16,

    /// <summary>A constant field of the header is not valid.</summary>
    InvalidHeader = 17,

    /// <summary>A provider UUID is not exactly 16 bytes.</summary>
    WrongProviderUuid = 18,

    /// <summary>The request did not supply the authentication that the service needs.</summary>
    NotAuthenticated = 19,

    /// <summary>The content length of the request is above the limit of the service.</summary>
    BodySizeExceedsLimit = 20,

    /// <summary>The operation needs administrator rights.</summary>
    AdminOperation = 21,

    /// <summary>An error that matches no other cause.</summary>
    PsaErrorGenericError = 1132,

    /// <summary>A policy denies the requested action.</summary>
    PsaErrorNotPermitted = 1133,

    /// <summary>The provider does not support the operation or one of its parameters.</summary>
    PsaErrorNotSupported = 1134,

    /// <summary>A parameter is not valid.</summary>
    PsaErrorInvalidArgument = 1135,

    /// <summary>The key handle is not valid.</summary>
    PsaErrorInvalidHandle = 1136,

    /// <summary>The provider cannot perform the action in its current state.</summary>
    PsaErrorBadState = 1137,

    /// <summary>An output buffer is too small.</summary>
    PsaErrorBufferTooSmall = 1138,

    /// <summary>The item already exists.</summary>
    PsaErrorAlreadyExists = 1139,

    /// <summary>The item does not exist.</summary>
    PsaErrorDoesNotExist = 1140,

    /// <summary>There is not enough runtime memory.</summary>
    PsaErrorInsufficientMemory = 1141,

    /// <summary>There is not enough persistent storage.</summary>
    PsaErrorInsufficientStorage = 1142,

    /// <summary>There was not enough data to read from a resource.</summary>
    PsaErrorInsufficientData = 1143,

    /// <summary>Communication inside the provider failed.</summary>
    PsaErrorCommunicationFailure = 1145,

    /// <summary>Storage failed, and data loss is possible.</summary>
    PsaErrorStorageFailure = 1146,

    /// <summary>The provider found a hardware failure.</summary>
    PsaErrorHardwareFailure = 1147,

    /// <summary>There is not enough entropy to generate the random data that the action needs.</summary>
    PsaErrorInsufficientEntropy = 1148,

    /// <summary>The signature, the message authentication code, or the hash is not correct.</summary>
    PsaErrorInvalidSignature = 1149,

    /// <summary>The padding of the decrypted data is not correct.</summary>
    PsaErrorInvalidPadding = 1150,

    /// <summary>The provider found an attempt to tamper with it.</summary>
    PsaErrorCorruptionDetected = 1151,

    /// <summary>Stored data is corrupt.</summary>
    PsaErrorDataCorrupt = 1152,
}
