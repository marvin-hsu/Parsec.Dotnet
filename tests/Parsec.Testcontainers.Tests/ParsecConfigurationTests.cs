namespace Parsec.Testcontainers.Tests;

// Only the merge of two configurations has a test. The merge semantics of Testcontainers are easy
// to get wrong, and every With* call of the builder goes through them. A test of a constructor
// that stores its arguments catches no defect of this package.
public sealed class ParsecConfigurationTests
{
    [Fact]
    public void AMergeTakesTheNewValues()
    {
        var oldValue = new ParsecConfiguration(
            ParsecAuthType.Direct,
            ParsecLogLevel.Info,
            "/run/parsec");

        var newValue = new ParsecConfiguration(
            ParsecAuthType.UnixPeerCredentials,
            ParsecLogLevel.Debug,
            "/var/run/parsec");

        var merged = new ParsecConfiguration(oldValue, newValue);

        Assert.Equal(ParsecAuthType.UnixPeerCredentials, merged.AuthType);
        Assert.Equal(ParsecLogLevel.Debug, merged.LogLevel);
        Assert.Equal("/var/run/parsec", merged.SocketDirectory);
    }

    [Fact]
    public void AMergeKeepsTheOldValueWhenTheNewValueIsNotSet()
    {
        var oldValue = new ParsecConfiguration(
            ParsecAuthType.UnixPeerCredentials,
            ParsecLogLevel.Warn,
            "/run/parsec");

        var merged = new ParsecConfiguration(oldValue, new ParsecConfiguration());

        Assert.Equal(ParsecAuthType.UnixPeerCredentials, merged.AuthType);
        Assert.Equal(ParsecLogLevel.Warn, merged.LogLevel);
        Assert.Equal("/run/parsec", merged.SocketDirectory);
    }

    [Fact]
    public void AMergeAlsoTakesTheNewValuesOfTheBaseClass()
    {
        var oldValue = new ParsecConfiguration();
        var newValue = new ParsecConfiguration(
            new ContainerConfiguration(hostname: "parsec-under-test"));

        var merged = new ParsecConfiguration(oldValue, newValue);

        Assert.Equal("parsec-under-test", merged.Hostname);
    }
}
