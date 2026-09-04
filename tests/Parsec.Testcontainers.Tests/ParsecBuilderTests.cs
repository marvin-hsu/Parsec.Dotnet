using System.Globalization;
using System.Reflection;
using System.Text;
using Parsec.Testcontainers.Configuration;
using Parsec.Testcontainers.Sockets;

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
    public async Task Build_WithAnotherSocketDirectory_WritesANewConfigurationFileAndMovesTheEndpoint()
    {
        var configuration = DockerRequirement.CreateBuilder().WithSocketDirectory("/tmp/parsec-abc12345/").Build().Configuration;

        var content = await ReadConfigFileAsync(configuration);

        Assert.Contains("socket_path = \"/tmp/parsec-abc12345/parsec.sock\"", content, StringComparison.Ordinal);
        Assert.Equal(
            "unix:/tmp/parsec-abc12345/parsec.sock",
            configuration.Environments[ParsecBuilder.EndpointEnvironmentVariable]);
    }

    // One case is enough to show that Build calls the check. ParsecSocketPathTests covers the
    // rules themselves, and needs no Docker endpoint for that.
    [Fact]
    public void Build_WithARelativeSocketDirectory_Throws()
    {
        var builder = DockerRequirement.CreateBuilder().WithSocketDirectory("run/parsec");

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
    public async Task WithImage_KeepsTheSettingsOfThisModule()
    {
        var configuration = DockerRequirement.CreateBuilder()
            .WithAuthType(ParsecAuthType.UnixPeerCredentials)
            .WithImage("ghcr.io/parallaxsecond/parsec-quickstart:latest")
            .Build()
            .Configuration;

        // A With* call of the base class goes through Clone. Both sides of that merge have an
        // assertion here. The base value must win, so the image is the one of the call. A Clone
        // that makes a new ParsecConfiguration instead of a merge drops the authenticator, and
        // the container then runs with the file of the image while the test author believes
        // otherwise.
        Assert.Equal("ghcr.io/parallaxsecond/parsec-quickstart:latest", configuration.Image.FullName);
        Assert.Equal(
            ParsecConfigFile.Build(ParsecAuthType.UnixPeerCredentials, ParsecLogLevel.Info, ParsecImage.SocketDirectory),
            await ReadConfigFileAsync(configuration));
    }

    [Fact]
    public void WithConfigFile_WithAnEmptyPath_Throws()
    {
        var builder = DockerRequirement.CreateBuilder().WithConfigFile(string.Empty);

        _ = Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void WithConfigFile_WithNoFileThere_Throws()
    {
        var missing = Path.Combine(Path.GetTempPath(), "parsec-not-there.toml");

        var builder = DockerRequirement.CreateBuilder().WithConfigFile(missing);

        var exception = Assert.Throws<FileNotFoundException>(builder.Build);
        Assert.Equal(missing, exception.FileName);
    }

    [Fact]
    public void WithConfigFile_AndWithAuthType_Throws()
    {
        var file = WriteConfigFile();

        try
        {
            // The settings write into a file that the build no longer produces, so taking both
            // would drop one of them without a word.
            var builder = DockerRequirement.CreateBuilder()
                .WithConfigFile(file)
                .WithAuthType(ParsecAuthType.UnixPeerCredentials);

            _ = Assert.Throws<InvalidOperationException>(builder.Build);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void WithConfigFile_AndWithLogLevel_Throws()
    {
        var file = WriteConfigFile();

        try
        {
            var builder = DockerRequirement.CreateBuilder()
                .WithConfigFile(file)
                .WithLogLevel(ParsecLogLevel.Debug);

            _ = Assert.Throws<InvalidOperationException>(builder.Build);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task WithConfigFile_PutsTheFileOfTheCallerInTheContainer()
    {
        // A marker that no other configuration carries, so the assertion cannot pass on the
        // file that the image already has or on one that this module writes.
        var file = WriteConfigFile("debug", admin: "a-test-admin");

        try
        {
            var builder = DockerRequirement.CreateBuilder().WithConfigFile(file);
            var mapped = await ReadConfigFileAsync(builder.Build().Configuration);

            Assert.Equal(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), mapped);
            Assert.Contains("a-test-admin", mapped, StringComparison.Ordinal);

            await using var container = DockerRequirement.CreateBuilder().WithConfigFile(file).Build();

            await container.StartAsync(TestContext.Current.CancellationToken);

            // The service started on the file, so the file is valid and reached the container.
            await container.PingAsync(TestContext.Current.CancellationToken);

            // The administrator only exists in the file of this test, so an administrator
            // operation proves which configuration the service read.
            var admin = await container.ExecParsecToolAsync(
                ["list-clients"],
                TestContext.Current.CancellationToken);

            Assert.Equal(0, admin.ExitCode);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static IEnumerable<string> WaitCommands(ParsecConfiguration configuration)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        foreach (var strategy in configuration.WaitStrategies)
        {
            var waitUntil = strategy.GetType().GetField("_waitUntil", flags)?.GetValue(strategy);

            if (waitUntil?.GetType().GetField("_command", flags)?.GetValue(waitUntil) is string[] command)
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

    /// <summary>
    /// Writes a complete service configuration that the image can run, with an administrator
    /// list, which no With method reaches.
    /// </summary>
    /// <param name="logLevel">The value for log_level, as the file spells it.</param>
    /// <param name="admin">A second administrator, so a test can tell this file apart from another.</param>
    /// <returns>The path of the file.</returns>
    private static string WriteConfigFile(string logLevel = "info", string admin = "parsec-tool")
    {
        var path = Path.Combine(Path.GetTempPath(), $"parsec-{Guid.NewGuid():N}.toml");

        var content = $$"""
            [core_settings]
            log_level = "{{logLevel}}"
            allow_root = true

            [listener]
            listener_type = "DomainSocket"
            timeout = 200
            socket_path = "{{ParsecImage.SocketDirectory}}/{{ParsecImage.SocketFileName}}"

            [authenticator]
            auth_type = "Direct"
            admins = [{ name = "parsec-tool" }, { name = "{{admin}}" }]

            [[key_manager]]
            name = "sqlite-manager"
            manager_type = "SQLite"
            sqlite_db_path = "/var/lib/parsec/kim.sqlite3"

            [[provider]]
            name = "mbed-crypto-provider"
            provider_type = "MbedCrypto"
            key_info_manager = "sqlite-manager"

            """;

        File.WriteAllText(path, content);

        return path;
    }
}
