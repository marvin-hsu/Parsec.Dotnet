namespace Parsec.Client;

/// <summary>
/// The configuration of the client is not usable.
/// </summary>
/// <remarks>
/// The library raises this exception before it opens a connection. Causes include a service
/// endpoint with an unsupported scheme, an endpoint with no path, and a Unix socket path that is
/// longer than the platform accepts. The application fixes the configuration and tries again.
/// </remarks>
public sealed class ParsecConfigurationException : ParsecException
{
    /// <summary>Initializes a new instance of the <see cref="ParsecConfigurationException"/> class.</summary>
    public ParsecConfigurationException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecConfigurationException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    public ParsecConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ParsecConfigurationException"/> class.</summary>
    /// <param name="message">The text that tells what went wrong.</param>
    /// <param name="innerException">The fault that caused this one.</param>
    public ParsecConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
