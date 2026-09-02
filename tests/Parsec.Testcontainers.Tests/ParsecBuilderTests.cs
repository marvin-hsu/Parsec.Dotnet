using System.Globalization;
using System.Reflection;
using System.Text;

namespace Parsec.Testcontainers.Tests;

// These tests assert on the container definition only. They never start a container. They still
// need a reachable Docker endpoint, because the Testcontainers base builder resolves the endpoint
// and the container operating system while it constructs the builder. The trait keeps the unit
// test lane free of that requirement. Each test makes the builder with
// DockerRequirement.CreateBuilder, which skips the test when Docker does not answer.
[Trait("Category", "IntegrationTests")]
public sealed class ParsecBuilderTests
{
    [Fact]
    public void Build_WithNoSettings_UsesThePinnedImage()
    {
        var configuration = DockerRequirement.CreateBuilder().Build().Configuration;

        Assert.Equal(ParsecImage.Reference, configuration.Image.FullName);
    }

    [Fact]
    public void Build_WithNoSettings_KeepsTheConfigurationFileOfTheImage()
    {
        var configuration = DockerRequirement.CreateBuilder().Build().Configuration;

        Assert.Null(FindConfigFile(configuration));
    }

    [Fact]
    public void Build_WithNoSettings_SetsTheEndpointVariable()
    {
        var configuration = DockerRequirement.CreateBuilder().Build().Configuration;

        Assert.Equal(
            "unix:/run/parsec/parsec.sock",
            configuration.Environments[ParsecBuilder.EndpointEnvironmentVariable]);
    }

    [Fact]
    public void Build_WithNoSettings_WaitsForTheServiceCommand()
    {
        var configuration = DockerRequirement.CreateBuilder().Build().Configuration;

        // The base builder always adds a strategy that waits for a running container. Only the
        // command strategy comes from this module, so the test looks for that command.
        Assert.Contains("parsec-tool ping", WaitCommands(configuration));
    }

    [Fact]
    public void Build_WithTheSettingsOfTheImage_KeepsTheConfigurationFileOfTheImage()
    {
        var configuration = DockerRequirement.CreateBuilder()
            .WithAuthType(ParsecImage.DefaultAuthType)
            .WithLogLevel(ParsecImage.DefaultLogLevel)
            .WithSocketDirectory(ParsecImage.SocketDirectory)
            .Build()
            .Configuration;

        Assert.Null(FindConfigFile(configuration));
    }

    [Fact]
    public async Task Build_WithAnotherLogLevel_WritesANewConfigurationFile()
    {
        var configuration = DockerRequirement.CreateBuilder().WithLogLevel(ParsecLogLevel.Debug).Build().Configuration;

        var content = await ReadConfigFileAsync(configuration);

        Assert.Equal(
            ParsecConfigFile.Build(ParsecAuthType.Direct, ParsecLogLevel.Debug, ParsecImage.SocketDirectory),
            content);
    }

    [Fact]
    public async Task Build_WithAnotherAuthType_WritesANewConfigurationFile()
    {
        var configuration = DockerRequirement.CreateBuilder().WithAuthType(ParsecAuthType.UnixPeerCredentials).Build().Configuration;

        var content = await ReadConfigFileAsync(configuration);

        Assert.Equal(
            ParsecConfigFile.Build(ParsecAuthType.UnixPeerCredentials, ParsecLogLevel.Info, ParsecImage.SocketDirectory),
            content);
    }

    [Fact]
    public async Task Build_WithAnotherSocketDirectory_WritesANewConfigurationFileAndMovesTheEndpoint()
    {
        var configuration = DockerRequirement.CreateBuilder().WithSocketDirectory("/tmp/parsec-abc12345/").Build().Configuration;

        var content = await ReadConfigFileAsync(configuration);

        Assert.Contains("socket_path = \"/tmp/parsec-abc12345/parsec.sock\"", content, StringComparison.Ordinal);
        Assert.Equal(
            "unix:/tmp/parsec-abc12345/parsec.sock",
            configuration.Environments[ParsecBuilder.EndpointEnvironmentVariable]);
    }

    [Fact]
    public void Build_WithARelativeSocketDirectory_Throws()
    {
        var builder = DockerRequirement.CreateBuilder().WithSocketDirectory("run/parsec");

        var exception = Assert.Throws<ArgumentException>(builder.Build);

        Assert.Equal(nameof(ParsecConfiguration.SocketDirectory), exception.ParamName);
    }

    [Fact]
    public void Build_WithAnEmptySocketDirectory_Throws()
    {
        var builder = DockerRequirement.CreateBuilder().WithSocketDirectory(string.Empty);

        _ = Assert.Throws<ArgumentException>(builder.Build);
    }

    [Fact]
    public void Build_WithTheRootSocketDirectory_Throws()
    {
        var builder = DockerRequirement.CreateBuilder().WithSocketDirectory("/");

        var exception = Assert.Throws<ArgumentException>(builder.Build);

        Assert.Equal(nameof(ParsecConfiguration.SocketDirectory), exception.ParamName);
    }

    [Fact]
    public void WithSocketDirectory_KeepsTheEarlierBuilderUnchanged()
    {
        var builder = DockerRequirement.CreateBuilder();

        _ = builder.WithSocketDirectory("/tmp/parsec-abc12345");

        Assert.Equal(
            "unix:/run/parsec/parsec.sock",
            builder.Build().Configuration.Environments[ParsecBuilder.EndpointEnvironmentVariable]);
    }

    [Fact]
    public void Build_OnALinuxHost_MountsADirectoryOfThisMachineOverTheSocketDirectory()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        var mount = FindSocketMount(container.Configuration, ParsecImage.SocketDirectory);

        if (!ParsecHostSocketDirectory.IsBindMountSupported)
        {
            // Another host system runs the container in a virtual machine, where a bind mount
            // shows the socket file but carries no connection. Such a host gets a bridge, so the
            // container maps the port of the bridge instead of the directory.
            Assert.Null(mount);
            Assert.True(container.NeedsSocketBridge);
            Assert.Contains(
                ParsecSocketBridge.PortInContainer.ToString(CultureInfo.InvariantCulture),
                container.Configuration.PortBindings!.Keys);

            return;
        }

        Assert.False(container.NeedsSocketBridge);
        Assert.NotNull(mount);
        Assert.NotNull(container.HostSocketDirectory);
        Assert.Equal(MountType.Bind, mount.Type);
        Assert.Equal(AccessMode.ReadWrite, mount.AccessMode);
        Assert.Equal(container.HostSocketDirectory.DirectoryPath, mount.Source);
    }

    [Fact]
    public void Build_WithASocketDirectory_MountsOverThatDirectory()
    {
        if (!ParsecHostSocketDirectory.IsBindMountSupported)
        {
            Assert.Skip("This host gets a bridge instead of a bind mount.");

            return;
        }

        var container = DockerRequirement.CreateBuilder().WithSocketDirectory("/run/other/").Build();

        Assert.NotNull(FindSocketMount(container.Configuration, "/run/other"));
    }

    [Fact]
    public void Build_CalledTwice_GivesEachContainerItsOwnHostSocketDirectory()
    {
        var builder = DockerRequirement.CreateBuilder();

        var first = builder.Build();
        var second = builder.Build();

        Assert.NotNull(first.HostSocketDirectory);
        Assert.NotNull(second.HostSocketDirectory);

        // Two containers must not share a socket, because tests can run at the same time.
        Assert.NotEqual(first.HostSocketDirectory.DirectoryPath, second.HostSocketDirectory.DirectoryPath);
        Assert.NotEqual(first.SocketPath, second.SocketPath);
    }

    [Fact]
    public void WithImage_ReplacesThePinnedImage()
    {
        var configuration = DockerRequirement.CreateBuilder()
            .WithImage("ghcr.io/parallaxsecond/parsec-quickstart:latest")
            .Build()
            .Configuration;

        Assert.Equal("ghcr.io/parallaxsecond/parsec-quickstart:latest", configuration.Image.FullName);
    }

    /// <summary>
    /// Gets the command of each wait strategy that runs a command in the container.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    /// <returns>Each command, with a space between the parts.</returns>
    /// <remarks>
    /// Testcontainers keeps the command in a private field, and gives no public way to read it
    /// back. The test reads the field, because a test that only counts the strategies cannot
    /// tell a missing command strategy from the strategy that the base builder adds. The test
    /// fails when a new version of the library changes the fields.
    /// </remarks>
    private static IEnumerable<string> WaitCommands(ParsecConfiguration configuration)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        foreach (var strategy in configuration.WaitStrategies)
        {
            var waitUntil = strategy.GetType().GetField("_waitUntil", Flags)?.GetValue(strategy);

            if (waitUntil?.GetType().GetField("_command", Flags)?.GetValue(waitUntil) is string[] command)
            {
                yield return string.Join(' ', command);
            }
        }
    }

    private static IMount? FindSocketMount(ParsecConfiguration configuration, string target)
        => configuration.Mounts.SingleOrDefault(mount => mount.Target == target);

    private static IResourceMapping? FindConfigFile(ParsecConfiguration configuration)
        => configuration.ResourceMappings.SingleOrDefault(
            mapping => mapping.Target == ParsecImage.ConfigFilePath);

    private static async Task<string> ReadConfigFileAsync(ParsecConfiguration configuration)
    {
        var mapping = FindConfigFile(configuration);

        Assert.NotNull(mapping);
        Assert.Equal(Unix.FileMode644, mapping.FileMode);

        return Encoding.UTF8.GetString(await mapping.GetAllBytesAsync(TestContext.Current.CancellationToken));
    }
}
