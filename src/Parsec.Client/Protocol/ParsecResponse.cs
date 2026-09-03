namespace Parsec.Client.Protocol;

/// <summary>
/// One response message: a wire header and a body.
/// </summary>
/// <remarks>
/// A response carries no authentication field. The body holds an encoded protobuf message. The
/// body can be empty, and an empty body is not an error: protobuf3 leaves a zero-valued scalar
/// off the wire, so a message whose every field holds a default value encodes to no bytes.
/// </remarks>
internal readonly record struct ParsecResponse
{
    /// <summary>Gets the header of the response.</summary>
    public WireHeader Header { get; init; }

    /// <summary>Gets the encoded body of the response.</summary>
    public ReadOnlyMemory<byte> Body { get; init; }

    /// <summary>Gets a value indicating whether the service reported success.</summary>
    public bool IsSuccess => Header.Status == ResponseStatus.Success;
}
