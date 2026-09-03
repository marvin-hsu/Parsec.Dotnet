using System.Globalization;
using Parsec.Client.Protocol;

namespace Parsec.Client;

/// <summary>
/// Builds the text of the exceptions of the library.
/// </summary>
/// <remarks>
/// Every message names the operation and the status. A support case starts with the message
/// alone, so the message must carry the two values that identify the fault.
/// </remarks>
internal static class ParsecErrorText
{
    /// <summary>The name that a value gets when the protocol version of the client does not define it.</summary>
    public const string UnknownName = "Unknown";

    /// <summary>Gets the name of a status.</summary>
    /// <param name="status">The status that came off the wire.</param>
    /// <returns>The name of the status, or <see cref="UnknownName"/>.</returns>
    public static string GetName(ResponseStatus status) => Enum.GetName(status) ?? UnknownName;

    /// <summary>Gets the name of an operation.</summary>
    /// <param name="operation">The operation that the request asked for.</param>
    /// <returns>The name of the operation, or <see cref="UnknownName"/>.</returns>
    public static string GetName(Opcode operation) => Enum.GetName(operation) ?? UnknownName;

    /// <summary>
    /// Describes a failed answer of the service.
    /// </summary>
    /// <param name="status">The status that the service sent.</param>
    /// <param name="operation">The operation of the request, or <see langword="null"/> if it is not known.</param>
    /// <returns>One sentence that names the operation and the status.</returns>
    public static string DescribeServiceFault(ResponseStatus status, Opcode? operation)
    {
        var statusText = string.Create(
            CultureInfo.InvariantCulture,
            $"status {GetName(status)} ({(ushort)status})");

        if (operation is null)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"The Parsec service answered with {statusText}.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"The Parsec service answered the {GetName(operation.Value)} request ({(uint)operation.Value}) with {statusText}.");
    }

    /// <summary>
    /// Describes a response that the client could not read.
    /// </summary>
    /// <param name="error">The cause that the frame reader reported.</param>
    /// <returns>One sentence that tells what is wrong with the response.</returns>
    public static string DescribeFrameFault(ParsecFrameError error) => error switch
    {
        ParsecFrameError.UnexpectedEndOfStream =>
            "The connection closed before the response of the Parsec service was complete.",
        ParsecFrameError.BadMagicNumber =>
            "The response does not start with the magic number of the Parsec protocol.",
        ParsecFrameError.HeaderSizeTooSmall =>
            "The header of the response is smaller than the Parsec protocol allows.",
        ParsecFrameError.BodyTooLarge =>
            "The body of the response is larger than the client accepts.",
        _ => "The response of the Parsec service did not parse.",
    };
}
