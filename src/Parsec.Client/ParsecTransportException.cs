using System.Globalization;

namespace Parsec.Client;

/// <summary>
/// The client could not talk to the Parsec service.
/// </summary>
/// <remarks>
/// The fault is below the wire protocol. Causes include a socket file that does not exist, a
/// socket file that the user cannot open, and a connection that the service closed. The inner
/// exception holds the fault of the platform.
/// </remarks>
public sealed class ParsecTransportException : ParsecException
{
    /// <summary>Initializes a new instance of the <see cref="ParsecTransportException"/> class.</summary>
    public ParsecTransportException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecTransportException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    public ParsecTransportException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecTransportException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    /// <param name="innerException">The fault that caused this one.</param>
    public ParsecTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Makes the exception for a fault of the platform on a socket.
    /// </summary>
    /// <param name="socketPath">The path of the socket file of the service.</param>
    /// <param name="innerException">The fault of the platform.</param>
    /// <returns>The exception to raise.</returns>
    internal static ParsecTransportException FromSocketFault(string socketPath, Exception innerException) =>
        new(
            string.Create(
                CultureInfo.InvariantCulture,
                $"The connection to the Parsec service at \"{socketPath}\" failed: {innerException.Message}"),
            innerException);
}
