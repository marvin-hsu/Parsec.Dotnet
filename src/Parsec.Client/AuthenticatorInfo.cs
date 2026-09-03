namespace Parsec.Client;

/// <summary>
/// Describes one authenticator of the Parsec service.
/// </summary>
/// <remarks>
/// The service runs the authenticators that its configuration names. An application must send
/// an authentication of a type that the service runs, otherwise the service answers
/// <see cref="ResponseStatus.AuthenticatorNotRegistered"/>.
/// </remarks>
/// <param name="id">The authentication type that the authenticator accepts.</param>
/// <param name="description">The text that the authenticator gives about itself.</param>
/// <param name="version">The version of the authenticator.</param>
public sealed class AuthenticatorInfo(AuthType id, string description, Version version)
{
    /// <summary>Gets the authentication type that the authenticator accepts.</summary>
    public AuthType Id { get; } = id;

    /// <summary>Gets the text that the authenticator gives about itself.</summary>
    public string Description { get; } = description;

    /// <summary>
    /// Gets the version of the authenticator.
    /// </summary>
    /// <remarks>
    /// The service reports a major number, a minor number and a revision number. They become the
    /// major, the minor and the build parts of the version.
    /// </remarks>
    public Version Version { get; } = version;
}
