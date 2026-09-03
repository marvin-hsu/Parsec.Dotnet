using System.Globalization;
using Parsec.Client.Protocol;

namespace Parsec.Client;

/// <summary>
/// The answer of the Parsec service does not follow the wire protocol.
/// </summary>
/// <remarks>
/// The client raises this exception for a header that does not parse, for a message that the
/// connection cut short, and for a body that does not decode. The connection is not usable after
/// this fault, because the client cannot tell where the next message starts.
/// </remarks>
public sealed class ParsecProtocolException : ParsecException
{
    /// <summary>Initializes a new instance of the <see cref="ParsecProtocolException"/> class.</summary>
    public ParsecProtocolException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecProtocolException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    public ParsecProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecProtocolException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    /// <param name="innerException">The fault that caused this one.</param>
    public ParsecProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private ParsecProtocolException(string message, Opcode? operation)
        : base(message) => Operation = operation;

    private ParsecProtocolException(string message, Opcode? operation, Exception innerException)
        : base(message, innerException) => Operation = operation;

    /// <summary>Gets the operation of the request, or <see langword="null"/> if it is not known.</summary>
    public Opcode? Operation { get; }

    /// <summary>
    /// Makes the exception for a response that the frame reader refused.
    /// </summary>
    /// <param name="error">The cause that the frame reader reported.</param>
    /// <param name="operation">The operation of the request, or <see langword="null"/> if it is not known.</param>
    /// <returns>The exception to raise.</returns>
    internal static ParsecProtocolException FromFrameError(ParsecFrameError error, Opcode? operation) =>
        new(Prefix(operation) + ParsecErrorText.DescribeFrameFault(error), operation);

    /// <summary>
    /// Makes the exception for a body that does not decode.
    /// </summary>
    /// <param name="operation">The operation of the request.</param>
    /// <param name="innerException">The fault of the decoder.</param>
    /// <returns>The exception to raise.</returns>
    internal static ParsecProtocolException DecodeFailed(Opcode operation, Exception innerException) =>
        new(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The client could not decode the body of the {ParsecErrorText.GetName(operation)} response ({(uint)operation})."),
            operation,
            innerException);

    /// <summary>
    /// Makes the exception for an answer that names another operation.
    /// </summary>
    /// <param name="expected">The operation of the request.</param>
    /// <param name="actual">The operation that the header of the answer names.</param>
    /// <returns>The exception to raise.</returns>
    internal static ParsecProtocolException MismatchedOpcode(Opcode expected, Opcode actual) =>
        new(ParsecErrorText.DescribeMismatchedOpcode(expected, actual), expected);

    /// <summary>
    /// Makes the exception for a value of an answer that is too large for the field it belongs to.
    /// </summary>
    /// <param name="operation">The operation of the request.</param>
    /// <param name="field">The name of the field.</param>
    /// <param name="value">The value that the service sent.</param>
    /// <param name="maximum">The largest value that the field holds.</param>
    /// <returns>The exception to raise.</returns>
    internal static ParsecProtocolException OutOfRangeField(
        Opcode operation,
        string field,
        uint value,
        long maximum) =>
        new(
            Prefix(operation) + ParsecErrorText.DescribeOutOfRangeField(field, value, maximum),
            operation);

    /// <summary>
    /// Names the operation at the start of a message.
    /// </summary>
    /// <param name="operation">The operation of the request, or <see langword="null"/> if it is not known.</param>
    /// <returns>The text to put before the description of the fault.</returns>
    private static string Prefix(Opcode? operation) =>
        operation is null
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $"The {ParsecErrorText.GetName(operation.Value)} request ({(uint)operation.Value}) failed. ");
}
