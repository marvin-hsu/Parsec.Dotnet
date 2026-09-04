using Parsec.Client.Protocol;

namespace Parsec.Client.Errors;

/// <summary>
/// The Parsec service answered a request with a failure status.
/// </summary>
/// <remarks>
/// <para>
/// The request reached the service, and the service refused it. <see cref="Status"/> holds the
/// value of the status field of the response header. Values 1 to 999 come from the service
/// itself. Values 1000 to 1999 come from the PSA Crypto layer of a provider, and
/// <see cref="ParsecPsaException"/> reports those.
/// </para>
/// <para>
/// A status that this version of the client does not know still lands here. The client never
/// fails on an unknown status, because a later service can add one.
/// </para>
/// </remarks>
public class ParsecServiceException : ParsecException
{
    /// <summary>The lowest status that the PSA Crypto layer uses.</summary>
    private const ushort PsaStatusRangeStart = 1000;

    /// <summary>The highest status that the PSA Crypto layer uses.</summary>
    private const ushort PsaStatusRangeEnd = 1999;

    /// <summary>Initializes a new instance of the <see cref="ParsecServiceException"/> class.</summary>
    public ParsecServiceException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecServiceException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    public ParsecServiceException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecServiceException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    /// <param name="innerException">The fault that caused this one.</param>
    public ParsecServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecServiceException"/> class.</summary>
    /// <param name="status">The status that the service sent.</param>
    /// <param name="operation">The operation of the request, or <see langword="null"/> if it is not known.</param>
    public ParsecServiceException(ResponseStatus status, Opcode? operation)
        : base(ParsecErrorText.DescribeServiceFault(status, operation))
    {
        Status = status;
        Operation = operation;
    }

    /// <summary>Gets the status that the service sent.</summary>
    public ResponseStatus Status { get; }

    /// <summary>Gets the operation of the request, or <see langword="null"/> if it is not known.</summary>
    public Opcode? Operation { get; }

    /// <summary>Gets the name of <see cref="Status"/>, or "Unknown".</summary>
    public string StatusName => ParsecErrorText.GetName(Status);

    /// <summary>
    /// Makes the exception that matches a failure status.
    /// </summary>
    /// <param name="status">The status that the service sent.</param>
    /// <param name="operation">The operation of the request, or <see langword="null"/> if it is not known.</param>
    /// <returns>
    /// A <see cref="ParsecPsaException"/> for a status of the PSA Crypto range, 1000 to 1999.
    /// A <see cref="ParsecServiceException"/> for every other status.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="status"/> is <see cref="ResponseStatus.Success"/>. A success is not a fault.
    /// </exception>
    public static ParsecServiceException FromStatus(ResponseStatus status, Opcode? operation = null)
    {
        if (status == ResponseStatus.Success)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "A success status does not describe a fault.");
        }

        var value = (ushort)status;
        if (value is >= PsaStatusRangeStart and <= PsaStatusRangeEnd)
        {
            return new ParsecPsaException(status, operation);
        }

        return new ParsecServiceException(status, operation);
    }
}
