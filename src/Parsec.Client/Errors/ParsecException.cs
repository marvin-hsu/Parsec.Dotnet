namespace Parsec.Client.Errors;

/// <summary>
/// The base of every exception that this library raises on purpose.
/// </summary>
/// <remarks>
/// An application that wants one catch clause for every fault of the library catches this type.
/// A more exact catch clause uses one of the derived types.
/// </remarks>
public abstract class ParsecException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ParsecException"/> class.</summary>
    protected ParsecException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    protected ParsecException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    /// <param name="innerException">The fault that caused this one.</param>
    protected ParsecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
