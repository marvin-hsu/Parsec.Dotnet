using System.Globalization;
using Parsec.Client.Protocol;

namespace Parsec.Client.Errors;

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

    /// <summary>Tells why a platform cannot use Unix peer credentials.</summary>
    public const string UnavailableUserId =
        "Unix peer credentials authentication needs a Unix user ID, which this platform does not report.";

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
    /// Describes an answer that names another operation than the request.
    /// </summary>
    /// <param name="expected">The operation of the request.</param>
    /// <param name="actual">The operation that the header of the answer names.</param>
    /// <returns>One sentence that names both operations.</returns>
    public static string DescribeMismatchedOpcode(Opcode expected, Opcode actual) => string.Create(
        CultureInfo.InvariantCulture,
        $"The client sent the {GetName(expected)} request ({(uint)expected}) but the answer names {GetName(actual)} ({(uint)actual}).");

    /// <summary>
    /// Describes a value of an answer that is too large for the field it belongs to.
    /// </summary>
    /// <param name="field">The name of the field.</param>
    /// <param name="value">The value that the service sent.</param>
    /// <param name="maximum">The largest value that the field holds.</param>
    /// <returns>One sentence that names the field, the value and the limit.</returns>
    public static string DescribeOutOfRangeField(string field, uint value, long maximum) => string.Create(
        CultureInfo.InvariantCulture,
        $"The service reported a {field} of {value}. The field holds a value of 0 to {maximum}.");

    /// <summary>
    /// Describes a field of an answer that the client cannot read back into its own model.
    /// </summary>
    /// <param name="field">The name of the field.</param>
    /// <param name="detail">What the field carried, in a form a reader can act on.</param>
    /// <returns>One sentence that names the field and what it carried.</returns>
    public static string DescribeUnreadableField(string field, string detail) => string.Create(
        CultureInfo.InvariantCulture,
        $"The service reported a {field} that this client cannot read: {detail}.");

    /// <summary>
    /// Describes an authentication field that the header cannot state the length of.
    /// </summary>
    /// <param name="length">The byte count of the field.</param>
    /// <returns>One sentence that names the byte count and the limit.</returns>
    public static string DescribeOversizeAuthenticationField(int length) => string.Create(
        CultureInfo.InvariantCulture,
        $"The authentication field is {length} bytes. The authentication length field of the header holds two bytes, so the field can be 0 to {ushort.MaxValue} bytes.");

    /// <summary>
    /// Describes an authentication buffer that is too small.
    /// </summary>
    /// <param name="destinationLength">The byte count of the buffer.</param>
    /// <param name="requiredLength">The byte count that the field needs.</param>
    /// <returns>One sentence that names both byte counts.</returns>
    public static string DescribeSmallAuthenticationBuffer(int destinationLength, int requiredLength) => string.Create(
        CultureInfo.InvariantCulture,
        $"The buffer holds {destinationLength} bytes, but the authentication field needs {requiredLength} bytes.");

    /// <summary>
    /// Describes an authentication that wrote a byte count other than the one it reported.
    /// </summary>
    /// <param name="reportedLength">The byte count that the implementation reported first.</param>
    /// <param name="writtenLength">The byte count that the implementation then wrote.</param>
    /// <returns>One sentence that names both byte counts.</returns>
    public static string DescribeAuthenticationLengthMismatch(int reportedLength, int writtenLength) => string.Create(
        CultureInfo.InvariantCulture,
        $"The authentication reported {reportedLength} bytes but then wrote {writtenLength} bytes. The two counts must match, because the header states the count before the field goes on the wire.");

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
