namespace Parsec.Client;

/// <summary>
/// Entry point for communicating with a Parsec service over its IPC transport.
/// </summary>
public interface IParsecClient
{
    /// <summary>
    /// Gets the implicit provider selected for cryptographic operations.
    /// </summary>
    public string ProviderName { get; }
}
