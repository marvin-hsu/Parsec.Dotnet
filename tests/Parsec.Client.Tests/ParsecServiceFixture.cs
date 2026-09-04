using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Parsec.Client.Authentication;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;
using Parsec.Testcontainers;

namespace Parsec.Client.Tests;

/// <summary>
/// Starts the real Parsec service one time for every integration test of this project.
/// </summary>
/// <remarks>
/// The service runs in a container and the tests reach it through a Unix domain socket on this
/// machine. A machine with no Docker endpoint skips the tests instead of failing them, the same
/// way the module tests do.
/// <para>
/// The container starts on the first call to <see cref="StartOrSkipAsync"/> rather than when the
/// fixture is built. The fixture belongs to a collection that unit tests also sit in, so building
/// it must not cost a container on a run that asks for no integration test at all.
/// </para>
/// </remarks>
public sealed class ParsecServiceFixture : IAsyncLifetime
{
    /// <summary>The name that the tests send in the direct authentication field.</summary>
    public const string ApplicationName = "parsec-dotnet-tests";

    private readonly SemaphoreSlim _gate = new(1, 1);

    private ParsecContainer? _container;
    private bool _started;

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
    /// Starts the service if it is not running yet, and skips the test when this machine cannot
    /// run it.
    /// </summary>
    /// <param name="cancellationToken">Stops the start.</param>
    /// <returns>A task that completes once the service answers.</returns>
    public async Task StartOrSkipAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_started)
            {
                _started = true;

                // Testcontainers looks for a Docker endpoint one time and keeps the answer. It
                // gives null when it finds none, for example while the Docker daemon does not run.
                if (TestcontainersSettings.OS.DockerEndpointAuthConfig is null)
                {
                    SkipReason = "Testcontainers found no Docker endpoint on this machine.";
                }
                else
                {
                    _container = new ParsecBuilder().Build();

                    await _container.StartAsync(cancellationToken);
                }
            }
        }
        finally
        {
            _ = _gate.Release();
        }

        if (SkipReason is { } reason)
        {
            Assert.Skip(reason);
        }
    }

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        _gate.Dispose();
    }

    /// <summary>
    /// Makes the operations that talk to the service that this fixture started.
    /// </summary>
    /// <returns>The core operations, with the direct authentication of the tests.</returns>
    internal Operations.ParsecCoreOperations CreateOperations() => new(
        new UnixDomainSocketTransport(Endpoint),
        new DirectAuthentication(ApplicationName));

    /// <summary>
    /// Makes the key operations that talk to the software provider of the service.
    /// </summary>
    /// <returns>The key operations, with the direct authentication of the tests.</returns>
    /// <remarks>
    /// The image carries Mbed Crypto and nothing else, so every key test runs against the
    /// software provider. What that provider supports is what these tests can cover.
    /// </remarks>
    internal Operations.ParsecKeyOperations CreateKeyOperations() => new(
        new UnixDomainSocketTransport(Endpoint),
        new DirectAuthentication(ApplicationName),
        ProviderId.MbedCrypto);

    /// <summary>
    /// Makes the operations that use a key of the software provider of the service.
    /// </summary>
    /// <returns>The crypto operations, with the direct authentication of the tests.</returns>
    internal Operations.ParsecCryptoOperations CreateCryptoOperations() => new(
        new UnixDomainSocketTransport(Endpoint),
        new DirectAuthentication(ApplicationName),
        ProviderId.MbedCrypto);

    /// <summary>
    /// Makes the attestation operations against one provider of the service.
    /// </summary>
    /// <param name="provider">The provider to ask.</param>
    /// <returns>The attestation operations, with the direct authentication of the tests.</returns>
    internal Operations.ParsecAttestationOperations CreateAttestationOperations(ProviderId provider) => new(
        new UnixDomainSocketTransport(Endpoint),
        new DirectAuthentication(ApplicationName),
        provider);
}
