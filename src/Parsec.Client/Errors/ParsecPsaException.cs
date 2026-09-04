using Parsec.Client.Protocol;

namespace Parsec.Client.Errors;

/// <summary>
/// The PSA Crypto layer of a provider refused a request.
/// </summary>
/// <remarks>
/// The status is in the range 1000 to 1999. The cause is in the cryptographic operation, and not
/// in the service. Examples are a key that does not exist and a signature that is not correct.
/// Use <see cref="ParsecServiceException.FromStatus"/> to get the right type for a status.
/// </remarks>
public sealed class ParsecPsaException : ParsecServiceException
{
    /// <summary>Initializes a new instance of the <see cref="ParsecPsaException"/> class.</summary>
    public ParsecPsaException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecPsaException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    public ParsecPsaException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecPsaException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    /// <param name="innerException">The fault that caused this one.</param>
    public ParsecPsaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecPsaException"/> class.</summary>
    /// <param name="status">The status that the service sent.</param>
    /// <param name="operation">The operation of the request, or <see langword="null"/> if it is not known.</param>
    public ParsecPsaException(ResponseStatus status, Opcode? operation)
        : base(status, operation)
    {
    }
}
