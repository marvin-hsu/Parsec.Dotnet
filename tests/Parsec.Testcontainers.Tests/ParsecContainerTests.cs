using System.Net.Sockets;
using Docker.DotNet;
using Parsec.Testcontainers.Sockets;

namespace Parsec.Testcontainers.Tests;

// These tests need a reachable Docker endpoint. The definition tests only make a container object,
// but the Testcontainers base types resolve the endpoint while they construct the builder and the
// container. The tests that start a container need Docker for the real reason.
[Collection(DockerTestGroup.Name)]
[Trait("Category", "IntegrationTests")]
public sealed class ParsecContainerTests
{
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
    public async Task StartAsync_WithSettingsThatTheImageDoesNotHave_ServesTheService()
    {
        await using var container = DockerRequirement.CreateBuilder()
            .WithAuthType(ParsecAuthType.UnixPeerCredentials)
            .WithLogLevel(ParsecLogLevel.Debug)
            .Build();

        // This is the one test that gives the file of ParsecConfigFile to the real service. The
        // wait strategy of the builder runs the tool in the container, so a file with a key that
        // the schema does not have, or an administrator list that the authenticator rejects, ends
        // the start here and not in the test suite of a user.
        await container.StartAsync(TestContext.Current.CancellationToken);

        var (status, _) = await RawPing.SendAsync(container.SocketPath, TestContext.Current.CancellationToken);

        Assert.Equal(0, (int)status);
    }

    [Fact]
    public async Task StopAsync_WithNoDispose_ClosesTheSocketOnThisMachine()
    {
        await using var container = DockerRequirement.CreateBuilder().Build();

        await container.StartAsync(TestContext.Current.CancellationToken);

        var (status, _) = await RawPing.SendAsync(container.SocketPath, TestContext.Current.CancellationToken);

        Assert.Equal(0, (int)status);

        // A user can stop a container in the teardown of a fixture and start it again. The stop
        // closes the bridge of this machine before it stops the container, so no listener holds
        // the socket file, and nothing on this machine answers on the path any more.
        await container.StopAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAnyAsync<SocketException>(
            () => RawPing.SendAsync(container.SocketPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposeAsync_AfterAFailedCreate_RemovesTheDirectoryAndTakesASecondCall()
    {
        var container = DockerRequirement.CreateBuilder().Build();
        var hostSocketDirectory = container.HostSocketDirectory;

        Assert.NotNull(hostSocketDirectory);

        // The container makes the directory in UnsafeCreateAsync, before Docker makes the
        // container. A pull that fails, or a test that fails before the start, leaves this
        // state: the directory is on the disk and no container ever ran. The dispose must remove
        // the directory, and a second dispose must not throw.
        hostSocketDirectory.MakeDirectory();

        Assert.True(Directory.Exists(hostSocketDirectory.DirectoryPath));

        await container.DisposeAsync();
        await container.DisposeAsync();

        Assert.False(Directory.Exists(hostSocketDirectory.DirectoryPath));
    }

    [Fact]
    public async Task StartAsync_WithAnImageThatIsNotThere_FailsAndLeavesNothing()
    {
        // The earlier dispose test puts the object into the failed state by hand. This one lets
        // Docker fail for real, so it also covers the message a caller reads and the cleanup that
        // runs after a start the module did not expect to fail.
        await using var container = DockerRequirement.CreateBuilder()
            .WithImage("ghcr.io/marvin-hsu/parsec-testcontainers:no-such-tag-exists")
            .Build();

        var hostSocketDirectory = container.HostSocketDirectory;

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => container.StartAsync(TestContext.Current.CancellationToken));

        // A caller who reads only the message has to be able to tell that the image is the
        // problem, without a stack trace and without the Docker logs.
        var message = exception.ToString();

        Assert.Contains("no-such-tag-exists", message, StringComparison.Ordinal);

        // The container never ran, so nothing of it may stay behind on this machine.
        await container.DisposeAsync();

        if (hostSocketDirectory is not null)
        {
            Assert.False(
                Directory.Exists(hostSocketDirectory.DirectoryPath),
                hostSocketDirectory.DirectoryPath + " is still there.");
        }
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

        // The method gives the result of the tool and throws on no exit code, because the caller
        // reads the exit code itself.
        var unknown = await container.ExecParsecToolAsync(["no-such-command"], TestContext.Current.CancellationToken);

        Assert.NotEqual(0, unknown.ExitCode);

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
