using System.Text;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;

namespace Parsec.Client.Authentication;

/// <summary>
/// Sends a JWT SPIFFE Verifiable Identity Document.
/// </summary>
/// <remarks>
/// <para>
/// The request carries authentication type 4 and the token as UTF-8 bytes. The service checks the
/// signature of the token against the SPIFFE trust bundle, so this type does not trust the client
/// machine.
/// </para>
/// <para>
/// The application fetches the token from its SPIFFE Workload API agent and supplies it here. The
/// library does not talk to that agent, because the agent is a separate dependency and the socket
/// of the agent has its own configuration. A token has a short life, so the application makes a
/// new instance of this class for each token.
/// </para>
/// </remarks>
public sealed class JwtSvidAuthentication : IParsecAuthentication
{
    private readonly byte[] _tokenBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtSvidAuthentication"/> class.
    /// </summary>
    /// <param name="token">The JWT SVID, in its compact serialized form.</param>
    /// <exception cref="ArgumentException">
    /// The token is empty, or it holds only white space, or its UTF-8 form is longer than
    /// <see cref="ushort.MaxValue"/> bytes, which the header cannot describe.
    /// </exception>
    public JwtSvidAuthentication(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        _tokenBytes = Encoding.UTF8.GetBytes(token);

        if (_tokenBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException(
                ParsecErrorText.DescribeOversizeAuthenticationField(_tokenBytes.Length),
                nameof(token));
        }
    }

    /// <inheritdoc/>
    public AuthType Type => AuthType.JwtSvid;

    /// <inheritdoc/>
    public int AuthBytesLength => _tokenBytes.Length;

    /// <inheritdoc/>
    public int WriteAuthBytes(Span<byte> destination)
    {
        AuthenticationField.ThrowIfDestinationTooSmall(destination, _tokenBytes.Length);

        _tokenBytes.CopyTo(destination);
        return _tokenBytes.Length;
    }
}
