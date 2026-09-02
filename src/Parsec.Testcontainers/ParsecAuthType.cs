namespace Parsec.Testcontainers;

/// <summary>
/// The authenticator that the Parsec service uses to identify an application.
/// </summary>
/// <remarks>
/// The Parsec service accepts one authenticator at a time. Both values in this enumeration are
/// only safe for tests. Read the deployment guidance before you copy either one into a real
/// system.
/// </remarks>
public enum ParsecAuthType
{
    /// <summary>
    /// The service trusts the application name that the client sends. This is the default of the
    /// module, because it lets a test choose any application name, and lets a test act as an
    /// administrator.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// The service reads the user ID of the peer from the Unix socket. A client cannot select its
    /// own identity. Use this value to test code that must work with peer credentials.
    /// </summary>
    UnixPeerCredentials = 1,
}
