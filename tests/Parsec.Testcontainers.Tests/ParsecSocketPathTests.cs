namespace Parsec.Testcontainers.Tests;

// These tests need no Docker endpoint. They look at the socket path of the container and at the
// check that the builder makes before it starts.
public sealed class ParsecSocketPathTests
{
    [Fact]
    public void DirectoryInContainer_WithNoSetting_IsTheDirectoryOfTheImage()
        => Assert.Equal(
            ParsecImage.SocketDirectory,
            ParsecSocketPath.DirectoryInContainer(new ParsecConfiguration()));

    [Theory]
    [InlineData("/run/other", "/run/other")]
    [InlineData("/run/other/", "/run/other")]
    [InlineData("/run/other///", "/run/other")]
    public void DirectoryInContainer_WithASetting_DropsTheTrailingSlash(string given, string expected)
        => Assert.Equal(
            expected,
            ParsecSocketPath.DirectoryInContainer(new ParsecConfiguration(socketDirectory: given)));

    [Fact]
    public void InContainer_PutsTheSocketFileInTheDirectory()
        => Assert.Equal(
            "/run/other/" + ParsecImage.SocketFileName,
            ParsecSocketPath.InContainer(new ParsecConfiguration(socketDirectory: "/run/other/")));

    [Fact]
    public void ValidateDirectory_WithNoSetting_TakesTheValue()
        => ParsecSocketPath.ValidateDirectory(null);

    [Theory]
    [InlineData("/run/parsec")]
    [InlineData("/run/parsec/")]
    [InlineData("/tmp/parsec-abc12345")]
    public void ValidateDirectory_WithAnAbsolutePath_TakesTheValue(string socketDirectory)
        => ParsecSocketPath.ValidateDirectory(socketDirectory);

    [Fact]
    public void ValidateDirectory_WithAnEmptyPath_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => ParsecSocketPath.ValidateDirectory(string.Empty));

        Assert.Equal(nameof(ParsecConfiguration.SocketDirectory), exception.ParamName);
    }

    [Theory]
    [InlineData("run/parsec")]
    [InlineData("./run/parsec")]
    public void ValidateDirectory_WithARelativePath_Throws(string socketDirectory)
    {
        var exception = Assert.Throws<ArgumentException>(() => ParsecSocketPath.ValidateDirectory(socketDirectory));

        Assert.Equal(nameof(ParsecConfiguration.SocketDirectory), exception.ParamName);
        Assert.Contains("absolute path", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("//")]
    public void ValidateDirectory_WithTheRootDirectory_Throws(string socketDirectory)
    {
        var exception = Assert.Throws<ArgumentException>(() => ParsecSocketPath.ValidateDirectory(socketDirectory));

        Assert.Equal(nameof(ParsecConfiguration.SocketDirectory), exception.ParamName);
        Assert.Contains("root directory", exception.Message, StringComparison.Ordinal);
    }
}
