using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Parsec.Client.DependencyInjection;

/// <summary>
/// Registers the Parsec client with a service collection.
/// </summary>
public static class ParsecClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IParsecClientFactory"/> with the default settings.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <remarks>
    /// The defaults read <c>PARSEC_SERVICE_ENDPOINT</c> and identify nobody, which is enough to
    /// ask the service what it can do and not enough to own a key. An application that touches a
    /// key wants the overload that takes settings.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddParsecClient(this IServiceCollection services) =>
        services.AddParsecClient(new ParsecClientOptions());

    /// <summary>
    /// Registers <see cref="IParsecClientFactory"/> with the settings given.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="options">The settings to build the client from.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddParsecClient(
        this IServiceCollection services,
        ParsecClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return services.AddParsecClient(_ => options);
    }

    /// <summary>
    /// Registers <see cref="IParsecClientFactory"/> with settings built from the container.
    /// </summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="options">Builds the settings from the services already registered.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <remarks>
    /// The factory is a singleton, so <paramref name="options"/> runs once. It runs when the
    /// factory is first resolved and not when the client first connects, so a setting read from
    /// configuration is read before anything reaches the service.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddParsecClient(
        this IServiceCollection services,
        Func<IServiceProvider, ParsecClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // TryAdd rather than Add: calling this twice should leave one client rather than two
        // talking to the same service, and the first registration is the one the application
        // meant.
        services.TryAddSingleton<IParsecClientFactory>(
            provider => new ParsecClientFactory(options(provider)));

        return services;
    }
}
