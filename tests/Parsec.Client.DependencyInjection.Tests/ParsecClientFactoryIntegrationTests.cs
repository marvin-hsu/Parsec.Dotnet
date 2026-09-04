using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;
using Parsec.Testcontainers;

namespace Parsec.Client.DependencyInjection.Tests;

/// <summary>
/// Registers a client the way an application would, against the real Parsec service.
/// </summary>
/// <remarks>
/// The unit tests check what registration puts in the container without connecting. This one
/// checks the other half: that what comes out of the container is a client that works, and that
/// disposing the container disposes it.
/// </remarks>
[Trait("Category", "IntegrationTests")]
public sealed class ParsecClientFactoryIntegrationTests : IAsyncLifetime
{
    private const string ApplicationName = "parsec-dotnet-di-tests";

    private ParsecContainer? _container;
    private string? _skipReason;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        if (TestcontainersSettings.OS.DockerEndpointAuthConfig is null)
        {
            _skipReason = "Testcontainers found no Docker endpoint on this machine.";
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

    [Fact]
    public async Task AClientResolvedFromTheContainerTalksToTheService()
    {
        SkipWhenTheServiceDoesNotRun();

        var services = new ServiceCollection();

        _ = services.AddParsecClient(new ParsecClientOptions
        {
            Endpoint = _container!.Endpoint,
            Authentication = new DirectAuthentication(ApplicationName),
        });

        await using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IParsecClientFactory>();
        var client = await factory.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderId.MbedCrypto, client.Provider);
        Assert.Equal(new Version(1, 0), client.WireProtocolVersion);

        // A client that connects but cannot work is not a client. One operation proves the
        // authentication came through the registration as well as the endpoint.
        var name = $"di-{Guid.NewGuid():N}";

        await client.Keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(),
            TestContext.Current.CancellationToken);

        try
        {
            var keys = await client.ListKeysAsync(TestContext.Current.CancellationToken);

            Assert.Contains(keys, key => key.Name == name);
        }
        finally
        {
            await client.Keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TheSameClientComesBackAndConnectsOnlyOnce()
    {
        SkipWhenTheServiceDoesNotRun();

        var services = new ServiceCollection();

        _ = services.AddParsecClient(new ParsecClientOptions
        {
            Endpoint = _container!.Endpoint,
            Authentication = new DirectAuthentication(ApplicationName),
        });

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IParsecClientFactory>();

        var first = await factory.GetAsync(TestContext.Current.CancellationToken);
        var second = await factory.GetAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task CallersThatArriveTogetherWaitOnOneConnect()
    {
        // Without the gate this would build a client per caller, each doing the two round trips
        // of a connect, and all but one would be thrown away.
        SkipWhenTheServiceDoesNotRun();

        var services = new ServiceCollection();

        _ = services.AddParsecClient(new ParsecClientOptions
        {
            Endpoint = _container!.Endpoint,
            Authentication = new DirectAuthentication(ApplicationName),
        });

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IParsecClientFactory>();

        var clients = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            async _ => await factory.GetAsync(TestContext.Current.CancellationToken)));

        Assert.Single(clients.Distinct());
    }

    [Fact]
    public async Task DisposingTheContainerDisposesTheClient()
    {
        SkipWhenTheServiceDoesNotRun();

        var services = new ServiceCollection();

        _ = services.AddParsecClient(new ParsecClientOptions
        {
            Endpoint = _container!.Endpoint,
            Authentication = new DirectAuthentication(ApplicationName),
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IParsecClientFactory>();

        _ = await factory.GetAsync(TestContext.Current.CancellationToken);

        await provider.DisposeAsync();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await factory.GetAsync(TestContext.Current.CancellationToken));
    }

    private void SkipWhenTheServiceDoesNotRun()
    {
        if (_skipReason is { } reason)
        {
            Assert.Skip(reason);
        }
    }
}
