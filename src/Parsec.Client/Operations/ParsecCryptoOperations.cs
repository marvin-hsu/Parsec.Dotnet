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
