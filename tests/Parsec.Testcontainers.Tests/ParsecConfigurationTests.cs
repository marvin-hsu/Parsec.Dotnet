namespace Parsec.Testcontainers.Tests;

public sealed class ParsecConfigurationTests
{
    [Fact]
    public void ANewConfigurationKeepsEveryValueOfTheImage()
    {
        var configuration = new ParsecConfiguration();

        Assert.Null(configuration.AuthType);
        Assert.Null(configuration.LogLevel);
        Assert.Null(configuration.SocketDirectory);
    }

    [Fact]
    public void TheConstructorKeepsTheValuesThatYouGive()
    {
        var configuration = new ParsecConfiguration(
            ParsecAuthType.UnixPeerCredentials,
            ParsecLogLevel.Trace,
            "/tmp/parsec");

        Assert.Equal(ParsecAuthType.UnixPeerCredentials, configuration.AuthType);
        Assert.Equal(ParsecLogLevel.Trace, configuration.LogLevel);
        Assert.Equal("/tmp/parsec", configuration.SocketDirectory);
    }

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
    public void AMergeChangesOneValueAndKeepsTheOthers()
    {
        var oldValue = new ParsecConfiguration(
            ParsecAuthType.Direct,
            ParsecLogLevel.Info,
            "/run/parsec");

        var merged = new ParsecConfiguration(oldValue, new ParsecConfiguration(logLevel: ParsecLogLevel.Trace));

        Assert.Equal(ParsecAuthType.Direct, merged.AuthType);
        Assert.Equal(ParsecLogLevel.Trace, merged.LogLevel);
        Assert.Equal("/run/parsec", merged.SocketDirectory);
    }

    [Fact]
    public void TheCopyConstructorKeepsEveryValue()
    {
        var source = new ParsecConfiguration(
            ParsecAuthType.UnixPeerCredentials,
            ParsecLogLevel.Error,
            "/run/parsec");

        var copy = new ParsecConfiguration(source);

        Assert.Equal(source.AuthType, copy.AuthType);
        Assert.Equal(source.LogLevel, copy.LogLevel);
        Assert.Equal(source.SocketDirectory, copy.SocketDirectory);
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
