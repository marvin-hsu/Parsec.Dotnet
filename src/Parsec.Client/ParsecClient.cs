using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Models;
using Parsec.Client.Operations;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client;

/// <summary>
/// A connection to a Parsec service, bound to one provider of that service.
/// </summary>
/// <remarks>
/// Build one with <see cref="CreateAsync"/>. That call asks the service which version of the
/// protocol it speaks and which providers it runs, so a client that comes back is one that has
/// already talked to a service that answered.
/// </remarks>
public sealed class ParsecClient : IParsecClient
{
    private readonly ParsecCoreOperations _core;

    private ParsecClient(
        UnixDomainSocketTransport transport,
        ParsecCoreOperations core,
        ProviderInfo provider,
        Version wireProtocolVersion,
        ParsecClientOptions options)
    {
        _core = core;

        Provider = provider.Id;
        ProviderName = provider.Description;
        WireProtocolVersion = wireProtocolVersion;

        Keys = new ParsecKeyOperations(transport, options.Authentication, provider.Id);
        Crypto = new ParsecCryptoOperations(transport, options.Authentication, provider.Id);
        Attestation = new ParsecAttestationOperations(transport, options.Authentication, provider.Id);
    }

    /// <inheritdoc/>
    public ProviderId Provider { get; }

    /// <inheritdoc/>
    public string ProviderName { get; }

    /// <inheritdoc/>
    public Version WireProtocolVersion { get; }

    /// <inheritdoc/>
    public IParsecKeyOperations Keys { get; }

    /// <inheritdoc/>
    public IParsecCryptoOperations Crypto { get; }

    /// <inheritdoc/>
    public IParsecAttestationOperations Attestation { get; }

    /// <summary>
    /// Connects to a service and picks the provider to work with.
    /// </summary>
    /// <param name="options">
    /// The settings to build the client from, or <see langword="null"/> for the defaults.
    /// </param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>A client bound to a provider that the service reported.</returns>
    /// <remarks>
    /// Two round trips happen here. The first is a Ping, which agrees the version of the protocol
    /// and proves the service answers. The second is ListProviders, which finds the provider: the
    /// one that <see cref="ParsecClientOptions.Provider"/> names, or the first one that is not
    /// the core provider. Doing this once at the start beats doing it inside every operation, and
    /// it turns a service that is absent or unusable into a failure at the point where the
    /// application can still do something about it.
    /// </remarks>
    /// <exception cref="ParsecConfigurationException">
    /// The endpoint is not one this client can reach, or the service runs no provider that
    /// matches the options.
    /// </exception>
    /// <exception cref="ParsecTransportException">The service could not be reached.</exception>
    /// <exception cref="ParsecServiceException">The service refused one of the two requests.</exception>
    public static async Task<ParsecClient> CreateAsync(
        ParsecClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ParsecClientOptions();

        var transport = new UnixDomainSocketTransport(
            options.Endpoint ?? ParsecEndpoint.Resolve(),
            options.ConnectTimeout,
            options.IoTimeout)
        {
            MaxBodyLength = options.MaxBodyLength,
        };

        var core = new ParsecCoreOperations(transport, options.Authentication);

        var version = await core.PingAsync(cancellationToken).ConfigureAwait(false);
        var providers = await core.ListProvidersAsync(cancellationToken).ConfigureAwait(false);

        return new ParsecClient(transport, core, Choose(providers, options.Provider), version, options);
    }

    /// <inheritdoc/>
    public Task<Version> PingAsync(CancellationToken cancellationToken = default) =>
        _core.PingAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ProviderInfo>> ListProvidersAsync(
        CancellationToken cancellationToken = default) =>
        _core.ListProvidersAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlySet<Opcode>> ListOpcodesAsync(
        ProviderId provider,
        CancellationToken cancellationToken = default) =>
        _core.ListOpcodesAsync(provider, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AuthenticatorInfo>> ListAuthenticatorsAsync(
        CancellationToken cancellationToken = default) =>
        _core.ListAuthenticatorsAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<KeyInfo>> ListKeysAsync(CancellationToken cancellationToken = default) =>
        _core.ListKeysAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<bool> CanDoCryptoAsync(
        KeyCheckType checkType,
        KeyAttributes attributes,
        CancellationToken cancellationToken = default) =>
        _core.CanDoCryptoAsync(Provider, checkType, attributes, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// There is nothing to release today. A client holds no connection between calls: each
    /// operation opens a socket, exchanges one message and closes it. The interface says
    /// <see cref="IAsyncDisposable"/> so that applications write <c>await using</c> from the
    /// start, because a client that later pools connections would otherwise leak them in every
    /// application already written against it.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Picks the provider to bind the client to.
    /// </summary>
    /// <param name="providers">The providers that the service reported.</param>
    /// <param name="wanted">The provider that the caller named, or <see langword="null"/>.</param>
    /// <returns>The provider to work with.</returns>
    /// <exception cref="ParsecConfigurationException">No provider matches.</exception>
    private static ProviderInfo Choose(IReadOnlyList<ProviderInfo> providers, ProviderId? wanted)
    {
        if (wanted is { } id)
        {
            return providers.FirstOrDefault(provider => provider.Id == id)
                ?? throw new ParsecConfigurationException(
                    ParsecErrorText.DescribeMissingProvider(id, providers));
        }

        // The core provider runs no cryptography, so binding to it would leave the client unable
        // to do the work it exists for.
        return providers.FirstOrDefault(provider => provider.Id != ProviderId.Core)
            ?? throw new ParsecConfigurationException(
                ParsecErrorText.DescribeNoCryptographicProvider());
    }
}
