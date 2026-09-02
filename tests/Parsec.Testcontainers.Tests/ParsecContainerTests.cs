namespace Parsec.Testcontainers.Tests;

// These tests need a reachable Docker endpoint. The definition tests only make a container object,
// but the Testcontainers base types resolve the endpoint while they construct the builder and the
// container. The tests that start a container need Docker for the real reason.
[Trait("Category", "IntegrationTests")]
public sealed class ParsecContainerTests
{
    [Fact]
    public void ContainerSocketPath_WithNoSettings_IsThePathInTheImage()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        Assert.Equal("/run/parsec/parsec.sock", container.ContainerSocketPath);
    }

    [Fact]
    public void ContainerSocketPath_WithASocketDirectory_FollowsTheDirectory()
    {
        var container = DockerRequirement.CreateBuilder()
            .WithSocketDirectory("/run/other/")
            .Build();

        Assert.Equal("/run/other/parsec.sock", container.ContainerSocketPath);
    }

    [Fact]
    public void SocketPath_OnALinuxHost_IsThePathOfTheBindMount()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        if (!ParsecHostSocketDirectory.IsBindMountSupported)
        {
            // Another host system runs the container in a virtual machine. There the socket of
            // the container needs a bridge, and the path of the bridge is the path to use.
            Assert.Equal(container.ContainerSocketPath, container.SocketPath);

            return;
        }

        Assert.Equal(container.HostSocketDirectory?.SocketPath, container.SocketPath);
        Assert.NotEqual(container.ContainerSocketPath, container.SocketPath);
        Assert.True(container.SocketPath.Length <= ParsecHostSocketDirectory.MaxSocketPathLength);
    }

    [Fact]
    public void Endpoint_WithNoSettings_HoldsTheSocketPath()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        Assert.Equal("unix:" + container.SocketPath, container.Endpoint.OriginalString);
        Assert.Equal("unix", container.Endpoint.Scheme);
    }

    [Fact]
    public void Endpoint_WithNoSettings_AgreesWithTheEnvironmentVariableInTheContainer()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        // The variable tells the service and the tools in the container where the socket is, so
        // it holds the path in the container. A client on this machine reads SocketPath, which is
        // the same path only while the container needs no bind mount and no bridge.
        Assert.Equal(
            "unix:" + container.ContainerSocketPath,
            container.Configuration.Environments[ParsecBuilder.EndpointEnvironmentVariable]);
    }

    [Fact]
    public async Task SocketPath_AfterStart_AnswersAPingFromThisMachine()
    {
        await using var container = DockerRequirement.CreateBuilder().Build();

        if (container.HostSocketDirectory is null)
        {
            Assert.Skip("This host has no socket on this machine yet. It needs the bridge.");

            return;
        }

        await container.StartAsync(TestContext.Current.CancellationToken);

        var (status, body) = await RawPing.SendAsync(container.SocketPath, TestContext.Current.CancellationToken);

        Assert.Equal(0, (int)status);

        // The body is the protobuf encoding of the version 1.0 of the wire protocol. The minor
        // version is zero, and protobuf leaves a zero out.
        Assert.Equal(new byte[] { 0x08, 0x01 }, body);
    }

    [Fact]
    public async Task DisposeAsync_RemovesTheSocketDirectoryOfThisMachine()
    {
        var container = DockerRequirement.CreateBuilder().Build();
        var hostSocketDirectory = container.HostSocketDirectory;

        if (hostSocketDirectory is null)
        {
            Assert.Skip("This host makes no socket directory yet. It needs the bridge.");

            return;
        }

        await container.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(hostSocketDirectory.DirectoryPath));

        await container.DisposeAsync();

        Assert.False(Directory.Exists(hostSocketDirectory.DirectoryPath));
    }

    [Fact]
    public async Task PingAsync_AfterStart_Answers()
    {
        await using var container = DockerRequirement.CreateBuilder().Build();

        await container.StartAsync(TestContext.Current.CancellationToken);

        await container.PingAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecParsecToolAsync_WithPing_ReportsTheProtocolVersion()
    {
        await using var container = DockerRequirement.CreateBuilder().Build();

        await container.StartAsync(TestContext.Current.CancellationToken);

        var result = await container.ExecParsecToolAsync(["ping"], TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("1.0", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecParsecToolAsync_WithAnUnknownCommand_GivesTheExitCode()
    {
        await using var container = DockerRequirement.CreateBuilder().Build();

        await container.StartAsync(TestContext.Current.CancellationToken);

        var result = await container.ExecParsecToolAsync("no-such-command");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task ExecParsecToolAsync_WithNullArguments_Throws()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => container.ExecParsecToolAsync(args: (string[])null!));
    }
}
