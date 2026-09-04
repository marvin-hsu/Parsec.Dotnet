using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;

namespace Parsec.Client.Operations;

/// <summary>
/// The operations that create, remove and read back the keys of one provider.
/// </summary>
/// <remarks>
/// Every one of these goes to a provider rather than to the core, and every one needs an
/// application identity, because a key belongs to the application that created it. A key name is
/// unique inside the pair of the provider and the application.
/// </remarks>
public interface IParsecKeyOperations
{
    /// <summary>
    /// Creates a key inside the provider.
    /// </summary>
    /// <param name="name">The name to give the key. It must not already exist.</param>
    /// <param name="attributes">What the key holds and what may be done with it.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>A task that completes when the provider has created the key.</returns>
    /// <remarks>
    /// The key material never leaves the provider. What comes back is nothing at all: the name is
    /// how the application reaches the key afterwards.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="attributes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused. <see cref="ResponseStatus.PsaErrorAlreadyExists"/> means the name is
    /// taken, and <see cref="ResponseStatus.PsaErrorNotSupported"/> means the provider cannot
    /// make a key of that shape.
    /// </exception>
    public Task GenerateKeyAsync(string name, KeyAttributes attributes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings key material in from outside and stores it under a name.
    /// </summary>
    /// <param name="name">The name to give the key. It must not already exist.</param>
    /// <param name="attributes">What the key holds and what may be done with it.</param>
    /// <param name="data">
    /// The key material, in the format the specification states for the key type. A public RSA
    /// key is the DER encoding of RSAPublicKey, and a symmetric key is its raw bytes.
    /// </param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>A task that completes when the provider has stored the key.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="attributes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task ImportKeyAsync(
        string name,
        KeyAttributes attributes,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a key from the provider.
    /// </summary>
    /// <param name="name">The name of the key to remove.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>A task that completes when the provider has removed the key.</returns>
    /// <remarks>
    /// Removing a key that is not there is a fault and not a quiet success, because the two cases
    /// mean different things to an application that is cleaning up after itself.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused. <see cref="ResponseStatus.PsaErrorDoesNotExist"/> means no key of
    /// that name belongs to the application.
    /// </exception>
    public Task DestroyKeyAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the public half of a key pair.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// The public key, DER encoded as the specification states for the key type.
    /// </returns>
    /// <remarks>
    /// This needs no export permission on the key. The public half of a key pair is public, and
    /// the permission covers the private half.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> ExportPublicKeyAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a key out of the provider.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The key material, encoded as the specification states for the key type.</returns>
    /// <remarks>
    /// This hands out secret material, so it works only on a key whose policy carries
    /// <see cref="KeyUsages.Export"/>, and that permission has to be granted when the key is
    /// created. A provider may refuse it outright.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused. <see cref="ResponseStatus.PsaErrorNotPermitted"/> means the policy
    /// of the key does not allow it.
    /// </exception>
    public Task<byte[]> ExportKeyAsync(string name, CancellationToken cancellationToken = default);
}
