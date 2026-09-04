using Microsoft.Extensions.DependencyInjection;
using Parsec.Client.Authentication;
using Parsec.Client.Protocol;

namespace Parsec.Client.DependencyInjection.Tests;

/// <summary>
/// Covers what the registration puts in the container, and what the factory does before it has
/// reached a service.
/// </summary>
/// <remarks>
/// Nothing here connects. Registration must not touch the network, and the tests that prove the
/// factory actually talks to a service need a container, so they live in
/// <c>ParsecClientFactoryIntegrationTests</c>.
/// </remarks>
public sealed class ParsecClientServiceCollectionExtensionsTests
{
    [Fact]
    public void TheFactoryIsRegisteredAsASingleton()
    {
        var services = new ServiceCollection();

        _ = services.AddParsecClient();

        var descriptor = Assert.Single(services);

        Assert.Equal(typeof(IParsecClientFactory), descriptor.ServiceType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void RegisteringTwiceLeavesOneFactory()
    {
        // Two factories would mean two clients talking to the same service, each doing the two
        // round trips of a connect, and the application would have no idea which one it had.
        var services = new ServiceCollection();

        _ = services.AddParsecClient();
        _ = services.AddParsecClient(new ParsecClientOptions
        {
            Authentication = new DirectAuthentication("second"),
        });

        _ = Assert.Single(services);
    }

    [Fact]
    public void TheSameFactoryComesBackEveryTime()
    {
        var services = new ServiceCollection();

        _ = services.AddParsecClient();

        using var provider = services.BuildServiceProvider();

        Assert.Same(
            provider.GetRequiredService<IParsecClientFactory>(),
            provider.GetRequiredService<IParsecClientFactory>());
    }

    [Fact]
    public void RegisteringTouchesNoService()
    {
        // Building the container must not connect. An application whose service is briefly down
        // should still start, and find out at the point where it asks for a client.
        var services = new ServiceCollection();

        _ = services.AddParsecClient(new ParsecClientOptions
        {
            Endpoint = new Uri("unix:/nonexistent/parsec.sock"),
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IParsecClientFactory>());
    }

    [Fact]
    public void TheSettingsAreBuiltFromTheContainerOnce()
    {
        var built = 0;
        var services = new ServiceCollection();

        _ = services.AddParsecClient(_ =>
        {
            built++;
            return new ParsecClientOptions { Provider = ProviderId.Tpm };
        });

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IParsecClientFactory>();
        _ = provider.GetRequiredService<IParsecClientFactory>();

        Assert.Equal(1, built);
    }

    [Fact]
    public void TheSettingsCanReadTheServicesAlreadyRegistered()
    {
        var services = new ServiceCollection();

        _ = services.AddSingleton<IParsecAuthentication>(new DirectAuthentication("from-container"));
        _ = services.AddParsecClient(provider => new ParsecClientOptions
        {
            Authentication = provider.GetRequiredService<IParsecAuthentication>(),
        });

        using var container = services.BuildServiceProvider();

        Assert.NotNull(container.GetRequiredService<IParsecClientFactory>());
    }

    [Fact]
    public async Task AFactoryThatCannotReachTheServiceRaisesAndStaysUsable()
    {
        // A connect that failed must not be remembered. A service that was down when the
        // application started should not stay unreachable for the life of the process.
        var missing = new Uri("unix:" + Path.Combine(Path.GetTempPath(), "no-parsec-di.sock")
            .Replace('\\', '/'));
        var services = new ServiceCollection();

        _ = services.AddParsecClient(new ParsecClientOptions { Endpoint = missing });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IParsecClientFactory>();

        _ = await Assert.ThrowsAnyAsync<Errors.ParsecException>(
            async () => await factory.GetAsync(TestContext.Current.CancellationToken));

        // The second call tries again rather than handing back the first failure.
        _ = await Assert.ThrowsAnyAsync<Errors.ParsecException>(
            async () => await factory.GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ADisposedFactoryRefusesToHandOutAClient()
    {
        var services = new ServiceCollection();

        _ = services.AddParsecClient();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IParsecClientFactory>();

        await provider.DisposeAsync();

        var fault = await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await factory.GetAsync(TestContext.Current.CancellationToken));

        // The factory has to be the one saying so. Its semaphore is disposed at the same time
        // and would raise the same type from a line that tells the caller nothing.
        Assert.Equal(typeof(ParsecClientFactory).FullName, fault.ObjectName);
    }

    [Fact]
    public async Task DisposingTwiceIsSafeEitherWay()
    {
        var services = new ServiceCollection();

        _ = services.AddParsecClient();

        var provider = services.BuildServiceProvider();
        var factory = (ParsecClientFactory)provider.GetRequiredService<IParsecClientFactory>();

        await factory.DisposeAsync();
        await factory.DisposeAsync();

        // The synchronous path has to be safe after the asynchronous one, because a service
        // provider that was disposed asynchronously can still be disposed again.
#pragma warning disable CA1849
        factory.Dispose();
#pragma warning restore CA1849

        await provider.DisposeAsync();

        // Still disposed, and still saying so itself rather than letting a released semaphore
        // answer for it.
        var fault = await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await factory.GetAsync(TestContext.Current.CancellationToken));

        Assert.Equal(typeof(ParsecClientFactory).FullName, fault.ObjectName);
    }

    [Fact]
    public async Task AContainerCanBeDisposedSynchronously()
    {
        // A service provider raises when a singleton implements only IAsyncDisposable and the
        // provider is disposed synchronously. An application that does not run under a host does
        // exactly that, and a failure at shutdown is a poor way to learn it.
        var services = new ServiceCollection();

        _ = services.AddParsecClient();

        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IParsecClientFactory>();

        // Disposing the provider synchronously is the whole point of this test, so the rule
        // asking for the asynchronous one has nothing to offer here.
#pragma warning disable CA1849
        provider.Dispose();
#pragma warning restore CA1849

        // Disposing synchronously has to leave the factory as disposed as the other path does.
        var fault = await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await factory.GetAsync(TestContext.Current.CancellationToken));

        Assert.Equal(typeof(ParsecClientFactory).FullName, fault.ObjectName);
    }

    [Fact]
    public void RegisteringRefusesNullArguments()
    {
        var services = new ServiceCollection();

        // The parameter name matters: without the guards the container raises the same type from
        // a line further in and blames one of its own arguments.
        AssertRefuses("services", () => ((IServiceCollection)null!).AddParsecClient());
        AssertRefuses("options", () => services.AddParsecClient((ParsecClientOptions)null!));
        AssertRefuses(
            "options",
            () => services.AddParsecClient((Func<IServiceProvider, ParsecClientOptions>)null!));
        AssertRefuses(
            "services",
            () => ((IServiceCollection)null!).AddParsecClient(_ => new ParsecClientOptions()));
    }

    private static void AssertRefuses(string parameter, Func<IServiceCollection> call)
    {
        var fault = Assert.Throws<ArgumentNullException>(call);

        Assert.Equal(parameter, fault.ParamName);
    }
}
