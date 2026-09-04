using System.Text;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;

namespace Parsec.Client.Authentication;

/// <summary>
/// Sends the application identity as plain text.
/// </summary>
/// <remarks>
/// <para>
/// The request carries authentication type 1 and the identity as UTF-8 bytes. The service does
/// not verify the identity, so it trusts every application on the machine. The threat model of
/// Parsec says to use this type only when all the clients are trusted.
/// </para>
/// <para>
/// The identity must be unique and it must stay the same across a restart of the application,
/// because the service keys the namespace of the stored keys on it. An identity that changes
/// hides the keys that the application made before.
/// </para>
/// </remarks>
public sealed class DirectAuthentication : IParsecAuthentication
{
    private readonly byte[] _applicationNameBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectAuthentication"/> class.
    /// </summary>
    /// <param name="applicationName">The identity of the application.</param>
    /// <exception cref="ArgumentException">
    /// The identity is empty, or it holds only white space, or its UTF-8 form is longer than
    /// <see cref="ushort.MaxValue"/> bytes, which the header cannot describe.
    /// </exception>
    public DirectAuthentication(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        _applicationNameBytes = Encoding.UTF8.GetBytes(applicationName);

        if (_applicationNameBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException(
                ParsecErrorText.DescribeOversizeAuthenticationField(_applicationNameBytes.Length),
                nameof(applicationName));
        }

        ApplicationName = applicationName;
    }

    /// <summary>Gets the identity of the application.</summary>
    public string ApplicationName { get; }

    /// <inheritdoc/>
    public AuthType Type => AuthType.Direct;

    /// <inheritdoc/>
    public int AuthBytesLength => _applicationNameBytes.Length;

    /// <inheritdoc/>
    public int WriteAuthBytes(Span<byte> destination)
    {
        AuthenticationField.ThrowIfDestinationTooSmall(destination, _applicationNameBytes.Length);

        _applicationNameBytes.CopyTo(destination);
        return _applicationNameBytes.Length;
    }
}
