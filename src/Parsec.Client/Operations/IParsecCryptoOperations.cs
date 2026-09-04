using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;

namespace Parsec.Client.Operations;

/// <summary>
/// The operations that use a key rather than manage one, together with the two that need no key.
/// </summary>
/// <remarks>
/// A verification answers with a boolean and not with an exception. A signature that does not
/// match is the answer to the question, not a failure of the request, and a caller that has to
/// catch an exception to learn it will sooner or later catch one that means something else.
/// </remarks>
public interface IParsecCryptoOperations
{
    /// <summary>
    /// Signs a hash that the caller computed.
    /// </summary>
    /// <param name="name">The name of the signing key.</param>
    /// <param name="algorithm">The signature algorithm. It must be the one the key binds to.</param>
    /// <param name="hash">The hash to sign. Its length must suit the algorithm.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The signature.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> SignHashAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a signature over a hash that the caller computed.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The signature algorithm. It must be the one the key binds to.</param>
    /// <param name="hash">The hash that was signed.</param>
    /// <param name="signature">The signature to check.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// <see langword="true"/> when the signature matches, and <see langword="false"/> when it
    /// does not.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused for a reason other than a signature that does not match.
    /// </exception>
    public Task<bool> VerifyHashAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> hash,
        ReadOnlyMemory<byte> signature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a message, hashing it as part of the operation.
    /// </summary>
    /// <param name="name">The name of the signing key.</param>
    /// <param name="algorithm">The signature algorithm. It must be the one the key binds to.</param>
    /// <param name="message">The message to sign.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The signature.</returns>
    /// <remarks>
    /// The Mbed Crypto provider does not offer this. Hash the message and call
    /// <see cref="SignHashAsync"/> instead where that provider is the only one running.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> SignMessageAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a signature over a message, hashing it as part of the operation.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The signature algorithm. It must be the one the key binds to.</param>
    /// <param name="message">The message that was signed.</param>
    /// <param name="signature">The signature to check.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// <see langword="true"/> when the signature matches, and <see langword="false"/> when it
    /// does not.
    /// </returns>
    /// <remarks>The Mbed Crypto provider does not offer this.</remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused for a reason other than a signature that does not match.
    /// </exception>
    public Task<bool> VerifyMessageAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> message,
        ReadOnlyMemory<byte> signature,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a hash over some bytes.
    /// </summary>
    /// <param name="algorithm">The hash to compute.</param>
    /// <param name="input">The bytes to hash.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The hash.</returns>
    /// <remarks>
    /// This needs no key. It is here because an application that signs a hash needs somewhere to
    /// get the hash from, and using the same provider for both keeps the two in step.
    /// </remarks>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> HashComputeAsync(
        Hash algorithm,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks some bytes against a hash of them.
    /// </summary>
    /// <param name="algorithm">The hash that was computed.</param>
    /// <param name="input">The bytes to hash.</param>
    /// <param name="hash">The hash to compare against.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// <see langword="true"/> when the hash of the bytes matches, and <see langword="false"/>
    /// when it does not.
    /// </returns>
    /// <remarks>
    /// The service compares the two in a way that takes the same time whatever the answer, which
    /// is why this is worth a round trip rather than hashing and comparing at home.
    /// </remarks>
    /// <exception cref="ParsecServiceException">
    /// The provider refused for a reason other than a hash that does not match.
    /// </exception>
    public Task<bool> HashCompareAsync(
        Hash algorithm,
        ReadOnlyMemory<byte> input,
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the provider for random bytes.
    /// </summary>
    /// <param name="length">How many bytes to ask for.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The bytes, of exactly the length asked for.</returns>
    /// <remarks>
    /// These come from the generator of the provider, which on a hardware provider is the one in
    /// the hardware. That is the reason to ask the service rather than the platform.
    /// </remarks>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    /// <exception cref="ParsecProtocolException">
    /// The provider answered with a different number of bytes than the one asked for.
    /// </exception>
    public Task<byte[]> GenerateRandomAsync(int length, CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts with the public half of an asymmetric key.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The algorithm. It must be the one the key binds to.</param>
    /// <param name="plaintext">The bytes to encrypt. An asymmetric algorithm takes only a few.</param>
    /// <param name="salt">
    /// The label for OAEP, which both sides must agree on. Leave it empty for PKCS#1 v1.5, which
    /// has no label.
    /// </param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The ciphertext.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> AsymmetricEncryptAsync(
        string name,
        EncryptionAlgorithm algorithm,
        ReadOnlyMemory<byte> plaintext,
        ReadOnlyMemory<byte> salt = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts with the private half of an asymmetric key.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The algorithm. It must be the one the key binds to.</param>
    /// <param name="ciphertext">The bytes to decrypt.</param>
    /// <param name="salt">The label that the encryption used.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The plaintext.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> AsymmetricDecryptAsync(
        string name,
        EncryptionAlgorithm algorithm,
        ReadOnlyMemory<byte> ciphertext,
        ReadOnlyMemory<byte> salt = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts and authenticates in one pass.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The algorithm. It must be the one the key binds to.</param>
    /// <param name="nonce">
    /// The nonce. It must never repeat for one key, and for most algorithms repeating it loses
    /// the key rather than only the message.
    /// </param>
    /// <param name="additionalData">
    /// Bytes to authenticate without encrypting. The decryption must be given the same bytes.
    /// </param>
    /// <param name="plaintext">The bytes to encrypt.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The ciphertext with the authentication tag on the end.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> AeadEncryptAsync(
        string name,
        AeadAlgorithm algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> additionalData,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts and checks the authentication tag in one pass.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The algorithm. It must be the one the key binds to.</param>
    /// <param name="nonce">The nonce that the encryption used.</param>
    /// <param name="additionalData">The bytes that the encryption authenticated.</param>
    /// <param name="ciphertext">The ciphertext with its tag.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The plaintext.</returns>
    /// <remarks>
    /// A tag that does not match raises rather than answering false, unlike the two verify
    /// operations. There is no plaintext to hand back in that case, and returning nothing
    /// alongside a boolean invites a caller to read the nothing.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused. <see cref="ResponseStatus.PsaErrorInvalidSignature"/> means the tag
    /// did not match, so the ciphertext or the additional data was changed.
    /// </exception>
    public Task<byte[]> AeadDecryptAsync(
        string name,
        AeadAlgorithm algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> additionalData,
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrees a shared secret with another party.
    /// </summary>
    /// <param name="name">The name of the private key of this side.</param>
    /// <param name="algorithm">The algorithm that produces the shared secret.</param>
    /// <param name="peerKey">The public key of the other side.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The shared secret.</returns>
    /// <remarks>
    /// The secret comes back raw. It is not a key: feed it through a derivation function before
    /// using it as one, because the bytes of a Diffie-Hellman result are not uniformly random.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The specification does not define <paramref name="algorithm"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> RawKeyAgreementAsync(
        string name,
        KeyAgreementKind algorithm,
        ReadOnlyMemory<byte> peerKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypts with a symmetric key.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The cipher mode. It must be the one the key binds to.</param>
    /// <param name="plaintext">The bytes to encrypt.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The initialisation vector followed by the ciphertext.</returns>
    /// <remarks>
    /// This encrypts and does not authenticate, so a change to the ciphertext goes unnoticed.
    /// Reach for <see cref="AeadEncryptAsync"/> unless something outside this call authenticates
    /// the result. The Mbed Crypto provider offers no cipher operation.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> CipherEncryptAsync(
        string name,
        Cipher algorithm,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts with a symmetric key.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The cipher mode. It must be the one the key binds to.</param>
    /// <param name="ciphertext">The initialisation vector followed by the ciphertext.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The plaintext.</returns>
    /// <remarks>The Mbed Crypto provider offers no cipher operation.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> CipherDecryptAsync(
        string name,
        Cipher algorithm,
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes a message authentication code.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The algorithm. It must be the one the key binds to.</param>
    /// <param name="input">The bytes to authenticate.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The code.</returns>
    /// <remarks>The Mbed Crypto provider offers no code operation.</remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    public Task<byte[]> MacComputeAsync(
        string name,
        MacAlgorithm algorithm,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a message authentication code.
    /// </summary>
    /// <param name="name">The name of the key.</param>
    /// <param name="algorithm">The algorithm. It must be the one the key binds to.</param>
    /// <param name="input">The bytes that were authenticated.</param>
    /// <param name="mac">The code to check.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// <see langword="true"/> when the code matches, and <see langword="false"/> when it does not.
    /// </returns>
    /// <remarks>The Mbed Crypto provider offers no code operation.</remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="algorithm"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused for a reason other than a code that does not match.
    /// </exception>
    public Task<bool> MacVerifyAsync(
        string name,
        MacAlgorithm algorithm,
        ReadOnlyMemory<byte> input,
        ReadOnlyMemory<byte> mac,
        CancellationToken cancellationToken = default);
}
