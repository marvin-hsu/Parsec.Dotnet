using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Parsec.Client.Transport;
using Parsec.Testcontainers;

namespace Parsec.Client.Tests;

/// <summary>
/// Starts the real Parsec service one time for the integration tests of a class.
/// </summary>
/// <remarks>
/// The service runs in a container and the tests reach it through a Unix domain socket on this
/// machine. A machine with no Docker endpoint skips the tests instead of failing them, the same
/// way the module tests do.
/// </remarks>
public sealed class ParsecServiceFixture : IAsyncLifetime
{
    /// <summary>The name that the tests send in the direct authentication field.</summary>
    public const string ApplicationName = "parsec-dotnet-tests";

    private ParsecContainer? _container;

    /// <summary>
    /// Gets the reason to skip the tests, or <see langword="null"/> when the service runs.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>
    /// Gets the address of the service on this machine.
    /// </summary>
    public Uri Endpoint => _container?.Endpoint ?? throw new InvalidOperationException(
        "The container did not start. Call SkipWhenTheServiceDoesNotRun first.");

    /// <summary>
    /// Skips the test when the service does not run on this machine.
    /// </summary>
    public void SkipWhenTheServiceDoesNotRun()
    {
        if (SkipReason is { } reason)
        {
            Assert.Skip(reason);
        }
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        // Testcontainers looks for a Docker endpoint one time and keeps the answer. It gives null
        // when it finds none, for example while the Docker daemon does not run.
        if (TestcontainersSettings.OS.DockerEndpointAuthConfig is null)
        {
            SkipReason = "Testcontainers found no Docker endpoint on this machine.";
            return;
        }

        _container = new ParsecBuilder().Build();

        await _container.StartAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Makes the operations that talk to the service that this fixture started.
    /// </summary>
    /// <returns>The core operations, with the direct authentication of the tests.</returns>
    internal Operations.ParsecCoreOperations CreateOperations() => new(
        new UnixDomainSocketTransport(Endpoint),
        new DirectAuthentication(ApplicationName));
}
