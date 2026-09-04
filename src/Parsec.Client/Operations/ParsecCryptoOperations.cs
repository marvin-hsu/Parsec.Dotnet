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
    ProviderId provider) : IParsecCryptoOperations
{
    private readonly ParsecOperationClient _client =
        new(transport ?? throw new ArgumentNullException(nameof(transport)));

    private readonly IParsecAuthentication _authentication =
        authentication ?? throw new ArgumentNullException(nameof(authentication));

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
