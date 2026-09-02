namespace Parsec.Testcontainers.Tests;

// These tests need a reachable Docker endpoint. The definition tests only make a container object,
// but the Testcontainers base types resolve the endpoint while they construct the builder and the
// container. The tests that start a container need Docker for the real reason.
[Trait("Category", "IntegrationTests")]
public sealed class ParsecContainerTests
{
    [Fact]
    public void SocketPath_WithNoSettings_IsThePathInTheImage()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        Assert.Equal("/run/parsec/parsec.sock", container.SocketPath);
    }

    [Fact]
    public void SocketPath_WithASocketDirectory_FollowsTheDirectory()
    {
        var container = DockerRequirement.CreateBuilder()
            .WithSocketDirectory("/run/other/")
            .Build();

        Assert.Equal("/run/other/parsec.sock", container.SocketPath);
    }

    [Fact]
    public void Endpoint_WithNoSettings_HoldsTheSocketPath()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        Assert.Equal("unix:/run/parsec/parsec.sock", container.Endpoint.OriginalString);
        Assert.Equal("unix", container.Endpoint.Scheme);
    }

    [Fact]
    public void Endpoint_WithNoSettings_AgreesWithTheEnvironmentVariable()
    {
        var container = DockerRequirement.CreateBuilder().Build();

        Assert.Equal(
            container.Configuration.Environments[ParsecBuilder.EndpointEnvironmentVariable],
            container.Endpoint.OriginalString);
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
