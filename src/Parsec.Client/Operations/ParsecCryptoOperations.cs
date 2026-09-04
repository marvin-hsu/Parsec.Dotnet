using Google.Protobuf;
using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Operations;

/// <summary>
/// The operations that use a key rather than manage one, together with the two that need no key.
/// </summary>
/// <remarks>
/// A verification answers with a boolean and not with an exception. A signature that does not
/// match is the answer to the question, not a failure of the request, and a caller that has to
/// catch an exception to learn it will sooner or later catch one that means something else.
/// </remarks>
/// <param name="transport">The transport that reaches the service.</param>
/// <param name="authentication">The authentication that the application chose.</param>
/// <param name="provider">The provider that holds the keys and runs the algorithms.</param>
internal sealed class ParsecCryptoOperations(
    IParsecTransport transport,
    IParsecAuthentication authentication,
    ProviderId provider)
{
    private readonly ParsecOperationClient _client =
        new(transport ?? throw new ArgumentNullException(nameof(transport)));

    private readonly IParsecAuthentication _authentication =
        authentication ?? throw new ArgumentNullException(nameof(authentication));

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
    public async Task<byte[]> SignHashAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaSignHash.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireSignature(algorithm),
            Hash = UnsafeByteOperations.UnsafeWrap(hash),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaSignHash,
            provider,
            _authentication,
            operation,
            PsaSignHash.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Signature.ToByteArray();
    }

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
    public async Task<bool> VerifyHashAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> hash,
        ReadOnlyMemory<byte> signature,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaVerifyHash.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireSignature(algorithm),
            Hash = UnsafeByteOperations.UnsafeWrap(hash),
            Signature = UnsafeByteOperations.UnsafeWrap(signature),
        };

        return await VerifyAsync(Opcode.PsaVerifyHash, operation, cancellationToken).ConfigureAwait(false);
    }

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
    public async Task<byte[]> SignMessageAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaSignMessage.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireSignature(algorithm),
            Message = UnsafeByteOperations.UnsafeWrap(message),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaSignMessage,
            provider,
            _authentication,
            operation,
            PsaSignMessage.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Signature.ToByteArray();
    }

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
    public async Task<bool> VerifyMessageAsync(
        string name,
        SignatureAlgorithm algorithm,
        ReadOnlyMemory<byte> message,
        ReadOnlyMemory<byte> signature,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaVerifyMessage.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireSignature(algorithm),
            Message = UnsafeByteOperations.UnsafeWrap(message),
            Signature = UnsafeByteOperations.UnsafeWrap(signature),
        };

        return await VerifyAsync(Opcode.PsaVerifyMessage, operation, cancellationToken)
            .ConfigureAwait(false);
    }

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
    public async Task<byte[]> HashComputeAsync(
        Hash algorithm,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default)
    {
        var operation = new PsaHashCompute.Operation
        {
            Alg = AlgorithmCodec.ToWireHash(algorithm),
            Input = UnsafeByteOperations.UnsafeWrap(input),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaHashCompute,
            provider,
            _authentication,
            operation,
            PsaHashCompute.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Hash.ToByteArray();
    }

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
    public async Task<bool> HashCompareAsync(
        Hash algorithm,
        ReadOnlyMemory<byte> input,
        ReadOnlyMemory<byte> hash,
        CancellationToken cancellationToken = default)
    {
        var operation = new PsaHashCompare.Operation
        {
            Alg = AlgorithmCodec.ToWireHash(algorithm),
            Input = UnsafeByteOperations.UnsafeWrap(input),
            Hash = UnsafeByteOperations.UnsafeWrap(hash),
        };

        return await VerifyAsync(Opcode.PsaHashCompare, operation, cancellationToken).ConfigureAwait(false);
    }

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
    public async Task<byte[]> GenerateRandomAsync(
        int length,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var result = await _client.ExecuteAsync(
            Opcode.PsaGenerateRandom,
            provider,
            _authentication,
            new PsaGenerateRandom.Operation { Size = (ulong)length },
            PsaGenerateRandom.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        var bytes = result.RandomBytes.ToByteArray();

        // A caller sizes a buffer or a key from this. A short answer that goes unnoticed becomes
        // a secret with fewer bits in it than the caller believes.
        return bytes.Length == length
            ? bytes
            : throw ParsecProtocolException.OutOfRangeField(
                Opcode.PsaGenerateRandom,
                "random byte count",
                (uint)bytes.Length,
                length);
    }

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
    public async Task<byte[]> AsymmetricEncryptAsync(
        string name,
        EncryptionAlgorithm algorithm,
        ReadOnlyMemory<byte> plaintext,
        ReadOnlyMemory<byte> salt = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaAsymmetricEncrypt.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireEncryptionAlgorithm(algorithm),
            Plaintext = UnsafeByteOperations.UnsafeWrap(plaintext),
            Salt = UnsafeByteOperations.UnsafeWrap(salt),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaAsymmetricEncrypt,
            provider,
            _authentication,
            operation,
            PsaAsymmetricEncrypt.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Ciphertext.ToByteArray();
    }

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
    public async Task<byte[]> AsymmetricDecryptAsync(
        string name,
        EncryptionAlgorithm algorithm,
        ReadOnlyMemory<byte> ciphertext,
        ReadOnlyMemory<byte> salt = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaAsymmetricDecrypt.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireEncryptionAlgorithm(algorithm),
            Ciphertext = UnsafeByteOperations.UnsafeWrap(ciphertext),
            Salt = UnsafeByteOperations.UnsafeWrap(salt),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaAsymmetricDecrypt,
            provider,
            _authentication,
            operation,
            PsaAsymmetricDecrypt.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Plaintext.ToByteArray();
    }

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
    public async Task<byte[]> AeadEncryptAsync(
        string name,
        AeadAlgorithm algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> additionalData,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaAeadEncrypt.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireAeadAlgorithm(algorithm),
            Nonce = UnsafeByteOperations.UnsafeWrap(nonce),
            AdditionalData = UnsafeByteOperations.UnsafeWrap(additionalData),
            Plaintext = UnsafeByteOperations.UnsafeWrap(plaintext),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaAeadEncrypt,
            provider,
            _authentication,
            operation,
            PsaAeadEncrypt.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Ciphertext.ToByteArray();
    }

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
    public async Task<byte[]> AeadDecryptAsync(
        string name,
        AeadAlgorithm algorithm,
        ReadOnlyMemory<byte> nonce,
        ReadOnlyMemory<byte> additionalData,
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaAeadDecrypt.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireAeadAlgorithm(algorithm),
            Nonce = UnsafeByteOperations.UnsafeWrap(nonce),
            AdditionalData = UnsafeByteOperations.UnsafeWrap(additionalData),
            Ciphertext = UnsafeByteOperations.UnsafeWrap(ciphertext),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaAeadDecrypt,
            provider,
            _authentication,
            operation,
            PsaAeadDecrypt.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Plaintext.ToByteArray();
    }

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
    public async Task<byte[]> RawKeyAgreementAsync(
        string name,
        KeyAgreementKind algorithm,
        ReadOnlyMemory<byte> peerKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaRawKeyAgreement.Operation
        {
            PrivateKeyName = name,
            Alg = AlgorithmCodec.ToWireKeyAgreementKind(algorithm),
            PeerKey = UnsafeByteOperations.UnsafeWrap(peerKey),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaRawKeyAgreement,
            provider,
            _authentication,
            operation,
            PsaRawKeyAgreement.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.SharedSecret.ToByteArray();
    }

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
    public async Task<byte[]> CipherEncryptAsync(
        string name,
        Cipher algorithm,
        ReadOnlyMemory<byte> plaintext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaCipherEncrypt.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireCipherMode(algorithm),
            Plaintext = UnsafeByteOperations.UnsafeWrap(plaintext),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaCipherEncrypt,
            provider,
            _authentication,
            operation,
            PsaCipherEncrypt.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Ciphertext.ToByteArray();
    }

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
    public async Task<byte[]> CipherDecryptAsync(
        string name,
        Cipher algorithm,
        ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaCipherDecrypt.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireCipherMode(algorithm),
            Ciphertext = UnsafeByteOperations.UnsafeWrap(ciphertext),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaCipherDecrypt,
            provider,
            _authentication,
            operation,
            PsaCipherDecrypt.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Plaintext.ToByteArray();
    }

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
    public async Task<byte[]> MacComputeAsync(
        string name,
        MacAlgorithm algorithm,
        ReadOnlyMemory<byte> input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaMacCompute.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireMacAlgorithm(algorithm),
            Input = UnsafeByteOperations.UnsafeWrap(input),
        };

        var result = await _client.ExecuteAsync(
            Opcode.PsaMacCompute,
            provider,
            _authentication,
            operation,
            PsaMacCompute.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Mac.ToByteArray();
    }

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
    public async Task<bool> MacVerifyAsync(
        string name,
        MacAlgorithm algorithm,
        ReadOnlyMemory<byte> input,
        ReadOnlyMemory<byte> mac,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaMacVerify.Operation
        {
            KeyName = name,
            Alg = AlgorithmCodec.ToWireMacAlgorithm(algorithm),
            Input = UnsafeByteOperations.UnsafeWrap(input),
            Mac = UnsafeByteOperations.UnsafeWrap(mac),
        };

        return await VerifyAsync(Opcode.PsaMacVerify, operation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs an operation whose failure to match is an answer rather than a fault.
    /// </summary>
    /// <param name="opcode">The operation to run.</param>
    /// <param name="operation">The encoded request.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns><see langword="true"/> when the service reported success.</returns>
    private async Task<bool> VerifyAsync(
        Opcode opcode,
        IMessage operation,
        CancellationToken cancellationToken)
    {
        var response = await _client.ExchangeAsync(
            opcode,
            provider,
            _authentication,
            operation,
            cancellationToken).ConfigureAwait(false);

        if (response.Header.Status == ResponseStatus.PsaErrorInvalidSignature)
        {
            return false;
        }

        ParsecOperationClient.ThrowIfFailed(opcode, response);

        return true;
    }
}
