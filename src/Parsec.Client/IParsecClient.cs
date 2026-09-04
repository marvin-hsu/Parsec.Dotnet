using Parsec.Client.Keys;
using Parsec.Client.Models;
using Parsec.Client.Operations;
using Parsec.Client.Protocol;

namespace Parsec.Client;

/// <summary>
/// A connection to a Parsec service, bound to one provider of that service.
/// </summary>
/// <remarks>
/// The members on this interface are the ones that belong to the service as a whole. The
/// operations that work on keys sit under <see cref="Keys"/>, <see cref="Crypto"/> and
/// <see cref="Attestation"/>, all three bound to the provider that <see cref="Provider"/> names.
/// <para>
/// A client holds no connection between calls. Each operation opens a socket, sends one request,
/// reads one answer and closes it, which is what the wire protocol expects.
/// </para>
/// </remarks>
public interface IParsecClient : IAsyncDisposable
{
    /// <summary>Gets the provider that the key operations of this client work on.</summary>
    public ProviderId Provider { get; }

    /// <summary>Gets the description that the service gives for that provider.</summary>
    public string ProviderName { get; }

    /// <summary>
    /// Gets the highest version of the wire protocol that the service reported when this client
    /// was built.
    /// </summary>
    public Version WireProtocolVersion { get; }

    /// <summary>Gets the operations that create, remove and read back keys.</summary>
    public IParsecKeyOperations Keys { get; }

    /// <summary>Gets the operations that use a key, and the two that need none.</summary>
    public IParsecCryptoOperations Crypto { get; }

    /// <summary>Gets the operations that prove where a key was created.</summary>
    public IParsecAttestationOperations Attestation { get; }

    /// <summary>
    /// Asks the service for the highest version of the wire protocol that it supports.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The version that the service reported.</returns>
    /// <remarks>
    /// This carries no authentication, so it also serves as a check that the service is there and
    /// answering.
    /// </remarks>
    public Task<Version> PingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the service which providers it runs.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The providers, with the core provider among them.</returns>
    public Task<IReadOnlyList<ProviderInfo>> ListProvidersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the service which operations a provider runs.
    /// </summary>
    /// <param name="provider">The provider to ask about.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The operations that the provider offers.</returns>
    /// <remarks>
    /// Worth asking before reaching for an operation that only some providers carry. It is
    /// cheaper than finding out from a failed request, and it distinguishes an operation the
    /// provider will not run from one the service does not implement at all.
    /// </remarks>
    public Task<IReadOnlySet<Opcode>> ListOpcodesAsync(
        ProviderId provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the service which authenticators it runs.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The authenticators, in the order the service prefers them.</returns>
    public Task<IReadOnlyList<AuthenticatorInfo>> ListAuthenticatorsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the service for the keys of this application.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// The keys of the application that this client authenticates as, across every provider.
    /// </returns>
    public Task<IReadOnlyList<KeyInfo>> ListKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the provider of this client whether it can work with a set of key attributes.
    /// </summary>
    /// <param name="checkType">The use that the question is about.</param>
    /// <param name="attributes">The attributes of the key.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// <see langword="true"/> when the provider accepts the attributes, and
    /// <see langword="false"/> when it does not support them.
    /// </returns>
    public Task<bool> CanDoCryptoAsync(
        KeyCheckType checkType,
        KeyAttributes attributes,
        CancellationToken cancellationToken = default);
}
