namespace Parsec.Client.DependencyInjection;

/// <summary>
/// Hands out the one <see cref="IParsecClient"/> of the application, connecting on first use.
/// </summary>
/// <remarks>
/// Building a client is asynchronous, because it asks the service for its protocol version and
/// its providers before it hands anything back. A service collection cannot await, so the client
/// cannot be registered directly and this factory is registered instead.
/// <para>
/// The alternative would be a client that pretends to exist before it has connected, and whose
/// <see cref="IParsecClient.Provider"/> and <see cref="IParsecClient.ProviderName"/> have no
/// answer to give until it has. One extra <c>await</c> at the point of use is a smaller price
/// than a property that throws.
/// </para>
/// </remarks>
public interface IParsecClientFactory
{
    /// <summary>
    /// Gets the client, connecting to the service if this is the first call.
    /// </summary>
    /// <param name="cancellationToken">Stops the connect.</param>
    /// <returns>The client, which is the same instance on every call.</returns>
    /// <remarks>
    /// Callers that arrive together wait on one connect rather than starting several. A connect
    /// that fails is not remembered, so the next call tries again: a service that was down when
    /// the application started should not stay unreachable for the life of the process.
    /// </remarks>
    /// <exception cref="Errors.ParsecTransportException">The service could not be reached.</exception>
    /// <exception cref="Errors.ParsecConfigurationException">
    /// The endpoint is not one this client can reach, or no provider matches the options.
    /// </exception>
    public ValueTask<IParsecClient> GetAsync(CancellationToken cancellationToken = default);
}
