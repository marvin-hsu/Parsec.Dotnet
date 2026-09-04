using Google.Protobuf;
using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Operations;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the encryption, code and agreement operations against a scripted service.
/// </summary>
public sealed class ParsecCipherOperationsTests
{
    private const string ApplicationName = "app";

    private static readonly byte[] _plaintext = "hello"u8.ToArray();

    private static readonly byte[] _ciphertext = [0x11, 0x22, 0x33];

    private static readonly byte[] _nonce = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];

    private static readonly byte[] _aad = "header"u8.ToArray();

    [Fact]
    public async Task AsymmetricEncryptSendsTheAlgorithmThePlaintextAndTheSalt()
    {
        var body = new PsaAsymmetricEncrypt.Result
        {
            Ciphertext = ByteString.CopyFrom(_ciphertext),
        }.ToByteArray();
        var transport = Script(Opcode.PsaAsymmetricEncrypt, ResponseStatus.Success, body);
        var algorithm = EncryptionAlgorithm.RsaOaep(Hash.Sha256);
        var salt = "label"u8.ToArray();

        var answer = await CreateOperations(transport).AsymmetricEncryptAsync(
            "encryption-key",
            algorithm,
            _plaintext,
            salt,
            TestContext.Current.CancellationToken);

        Assert.Equal(_ciphertext, answer);

        var request = Assert.Single(transport.SentRequests);
        Assert.Equal(Opcode.PsaAsymmetricEncrypt, request.Header.Opcode);

        var sent = PsaAsymmetricEncrypt.Operation.Parser.ParseFrom(request.Body.Span);
        Assert.Equal("encryption-key", sent.KeyName);
        Assert.Equal(_plaintext, sent.Plaintext.ToByteArray());
        Assert.Equal(salt, sent.Salt.ToByteArray());
        Assert.Equal(AlgorithmCodec.ToWireEncryptionAlgorithm(algorithm), sent.Alg);
    }

    [Fact]
    public async Task AsymmetricDecryptReadsThePlaintextBack()
    {
        var body = new PsaAsymmetricDecrypt.Result
        {
            Plaintext = ByteString.CopyFrom(_plaintext),
        }.ToByteArray();
        var transport = Script(Opcode.PsaAsymmetricDecrypt, ResponseStatus.Success, body);

        var answer = await CreateOperations(transport).AsymmetricDecryptAsync(
            "encryption-key",
            EncryptionAlgorithm.RsaPkcs1v15Crypt,
            _ciphertext,
            default,
            TestContext.Current.CancellationToken);

        Assert.Equal(_plaintext, answer);

        var sent = PsaAsymmetricDecrypt.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal(_ciphertext, sent.Ciphertext.ToByteArray());
        Assert.Empty(sent.Salt.ToByteArray());
    }

    [Fact]
    public async Task AeadEncryptSendsTheNonceTheAdditionalDataAndThePlaintext()
    {
        var body = new PsaAeadEncrypt.Result { Ciphertext = ByteString.CopyFrom(_ciphertext) }.ToByteArray();
        var transport = Script(Opcode.PsaAeadEncrypt, ResponseStatus.Success, body);

        var answer = await CreateOperations(transport).AeadEncryptAsync(
            "aes-key",
            AeadAlgorithm.Gcm,
            _nonce,
            _aad,
            _plaintext,
            TestContext.Current.CancellationToken);

        Assert.Equal(_ciphertext, answer);

        var sent = PsaAeadEncrypt.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("aes-key", sent.KeyName);
        Assert.Equal(_nonce, sent.Nonce.ToByteArray());
        Assert.Equal(_aad, sent.AdditionalData.ToByteArray());
        Assert.Equal(_plaintext, sent.Plaintext.ToByteArray());
        Assert.Equal(AlgorithmCodec.ToWireAeadAlgorithm(AeadAlgorithm.Gcm), sent.Alg);
    }

    [Fact]
    public async Task AeadDecryptSendsTheSameFieldsAndReadsThePlaintextBack()
    {
        var body = new PsaAeadDecrypt.Result { Plaintext = ByteString.CopyFrom(_plaintext) }.ToByteArray();
        var transport = Script(Opcode.PsaAeadDecrypt, ResponseStatus.Success, body);

        var answer = await CreateOperations(transport).AeadDecryptAsync(
            "aes-key",
            AeadAlgorithm.Ccm.WithTagLength(12),
            _nonce,
            _aad,
            _ciphertext,
            TestContext.Current.CancellationToken);

        Assert.Equal(_plaintext, answer);

        var sent = PsaAeadDecrypt.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal(_nonce, sent.Nonce.ToByteArray());
        Assert.Equal(_aad, sent.AdditionalData.ToByteArray());
        Assert.Equal(_ciphertext, sent.Ciphertext.ToByteArray());
        Assert.Equal(
            AlgorithmCodec.ToWireAeadAlgorithm(AeadAlgorithm.Ccm.WithTagLength(12)),
            sent.Alg);
    }

    [Fact]
    public async Task AeadDecryptRaisesWhenTheTagDoesNotMatch()
    {
        // Unlike the verify operations this one raises. There is no plaintext to hand back, and
        // returning nothing alongside a boolean invites a caller to read the nothing.
        var transport = Script(Opcode.PsaAeadDecrypt, ResponseStatus.PsaErrorInvalidSignature);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecPsaException>(() => operations.AeadDecryptAsync(
            "aes-key",
            AeadAlgorithm.Gcm,
            _nonce,
            _aad,
            _ciphertext,
            TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorInvalidSignature, fault.Status);
    }

    [Fact]
    public async Task RawKeyAgreementNamesThePrivateKeyAndCarriesThePeerKey()
    {
        var secret = new byte[32];
        secret[0] = 0x5A;
        var body = new PsaRawKeyAgreement.Result
        {
            SharedSecret = ByteString.CopyFrom(secret),
        }.ToByteArray();
        var transport = Script(Opcode.PsaRawKeyAgreement, ResponseStatus.Success, body);
        var peer = new byte[] { 0x04, 0xAA, 0xBB };

        var answer = await CreateOperations(transport).RawKeyAgreementAsync(
            "agreement-key",
            KeyAgreementKind.Ecdh,
            peer,
            TestContext.Current.CancellationToken);

        Assert.Equal(secret, answer);

        var sent = PsaRawKeyAgreement.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("agreement-key", sent.PrivateKeyName);
        Assert.Equal(peer, sent.PeerKey.ToByteArray());
        Assert.Equal(AlgorithmCodec.ToWireKeyAgreementKind(KeyAgreementKind.Ecdh), sent.Alg);
    }

    [Fact]
    public async Task RawKeyAgreementRefusesAnAlgorithmTheSpecificationDoesNotDefine()
    {
        var transport = new ScriptedTransport();
        var operations = CreateOperations(transport);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => operations.RawKeyAgreementAsync(
            "agreement-key",
            KeyAgreementKind.None,
            default,
            TestContext.Current.CancellationToken));

        Assert.Empty(transport.SentRequests);
    }

    [Fact]
    public async Task CipherEncryptAndDecryptCarryTheMode()
    {
        var encryptBody = new PsaCipherEncrypt.Result
        {
            Ciphertext = ByteString.CopyFrom(_ciphertext),
        }.ToByteArray();
        var encrypt = Script(Opcode.PsaCipherEncrypt, ResponseStatus.Success, encryptBody);

        var encrypted = await CreateOperations(encrypt).CipherEncryptAsync(
            "aes-key",
            Cipher.CbcPkcs7,
            _plaintext,
            TestContext.Current.CancellationToken);

        Assert.Equal(_ciphertext, encrypted);

        var sentEncrypt = PsaCipherEncrypt.Operation.Parser.ParseFrom(
            Assert.Single(encrypt.SentRequests).Body.Span);

        Assert.Equal(AlgorithmCodec.ToWireCipherMode(Cipher.CbcPkcs7), sentEncrypt.Alg);

        var decryptBody = new PsaCipherDecrypt.Result
        {
            Plaintext = ByteString.CopyFrom(_plaintext),
        }.ToByteArray();
        var decrypt = Script(Opcode.PsaCipherDecrypt, ResponseStatus.Success, decryptBody);

        var decrypted = await CreateOperations(decrypt).CipherDecryptAsync(
            "aes-key",
            Cipher.Ctr,
            _ciphertext,
            TestContext.Current.CancellationToken);

        Assert.Equal(_plaintext, decrypted);

        var sentDecrypt = PsaCipherDecrypt.Operation.Parser.ParseFrom(
            Assert.Single(decrypt.SentRequests).Body.Span);

        Assert.Equal(AlgorithmCodec.ToWireCipherMode(Cipher.Ctr), sentDecrypt.Alg);
        Assert.Equal(_ciphertext, sentDecrypt.Ciphertext.ToByteArray());
    }

    [Fact]
    public async Task MacComputeSendsTheAlgorithmAndReadsTheCodeBack()
    {
        var code = new byte[] { 0xFE, 0xED };
        var body = new PsaMacCompute.Result { Mac = ByteString.CopyFrom(code) }.ToByteArray();
        var transport = Script(Opcode.PsaMacCompute, ResponseStatus.Success, body);
        var algorithm = MacAlgorithm.Hmac(Hash.Sha256);

        var answer = await CreateOperations(transport).MacComputeAsync(
            "hmac-key",
            algorithm,
            _plaintext,
            TestContext.Current.CancellationToken);

        Assert.Equal(code, answer);

        var sent = PsaMacCompute.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("hmac-key", sent.KeyName);
        Assert.Equal(_plaintext, sent.Input.ToByteArray());
        Assert.Equal(AlgorithmCodec.ToWireMacAlgorithm(algorithm), sent.Alg);
    }

    [Fact]
    public async Task MacVerifyAnswersTrueOnSuccessAndFalseOnAMismatch()
    {
        var code = new byte[] { 0xFE, 0xED };
        var match = Script(Opcode.PsaMacVerify, ResponseStatus.Success);

        Assert.True(await CreateOperations(match).MacVerifyAsync(
            "hmac-key",
            MacAlgorithm.Cmac,
            _plaintext,
            code,
            TestContext.Current.CancellationToken));

        var sent = PsaMacVerify.Operation.Parser.ParseFrom(Assert.Single(match.SentRequests).Body.Span);
        Assert.Equal(code, sent.Mac.ToByteArray());
        Assert.Equal(_plaintext, sent.Input.ToByteArray());

        var mismatch = Script(Opcode.PsaMacVerify, ResponseStatus.PsaErrorInvalidSignature);

        Assert.False(await CreateOperations(mismatch).MacVerifyAsync(
            "hmac-key",
            MacAlgorithm.Cmac,
            _plaintext,
            code,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MacVerifyStillRaisesEveryOtherFailedStatus()
    {
        var transport = Script(Opcode.PsaMacVerify, ResponseStatus.PsaErrorNotSupported);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAnyAsync<ParsecServiceException>(() => operations.MacVerifyAsync(
            "hmac-key",
            MacAlgorithm.Cmac,
            _plaintext,
            _ciphertext,
            TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorNotSupported, fault.Status);
    }

    [Fact]
    public async Task EveryOperationRefusesANullName()
    {
        var operations = CreateOperations(new ScriptedTransport());
        var token = TestContext.Current.CancellationToken;

        await AssertRefuses("name", () => operations.AsymmetricEncryptAsync(
            null!, EncryptionAlgorithm.RsaPkcs1v15Crypt, _plaintext, default, token));
        await AssertRefuses("name", () => operations.AsymmetricDecryptAsync(
            null!, EncryptionAlgorithm.RsaPkcs1v15Crypt, _ciphertext, default, token));
        await AssertRefuses("name", () => operations.AeadEncryptAsync(
            null!, AeadAlgorithm.Gcm, _nonce, _aad, _plaintext, token));
        await AssertRefuses("name", () => operations.AeadDecryptAsync(
            null!, AeadAlgorithm.Gcm, _nonce, _aad, _ciphertext, token));
        await AssertRefuses("name", () => operations.RawKeyAgreementAsync(
            null!, KeyAgreementKind.Ecdh, default, token));
        await AssertRefuses("name", () => operations.CipherEncryptAsync(
            null!, Cipher.Ctr, _plaintext, token));
        await AssertRefuses("name", () => operations.CipherDecryptAsync(
            null!, Cipher.Ctr, _ciphertext, token));
        await AssertRefuses("name", () => operations.MacComputeAsync(
            null!, MacAlgorithm.Cmac, _plaintext, token));
        await AssertRefuses("name", () => operations.MacVerifyAsync(
            null!, MacAlgorithm.Cmac, _plaintext, _ciphertext, token));
    }

    [Fact]
    public async Task EveryOperationRefusesANullAlgorithm()
    {
        var operations = CreateOperations(new ScriptedTransport());
        var token = TestContext.Current.CancellationToken;

        await AssertRefuses("encryption", () => operations.AsymmetricEncryptAsync(
            "k", null!, _plaintext, default, token));
        await AssertRefuses("encryption", () => operations.AsymmetricDecryptAsync(
            "k", null!, _ciphertext, default, token));
        await AssertRefuses("aead", () => operations.AeadEncryptAsync(
            "k", null!, _nonce, _aad, _plaintext, token));
        await AssertRefuses("aead", () => operations.AeadDecryptAsync(
            "k", null!, _nonce, _aad, _ciphertext, token));
        await AssertRefuses("mac", () => operations.MacComputeAsync("k", null!, _plaintext, token));
        await AssertRefuses("mac", () => operations.MacVerifyAsync(
            "k", null!, _plaintext, _ciphertext, token));
    }

    private static async Task AssertRefuses(string parameter, Func<Task> call)
    {
        var fault = await Assert.ThrowsAsync<ArgumentNullException>(call);

        Assert.Equal(parameter, fault.ParamName);
    }

    private static ScriptedTransport Script(
        Opcode opcode,
        ResponseStatus status,
        ReadOnlyMemory<byte> body = default) =>
        new ScriptedTransport().EnqueueResponse(opcode, status, body, ProviderId.MbedCrypto);

    private static ParsecCryptoOperations CreateOperations(ScriptedTransport transport) =>
        new(transport, new DirectAuthentication(ApplicationName), ProviderId.MbedCrypto);
}
