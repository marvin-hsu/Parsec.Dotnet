namespace Parsec.Testcontainers.Tests;

public sealed class ParsecConfigFileTests
{
    [Fact]
    public void Build_WithDefaultSettings_WritesTheSameFileAsTheImage()
    {
        var content = ParsecConfigFile.Build(
            ParsecImage.DefaultAuthType,
            ParsecImage.DefaultLogLevel,
            ParsecImage.SocketDirectory);

        Assert.Equal(
            """
            # Parsec service configuration written by Parsec.Testcontainers.
            #
            # THIS FILE IS FOR INTEGRATION TESTING ONLY. It gives an application more
            # trust than a real system must give. Read the deployment guidance before
            # you copy any part of it:
            # https://parallaxsecond.github.io/parsec-book/parsec_security/secure_deployment.html
            #
            # Schema: Parsec 1.5.0.

            [core_settings]
            log_level = "info"
            allow_root = true

            [listener]
            listener_type = "DomainSocket"
            timeout = 200
            socket_path = "/run/parsec/parsec.sock"

            [authenticator]
            auth_type = "Direct"
            admins = [{ name = "parsec-tool" }, { name = "admin" }]

            [[key_manager]]
            name = "sqlite-manager"
            manager_type = "SQLite"
            sqlite_db_path = "/var/lib/parsec/kim.sqlite3"

            [[provider]]
            name = "mbed-crypto-provider"
            provider_type = "MbedCrypto"
            key_info_manager = "sqlite-manager"

            """.ReplaceLineEndings("\n"),
            content);
    }

    [Fact]
    public void Build_UsesTheLineFeedCharacterOnly()
    {
        var content = ParsecConfigFile.Build(ParsecAuthType.Direct, ParsecLogLevel.Info, "/run/parsec");

        Assert.DoesNotContain('\r', content);
    }

    [Theory]
    [InlineData(ParsecLogLevel.Error, "error")]
    [InlineData(ParsecLogLevel.Warn, "warn")]
    [InlineData(ParsecLogLevel.Info, "info")]
    [InlineData(ParsecLogLevel.Debug, "debug")]
    [InlineData(ParsecLogLevel.Trace, "trace")]
    public void Build_WritesTheLogLevelInLowerCase(ParsecLogLevel logLevel, string expected)
    {
        var content = ParsecConfigFile.Build(ParsecAuthType.Direct, logLevel, "/run/parsec");

        Assert.Contains("log_level = \"" + expected + "\"\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithUnixPeerCredentials_DeclaresNoAdministrator()
    {
        var content = ParsecConfigFile.Build(
            ParsecAuthType.UnixPeerCredentials,
            ParsecLogLevel.Info,
            "/run/parsec");

        Assert.Contains("auth_type = \"UnixPeerCredentials\"\n", content, StringComparison.Ordinal);
        Assert.DoesNotContain("admins", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WritesTheSocketPathBelowTheGivenDirectory()
    {
        var content = ParsecConfigFile.Build(ParsecAuthType.Direct, ParsecLogLevel.Info, "/tmp/parsec-abc12345");

        Assert.Contains("socket_path = \"/tmp/parsec-abc12345/parsec.sock\"\n", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithAnUnknownLogLevel_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => ParsecConfigFile.Build(ParsecAuthType.Direct, (ParsecLogLevel)99, "/run/parsec"));

    [Fact]
    public void Build_WithAnUnknownAuthType_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => ParsecConfigFile.Build((ParsecAuthType)99, ParsecLogLevel.Info, "/run/parsec"));
}
