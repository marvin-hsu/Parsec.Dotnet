using System.Net.Sockets;
using System.Text;
using Parsec.Client.Errors;
using Parsec.Client.Transport;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers service discovery: the order of the three sources of the endpoint, the schemes that
/// the client accepts, and the socket path limit of the platform.
/// </summary>
public sealed class ParsecEndpointTests
{
    /// <summary>The endpoint that the service specification names as the default.</summary>
    private const string DefaultEndpoint = "unix:/run/parsec/parsec.sock";

    [Fact]
    public void DefaultIsTheEndpointOfTheSpecification()
    {
        Assert.Equal(DefaultEndpoint, ParsecEndpoint.Default.ToString());
        Assert.Equal("/run/parsec/parsec.sock", ParsecEndpoint.GetSocketPath(ParsecEndpoint.Default));
    }

    [Fact]
    public void ResolveFallsBackToTheDefaultWhenNothingStatesAnEndpoint()
    {
        Assert.Equal(ParsecEndpoint.Default, ParsecEndpoint.Resolve(null, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveTreatsBlankTextAsAbsent(string blank)
    {
        Assert.Equal(ParsecEndpoint.Default, ParsecEndpoint.Resolve(blank, blank));
        Assert.Equal("unix:/tmp/from-env.sock", ParsecEndpoint.Resolve(blank, "unix:/tmp/from-env.sock").ToString());
    }

    [Fact]
    public void ResolveReadsTheEnvironmentValueWhenTheApplicationStatesNothing()
    {
        Assert.Equal(
            "unix:/tmp/from-env.sock",
            ParsecEndpoint.Resolve(null, "unix:/tmp/from-env.sock").ToString());
    }

    [Fact]
    public void ResolvePrefersTheEndpointOfTheApplication()
    {
        Assert.Equal(
            "unix:/tmp/from-app.sock",
            ParsecEndpoint.Resolve("unix:/tmp/from-app.sock", "unix:/tmp/from-env.sock").ToString());
    }

    /// <summary>
    /// The library must read the environment variable itself. This is the one test that touches
    /// the real process environment; every other test states the value as an argument.
    /// </summary>
    [Fact]
    public void ResolveReadsTheRealEnvironmentVariable()
    {
        var original = Environment.GetEnvironmentVariable(ParsecEndpoint.EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(ParsecEndpoint.EnvironmentVariableName, "unix:/tmp/real-env.sock");
            Assert.Equal("unix:/tmp/real-env.sock", ParsecEndpoint.Resolve().ToString());
            Assert.Equal("unix:/tmp/override.sock", ParsecEndpoint.Resolve("unix:/tmp/override.sock").ToString());

            Environment.SetEnvironmentVariable(ParsecEndpoint.EnvironmentVariableName, null);
            Assert.Equal(ParsecEndpoint.Default, ParsecEndpoint.Resolve());
        }
        finally
        {
            Environment.SetEnvironmentVariable(ParsecEndpoint.EnvironmentVariableName, original);
        }
    }

    [Theory]
    [InlineData("http://example.com/parsec")]
    [InlineData("tcp://127.0.0.1:1234")]
    [InlineData("file:/run/parsec/parsec.sock")]
    [InlineData("/run/parsec/parsec.sock")]
    public void ResolveRefusesAnySchemeOtherThanUnix(string endpoint)
    {
        // A bare path is in this list on purpose. It parses as a file URI, which is not a
        // Unix socket endpoint, so the client refuses it and says so.
        var exception = Assert.Throws<ParsecConfigurationException>(() => ParsecEndpoint.Resolve(endpoint, null));
        Assert.Contains("unix", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a uri")]
    [InlineData("://missing-scheme")]
    public void ResolveRefusesTextThatIsNotAnAbsoluteUri(string endpoint)
    {
        Assert.Throws<ParsecConfigurationException>(() => ParsecEndpoint.Resolve(endpoint, null));
    }

    [Theory]
    [InlineData("unix:/run/parsec/parsec.sock", "/run/parsec/parsec.sock")]
    [InlineData("unix:///run/parsec/parsec.sock", "/run/parsec/parsec.sock")]
    [InlineData("UNIX:/run/parsec/parsec.sock", "/run/parsec/parsec.sock")]
    [InlineData("unix:/tmp/a%20b.sock", "/tmp/a b.sock")]
    public void GetSocketPathReadsThePathOfTheUri(string endpoint, string expected)
    {
        Assert.Equal(expected, ParsecEndpoint.GetSocketPath(new Uri(endpoint)));
    }

    /// <summary>
    /// A URI such as unix://run/parsec.sock puts "run" in the host and leaves "/parsec.sock" in
    /// the path. The Go reference client would connect to the short path. This client refuses,
    /// so the mistake is visible.
    /// </summary>
    [Fact]
    public void GetSocketPathRefusesAnEndpointWithAHost()
    {
        var exception = Assert.Throws<ParsecConfigurationException>(
            () => ParsecEndpoint.GetSocketPath(new Uri("unix://run/parsec.sock")));

        Assert.Contains("run", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSocketPathRefusesASchemeThatIsNotUnix()
    {
        // Resolve checks the scheme too, but an application can hand an endpoint straight to
        // this method, so the check belongs here as well. The endpoints below hold a path and no
        // host, so the scheme is the only thing that is wrong with them.
        var fault = Assert.Throws<ParsecConfigurationException>(
            () => ParsecEndpoint.GetSocketPath(new Uri("tcp:/run/parsec/parsec.sock")));

        Assert.Contains("tcp", fault.Message, StringComparison.Ordinal);
        Assert.Throws<ParsecConfigurationException>(
            () => ParsecEndpoint.GetSocketPath(new Uri("unixgram:/run/parsec/parsec.sock")));
    }

    [Fact]
    public void GetSocketPathRefusesAnEndpointWithNoPath()
    {
        Assert.Throws<ParsecConfigurationException>(() => ParsecEndpoint.GetSocketPath(new Uri("unix:")));
    }

    [Fact]
    public void GetSocketPathRefusesARelativeUri()
    {
        Assert.Throws<ParsecConfigurationException>(
            () => ParsecEndpoint.GetSocketPath(new Uri("parsec.sock", UriKind.Relative)));
    }

    [Fact]
    public void GetSocketPathRefusesNull()
    {
        Assert.Throws<ArgumentNullException>(() => ParsecEndpoint.GetSocketPath(null!));
    }

    /// <summary>
    /// The address of a Unix domain socket holds the path in a fixed field that ends with a
    /// terminator byte. macOS gives the field 104 bytes and Linux gives it 108, so the longest
    /// path is 103 bytes and 107 bytes.
    /// </summary>
    /// <param name="fieldBytes">The byte count of the socket path field.</param>
    /// <param name="longest">The byte count of the longest path that fits in the field.</param>
    [Theory]
    [InlineData(ParsecEndpoint.SocketPathFieldBytesElsewhere, 103)]
    [InlineData(ParsecEndpoint.SocketPathFieldBytesOnLinuxAndWindows, 107)]
    public void GetSocketPathAcceptsTheLongestPathOfTheFieldAndRefusesOneMore(int fieldBytes, int longest)
    {
        var accepted = "/" + new string('a', longest - 1);
        Assert.Equal(longest, accepted.Length);
        Assert.Equal(accepted, ParsecEndpoint.GetSocketPath(new Uri("unix:" + accepted), fieldBytes));

        var refused = accepted + "a";
        var exception = Assert.Throws<ParsecConfigurationException>(
            () => ParsecEndpoint.GetSocketPath(new Uri("unix:" + refused), fieldBytes));

        Assert.Contains(ParsecEndpoint.EnvironmentVariableName, exception.Message, StringComparison.Ordinal);
        Assert.Contains(longest.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The limit counts bytes, not characters. A path of 60 characters that each need two bytes
    /// does not fit in a field of 104 bytes.
    /// </summary>
    [Fact]
    public void GetSocketPathCountsBytesAndNotCharacters()
    {
        var path = "/" + new string('é', 60);
        Assert.Equal(61, path.Length);
        Assert.Equal(121, Encoding.UTF8.GetByteCount(path));

        Assert.Throws<ParsecConfigurationException>(
            () => ParsecEndpoint.GetSocketPath(new Uri("unix:" + path), ParsecEndpoint.SocketPathFieldBytesElsewhere));
    }

    /// <summary>
    /// The limit of this library must be the limit of this platform. The longest path that the
    /// library accepts is the longest path that the socket API accepts, and one byte more is
    /// refused by both.
    /// </summary>
    [Fact]
    public void TheAcceptedPathLengthMatchesThePlatform()
    {
        var longest = "/" + new string('a', ParsecEndpoint.SocketPathFieldBytes - 2);

        _ = new UnixDomainSocketEndPoint(longest);
        Assert.Equal(longest, ParsecEndpoint.GetSocketPath(new Uri("unix:" + longest)));

        var tooLong = longest + "a";
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnixDomainSocketEndPoint(tooLong));
        Assert.Throws<ParsecConfigurationException>(() => ParsecEndpoint.GetSocketPath(new Uri("unix:" + tooLong)));
    }
}
