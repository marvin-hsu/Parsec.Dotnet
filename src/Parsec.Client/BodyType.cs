using System.Diagnostics.CodeAnalysis;

namespace Parsec.Client;

/// <summary>
/// Identifies the encoding of the body of a message.
/// </summary>
/// <remarks>
/// The value goes into the content type field and the accept type field of the wire header.
/// Protocol buffers is the only encoding that the service supports.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1028:Enum storage should be Int32",
    Justification = "The content type field of the wire header is one unsigned byte.")]
internal enum BodyType : byte
{
    /// <summary>The body holds a protocol buffers message.</summary>
    Protobuf = 0,
}
