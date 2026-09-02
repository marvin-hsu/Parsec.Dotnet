using System.Net.Http;
using System.Net.Sockets;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;

namespace Parsec.Testcontainers.Tests;

/// <summary>
/// Gives the tests a Docker endpoint, or skips the test when Docker does not answer.
/// </summary>
/// <remarks>
/// The Testcontainers base builder needs a reachable Docker endpoint before it starts a
/// container. It resolves the endpoint and the container operating system while it makes the
/// builder. With no endpoint that step throws, and a test in the integration lane must not fail
/// for that reason. The helper asks the daemon one time with a ping and turns a failure into a
/// skip.
/// </remarks>
internal static class DockerRequirement
{
    private static readonly Lazy<string?> _skipReason = new(FindSkipReason);

    /// <summary>
    /// Skips the test when Docker does not answer a ping.
    /// </summary>
    internal static void SkipWhenDockerDoesNotAnswer()
    {
        if (_skipReason.Value is { } reason)
        {
            Assert.Skip(reason);
        }
    }

    /// <summary>
    /// Makes a new <see cref="ParsecBuilder"/>, or skips the test when Docker does not answer.
    /// </summary>
    /// <returns>A new builder with the default settings.</returns>
    internal static ParsecBuilder CreateBuilder()
    {
        SkipWhenDockerDoesNotAnswer();

        return new ParsecBuilder();
    }

    /// <summary>
    /// Makes a Docker client for the endpoint that Testcontainers uses.
    /// </summary>
    /// <returns>A new client. The caller disposes it.</returns>
    /// <remarks>
    /// A test uses the client to look at the daemon itself, for example to see that a container
    /// is gone after the dispose. Call the method only after
    /// <see cref="SkipWhenDockerDoesNotAnswer"/>, because it needs an endpoint.
    /// </remarks>
    internal static DockerClient CreateClient()
    {
        var authConfig = TestcontainersSettings.OS.DockerEndpointAuthConfig;

        Assert.NotNull(authConfig);

        return authConfig.GetDockerClientBuilder(Guid.Empty).Build();
    }

    /// <summary>
    /// Asks the daemon for a ping.
    /// </summary>
    /// <returns>The reason to skip, or <c>null</c> when Docker answers.</returns>
    private static string? FindSkipReason()
    {
        try
        {
            // Testcontainers looks for an endpoint one time and keeps the answer. It gives null
            // when it finds none, for example while the Docker daemon does not run.
            if (TestcontainersSettings.OS.DockerEndpointAuthConfig is null)
            {
                return "Testcontainers found no Docker endpoint on this machine.";
            }

            using var client = CreateClient();

            client.System.PingAsync(CancellationToken.None).GetAwaiter().GetResult();

            return null;
        }
        catch (Exception exception) when (exception is DockerApiException
            or DockerUnavailableException
            or DotNet.Testcontainers.Builders.DockerConfigurationException
            or HttpRequestException
            or IOException
            or SocketException
            or TimeoutException
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException)
        {
            return "Docker does not answer a ping: " + exception.Message;
        }
    }
}
