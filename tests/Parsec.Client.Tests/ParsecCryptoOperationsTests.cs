using Google.Protobuf;
using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Operations;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the signing, hashing and random operations against a scripted service.
/// </summary>
public sealed class ParsecCryptoOperationsTests
{
    private const string ApplicationName = "app";

    private static readonly byte[] Digest = [0x01, 0x02, 0x03, 0x04];

    private static readonly byte[] Signature = [0xAA, 0xBB, 0xCC];

    [Fact]
    public async Task SignHashSendsTheKeyTheAlgorithmAndTheHash()
    {
        var body = new PsaSignHash.Result { Signature = ByteString.CopyFrom(Signature) }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaSignHash,
            ResponseStatus.Success,
            body,
            ProviderId.MbedCrypto);
        var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

        var answer = await CreateOperations(transport).SignHashAsync(
            "signing-key",
            algorithm,
            Digest,
            TestContext.Current.CancellationToken);

        Assert.Equal(Signature, answer);

        var request = Assert.Single(transport.SentRequests);
        Assert.Equal(Opcode.PsaSignHash, request.Header.Opcode);
        Assert.Equal(ProviderId.MbedCrypto, request.Header.Provider);

        var sent = PsaSignHash.Operation.Parser.ParseFrom(request.Body.Span);
        Assert.Equal("signing-key", sent.KeyName);
        Assert.Equal(Digest, sent.Hash.ToByteArray());
        Assert.Equal(AlgorithmCodec.ToWireSignature(algorithm), sent.Alg);
    }

    [Fact]
    public async Task VerifyHashAnswersTrueOnSuccess()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaVerifyHash,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        Assert.True(await CreateOperations(transport).VerifyHashAsync(
            "signing-key",
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256),
            Digest,
            Signature,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyHashAnswersFalseForASignatureThatDoesNotMatch()
    {
        // A signature that does not match is the answer to the question. A caller that has to
        // catch an exception to learn it will sooner or later catch one that means something
        // else and treat a broken service as a failed check.
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaVerifyHash,
            ResponseStatus.PsaErrorInvalidSignature,
            default,
            ProviderId.MbedCrypto);

        Assert.False(await CreateOperations(transport).VerifyHashAsync(
            "signing-key",
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256),
            Digest,
            Signature,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ResponseStatus.PsaErrorDoesNotExist)]
    [InlineData(ResponseStatus.PsaErrorNotPermitted)]
    [InlineData(ResponseStatus.PsaErrorInvalidArgument)]
    public async Task VerifyHashStillRaisesEveryOtherFailedStatus(ResponseStatus status)
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaVerifyHash,
            status,
            default,
            ProviderId.MbedCrypto);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAnyAsync<ParsecServiceException>(() => operations.VerifyHashAsync(
            "signing-key",
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256),
            Digest,
            Signature,
            TestContext.Current.CancellationToken));

        Assert.Equal(status, fault.Status);
    }

    [Fact]
    public async Task VerifyHashSendsBothTheHashAndTheSignature()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaVerifyHash,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        await CreateOperations(transport).VerifyHashAsync(
            "signing-key",
            SignatureAlgorithm.EcdsaAny,
            Digest,
            Signature,
            TestContext.Current.CancellationToken);

        var sent = PsaVerifyHash.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("signing-key", sent.KeyName);
        Assert.Equal(Digest, sent.Hash.ToByteArray());
        Assert.Equal(Signature, sent.Signature.ToByteArray());
    }

    [Fact]
    public async Task SignMessageCarriesTheMessageRatherThanAHash()
    {
        var message = "sign me"u8.ToArray();
        var body = new PsaSignMessage.Result { Signature = ByteString.CopyFrom(Signature) }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaSignMessage,
            ResponseStatus.Success,
            body,
            ProviderId.MbedCrypto);

        var answer = await CreateOperations(transport).SignMessageAsync(
            "signing-key",
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256),
            message,
            TestContext.Current.CancellationToken);

        Assert.Equal(Signature, answer);

        var request = Assert.Single(transport.SentRequests);
        Assert.Equal(Opcode.PsaSignMessage, request.Header.Opcode);
        Assert.Equal(
            message,
            PsaSignMessage.Operation.Parser.ParseFrom(request.Body.Span).Message.ToByteArray());
    }

    [Fact]
    public async Task VerifyMessageAnswersFalseForASignatureThatDoesNotMatch()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaVerifyMessage,
            ResponseStatus.PsaErrorInvalidSignature,
            default,
            ProviderId.MbedCrypto);

        Assert.False(await CreateOperations(transport).VerifyMessageAsync(
            "signing-key",
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256),
            "sign me"u8.ToArray(),
            Signature,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyMessageSendsTheMessageAndTheSignature()
    {
        var message = "sign me"u8.ToArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaVerifyMessage,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        Assert.True(await CreateOperations(transport).VerifyMessageAsync(
            "signing-key",
            SignatureAlgorithm.RsaPss(Hash.Sha384),
            message,
            Signature,
            TestContext.Current.CancellationToken));

        var sent = PsaVerifyMessage.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal(message, sent.Message.ToByteArray());
        Assert.Equal(Signature, sent.Signature.ToByteArray());
    }

    [Fact]
    public async Task HashComputeSendsTheAlgorithmAndReadsTheHashBack()
    {
        var digest = new byte[] { 0x11, 0x22 };
        var body = new PsaHashCompute.Result { Hash = ByteString.CopyFrom(digest) }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaHashCompute,
            ResponseStatus.Success,
            body,
            ProviderId.MbedCrypto);

        var answer = await CreateOperations(transport).HashComputeAsync(
            Hash.Sha384,
            "hash me"u8.ToArray(),
            TestContext.Current.CancellationToken);

        Assert.Equal(digest, answer);

        var sent = PsaHashCompute.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal(AlgorithmCodec.ToWireHash(Hash.Sha384), sent.Alg);
        Assert.Equal("hash me"u8.ToArray(), sent.Input.ToByteArray());
    }

    [Fact]
    public async Task HashCompareAnswersFalseWhenTheHashDoesNotMatch()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaHashCompare,
            ResponseStatus.PsaErrorInvalidSignature,
            default,
            ProviderId.MbedCrypto);

        Assert.False(await CreateOperations(transport).HashCompareAsync(
            Hash.Sha256,
            "hash me"u8.ToArray(),
            Digest,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HashCompareSendsTheInputAndTheHash()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaHashCompare,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        Assert.True(await CreateOperations(transport).HashCompareAsync(
            Hash.Sha256,
            "hash me"u8.ToArray(),
            Digest,
            TestContext.Current.CancellationToken));

        var sent = PsaHashCompare.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("hash me"u8.ToArray(), sent.Input.ToByteArray());
        Assert.Equal(Digest, sent.Hash.ToByteArray());
    }

    [Fact]
    public async Task GenerateRandomAsksForTheLengthAndChecksWhatCameBack()
    {
        var random = new byte[32];
        random[0] = 0x7F;
        var body = new PsaGenerateRandom.Result { RandomBytes = ByteString.CopyFrom(random) }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaGenerateRandom,
            ResponseStatus.Success,
            body,
            ProviderId.MbedCrypto);

        var answer = await CreateOperations(transport).GenerateRandomAsync(
            32,
            TestContext.Current.CancellationToken);

        Assert.Equal(random, answer);

        var sent = PsaGenerateRandom.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal(32ul, sent.Size);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public async Task GenerateRandomRefusesAnAnswerOfTheWrongLength(int answered)
    {
        // A caller sizes a key or a nonce from this. A short answer that goes unnoticed becomes a
        // secret with fewer bits in it than the caller believes.
        var body = new PsaGenerateRandom.Result
        {
            RandomBytes = ByteString.CopyFrom(new byte[answered]),
        }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaGenerateRandom,
            ResponseStatus.Success,
            body,
            ProviderId.MbedCrypto);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.GenerateRandomAsync(32, TestContext.Current.CancellationToken));

        Assert.Contains("random byte count", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateRandomAcceptsAskingForNothing()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaGenerateRandom,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        Assert.Empty(await CreateOperations(transport).GenerateRandomAsync(
            0,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GenerateRandomRefusesANegativeLength()
    {
        var operations = CreateOperations(new ScriptedTransport());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => operations.GenerateRandomAsync(-1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EveryOperationWithAKeyRefusesANullName()
    {
        var operations = CreateOperations(new ScriptedTransport());
        var algorithm = SignatureAlgorithm.EcdsaAny;
        var token = TestContext.Current.CancellationToken;

        await AssertRefuses("name", () => operations.SignHashAsync(null!, algorithm, Digest, token));
        await AssertRefuses("name", () => operations.VerifyHashAsync(null!, algorithm, Digest, Signature, token));
        await AssertRefuses("name", () => operations.SignMessageAsync(null!, algorithm, Digest, token));
        await AssertRefuses(
            "name",
            () => operations.VerifyMessageAsync(null!, algorithm, Digest, Signature, token));
    }

    [Fact]
    public async Task EveryOperationWithAKeyRefusesANullAlgorithm()
    {
        var operations = CreateOperations(new ScriptedTransport());
        var token = TestContext.Current.CancellationToken;

        await AssertRefuses("signature", () => operations.SignHashAsync("k", null!, Digest, token));
        await AssertRefuses("signature", () => operations.VerifyHashAsync("k", null!, Digest, Signature, token));
        await AssertRefuses("signature", () => operations.SignMessageAsync("k", null!, Digest, token));
        await AssertRefuses(
            "signature",
            () => operations.VerifyMessageAsync("k", null!, Digest, Signature, token));
    }

    [Fact]
    public void TheOperationsRefuseANullTransportOrAuthentication()
    {
        Assert.Throws<ArgumentNullException>(() => new ParsecCryptoOperations(
            null!,
            new DirectAuthentication(ApplicationName),
            ProviderId.MbedCrypto));
        Assert.Throws<ArgumentNullException>(() => new ParsecCryptoOperations(
            new ScriptedTransport(),
            null!,
            ProviderId.MbedCrypto));
    }

    [Fact]
    public async Task AHashTheSpecificationDoesNotDefineNeverReachesTheService()
    {
        var transport = new ScriptedTransport();
        var operations = CreateOperations(transport);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => operations.HashComputeAsync(
            (Hash)99,
            Digest,
            TestContext.Current.CancellationToken));

        Assert.Empty(transport.SentRequests);
    }

    private static async Task AssertRefuses(string parameter, Func<Task> call)
    {
        var fault = await Assert.ThrowsAsync<ArgumentNullException>(call);

        Assert.Equal(parameter, fault.ParamName);
    }

    private static ParsecCryptoOperations CreateOperations(ScriptedTransport transport) =>
        new(transport, new DirectAuthentication(ApplicationName), ProviderId.MbedCrypto);
}
