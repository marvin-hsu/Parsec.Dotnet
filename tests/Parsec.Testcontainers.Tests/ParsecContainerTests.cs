using Docker.DotNet;

namespace Parsec.Testcontainers.Tests;

// These tests need a reachable Docker endpoint. The definition tests only make a container object,
// but the Testcontainers base types resolve the endpoint while they construct the builder and the
// container. The tests that start a container need Docker for the real reason.
[Collection(DockerTestGroup.Name)]
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
    public void SocketPath_WithNoSettings_IsAShortPathOnThisMachine()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        // A Linux host gets the socket of the container through a bind mount, and another host
        // system gets the socket of the bridge. Both are in the same directory of this machine.
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

        Assert.NotNull(hostSocketDirectory);

        await container.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(hostSocketDirectory.DirectoryPath));

        await container.DisposeAsync();

        Assert.False(Directory.Exists(hostSocketDirectory.DirectoryPath));
    }

    [Fact]
    public async Task SocketPath_AfterStart_TakesMoreThanOneConnection()
    {
        await using var container = DockerRequirement.CreateBuilder().Build();

        await container.StartAsync(TestContext.Current.CancellationToken);

        // A client opens a connection for each request, so the path must stay usable.
        var (firstStatus, _) = await RawPing.SendAsync(container.SocketPath, TestContext.Current.CancellationToken);
        var (secondStatus, _) = await RawPing.SendAsync(container.SocketPath, TestContext.Current.CancellationToken);

        Assert.Equal(0, (int)firstStatus);
        Assert.Equal(0, (int)secondStatus);
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

        // The test log keeps the answer of the tool. A run with detailed output shows it, which
        // makes the wire path visible in a continuous integration log.
        TestContext.Current.TestOutputHelper?.WriteLine("parsec-tool ping: " + result.Stdout.Trim());

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

    [Fact]
    public async Task Container_FromStartToDispose_ServesTheServiceAndLeavesNothing()
    {
        var container = DockerRequirement.CreateBuilder().Build();
        var hostSocketDirectory = container.HostSocketDirectory;

        Assert.NotNull(hostSocketDirectory);

        await container.StartAsync(TestContext.Current.CancellationToken);

        var id = container.Id;

        await container.PingAsync(TestContext.Current.CancellationToken);

        var providers = await container.ExecParsecToolAsync(["list-providers"], TestContext.Current.CancellationToken);

        Assert.Equal(0, providers.ExitCode);
        Assert.Contains("Mbed Crypto", providers.Stdout, StringComparison.Ordinal);

        await container.DisposeAsync();

        // The dispose must leave neither a container in the daemon nor a directory on this
        // machine, because a test run makes many containers.
        using var client = DockerRequirement.CreateClient();

        _ = await Assert.ThrowsAsync<DockerContainerNotFoundException>(
            () => client.Containers.InspectContainerAsync(id, TestContext.Current.CancellationToken));

        Assert.False(Directory.Exists(hostSocketDirectory.DirectoryPath));
    }

    [Fact]
    public async Task TwoContainers_StartedAtTheSameTime_ShareNoPathAndNoPort()
    {
        await using var first = DockerRequirement.CreateBuilder().Build();
        await using var second = DockerRequirement.CreateBuilder().Build();

        await Task.WhenAll(
            first.StartAsync(TestContext.Current.CancellationToken),
            second.StartAsync(TestContext.Current.CancellationToken));

        Assert.NotEqual(first.SocketPath, second.SocketPath);

        if (first.NeedsSocketBridge)
        {
            // Each bridge takes a port of this machine that Docker gives it.
            Assert.NotEqual(
                first.GetMappedPublicPort(ParsecSocketBridge.PortInContainer),
                second.GetMappedPublicPort(ParsecSocketBridge.PortInContainer));
        }

        // Both sockets must answer, which shows that the second container did not take the socket
        // or the port of the first one.
        var (firstStatus, _) = await RawPing.SendAsync(first.SocketPath, TestContext.Current.CancellationToken);
        var (secondStatus, _) = await RawPing.SendAsync(second.SocketPath, TestContext.Current.CancellationToken);

        Assert.Equal(0, (int)firstStatus);
        Assert.Equal(0, (int)secondStatus);
    }
}
