using Google.Protobuf;
using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Operations;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the key management operations against a scripted service.
/// </summary>
/// <remarks>
/// Each test reads the request the client sent back off the wire and decodes it, rather than
/// comparing whole golden bytes. The framing already has golden coverage in
/// <see cref="ParsecFramingTests"/>, so what matters here is that the right operation carries
/// the right fields to the right provider.
/// </remarks>
public sealed class ParsecKeyOperationsTests
{
    private const string ApplicationName = "app";

    [Fact]
    public async Task GenerateKeySendsTheNameAndTheAttributes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaGenerateKey,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);
        var attributes = KeyAttributes.RsaSigningKey();

        await CreateOperations(transport).GenerateKeyAsync(
            "signing-key",
            attributes,
            TestContext.Current.CancellationToken);

        var request = Assert.Single(transport.SentRequests);
        Assert.Equal(Opcode.PsaGenerateKey, request.Header.Opcode);
        Assert.Equal(ProviderId.MbedCrypto, request.Header.Provider);

        var body = PsaGenerateKey.Operation.Parser.ParseFrom(request.Body.Span);
        Assert.Equal("signing-key", body.KeyName);
        Assert.Equal(attributes, KeyAttributesCodec.FromWire(Opcode.PsaGenerateKey, body.Attributes));
    }

    [Fact]
    public async Task ImportKeyCarriesTheMaterialUnchanged()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaImportKey,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);
        var material = new byte[] { 0x00, 0xFF, 0x10, 0x7A };

        await CreateOperations(transport).ImportKeyAsync(
            "imported",
            KeyAttributes.AesKey(128),
            material,
            TestContext.Current.CancellationToken);

        var body = PsaImportKey.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("imported", body.KeyName);
        Assert.Equal(material, body.Data.ToByteArray());
    }

    [Fact]
    public async Task ImportKeyAcceptsEmptyMaterial()
    {
        // Proto3 leaves an empty bytes field out, so the request is shorter and the service sees
        // no data field at all. That is the encoding, not a fault of the client.
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaImportKey,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        await CreateOperations(transport).ImportKeyAsync(
            "imported",
            KeyAttributes.AesKey(128),
            default,
            TestContext.Current.CancellationToken);

        var body = PsaImportKey.Operation.Parser.ParseFrom(
            Assert.Single(transport.SentRequests).Body.Span);

        Assert.Empty(body.Data.ToByteArray());
    }

    [Fact]
    public async Task DestroyKeySendsOnlyTheName()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaDestroyKey,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        await CreateOperations(transport).DestroyKeyAsync("gone", TestContext.Current.CancellationToken);

        var request = Assert.Single(transport.SentRequests);
        Assert.Equal(Opcode.PsaDestroyKey, request.Header.Opcode);
        Assert.Equal("gone", PsaDestroyKey.Operation.Parser.ParseFrom(request.Body.Span).KeyName);
    }

    [Fact]
    public async Task ExportPublicKeyReadsTheBytesBack()
    {
        var exported = new byte[] { 0x30, 0x82, 0x01, 0x0A };
        var body = new PsaExportPublicKey.Result { Data = ByteString.CopyFrom(exported) }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaExportPublicKey,
            ResponseStatus.Success,
            body,
            ProviderId.MbedCrypto);

        var answer = await CreateOperations(transport).ExportPublicKeyAsync(
            "signing-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(exported, answer);
        var sent = PsaExportPublicKey.Operation.Parser.ParseFrom(Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("signing-key", sent.KeyName);
    }

    [Fact]
    public async Task ExportKeyReadsTheBytesBack()
    {
        var exported = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var body = new PsaExportKey.Result { Data = ByteString.CopyFrom(exported) }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaExportKey,
            ResponseStatus.Success,
            body,
            ProviderId.MbedCrypto);

        var answer = await CreateOperations(transport).ExportKeyAsync(
            "signing-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(exported, answer);
        var sent = PsaExportKey.Operation.Parser.ParseFrom(Assert.Single(transport.SentRequests).Body.Span);

        Assert.Equal("signing-key", sent.KeyName);
    }

    [Fact]
    public async Task AnEmptyExportReadsBackAsAnEmptyArray()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaExportPublicKey,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        var answer = await CreateOperations(transport).ExportPublicKeyAsync(
            "signing-key",
            TestContext.Current.CancellationToken);

        Assert.Empty(answer);
    }

    [Fact]
    public async Task ANameThatIsTakenRaisesTheStatusOfTheService()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaGenerateKey,
            ResponseStatus.PsaErrorAlreadyExists,
            default,
            ProviderId.MbedCrypto);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecPsaException>(() => operations.GenerateKeyAsync(
            "taken",
            KeyAttributes.RsaSigningKey(),
            TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorAlreadyExists, fault.Status);
    }

    [Theory]
    [InlineData(ResponseStatus.PsaErrorDoesNotExist)]
    [InlineData(ResponseStatus.PsaErrorNotPermitted)]
    [InlineData(ResponseStatus.PsaErrorNotSupported)]
    public async Task EveryFailedStatusReachesTheCaller(ResponseStatus status)
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.PsaDestroyKey,
            status,
            default,
            ProviderId.MbedCrypto);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAnyAsync<ParsecServiceException>(
            () => operations.DestroyKeyAsync("gone", TestContext.Current.CancellationToken));

        Assert.Equal(status, fault.Status);
    }

    [Fact]
    public async Task EveryOperationRefusesANullName()
    {
        var operations = CreateOperations(new ScriptedTransport());
        var attributes = KeyAttributes.RsaSigningKey();
        var token = TestContext.Current.CancellationToken;

        // The parameter name matters. Without the guard the encoder raises the same type a line
        // later and blames a field of a generated message, which tells the caller nothing about
        // the argument they passed.
        await AssertRefusesNullName(() => operations.GenerateKeyAsync(null!, attributes, token));
        await AssertRefusesNullName(() => operations.ImportKeyAsync(null!, attributes, default, token));
        await AssertRefusesNullName(() => operations.DestroyKeyAsync(null!, token));
        await AssertRefusesNullName(() => operations.ExportPublicKeyAsync(null!, token));
        await AssertRefusesNullName(() => operations.ExportKeyAsync(null!, token));
    }

    [Fact]
    public async Task CreatingAKeyRefusesNullAttributes()
    {
        var operations = CreateOperations(new ScriptedTransport());
        var token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentNullException>(() => operations.GenerateKeyAsync("k", null!, token));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => operations.ImportKeyAsync("k", null!, default, token));
    }

    [Fact]
    public void TheOperationsRefuseANullTransportOrAuthentication()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ParsecKeyOperations(null!, new DirectAuthentication(ApplicationName), ProviderId.MbedCrypto));
        Assert.Throws<ArgumentNullException>(
            () => new ParsecKeyOperations(new ScriptedTransport(), null!, ProviderId.MbedCrypto));
    }

    [Fact]
    public void TheSigningDefaultsMatchWhatTheDocumentationPromises()
    {
        var rsa = KeyAttributes.RsaSigningKey();

        Assert.Equal(KeyType.RsaKeyPair, rsa.Type);
        Assert.Equal(2048u, rsa.Bits);
        Assert.Equal(KeyUsages.SignHash | KeyUsages.VerifyHash, rsa.Policy.Usage);
        Assert.Equal(SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256), rsa.Policy.Algorithm);

        var ecc = KeyAttributes.EccSigningKey();

        Assert.Equal(KeyType.EccKeyPair(EccFamily.SecpR1), ecc.Type);
        Assert.Equal(256u, ecc.Bits);
        Assert.Equal(SignatureAlgorithm.Ecdsa(Hash.Sha256), ecc.Policy.Algorithm);
    }

    [Fact]
    public void AKeyIsNotExportableUnlessTheCallerAsks()
    {
        // Export is the permission that undoes the point of the service, so it is off by default
        // and the caller has to name it.
        Assert.False(KeyAttributes.RsaSigningKey().Policy.Usage.HasFlag(KeyUsages.Export));
        Assert.False(KeyAttributes.EccSigningKey().Policy.Usage.HasFlag(KeyUsages.Export));

        // Asking for export must add the permission and keep the two that were already there.
        var every = KeyUsages.SignHash | KeyUsages.VerifyHash | KeyUsages.Export;
        Assert.Equal(every, KeyAttributes.RsaSigningKey(exportable: true).Policy.Usage);
        Assert.Equal(every, KeyAttributes.EccSigningKey(exportable: true).Policy.Usage);
    }

    [Fact]
    public void TheEncryptionDefaultsMatchWhatTheDocumentationPromises()
    {
        var rsa = KeyAttributes.RsaEncryptionKey();

        Assert.Equal(KeyUsages.Encrypt | KeyUsages.Decrypt, rsa.Policy.Usage);
        Assert.Equal(EncryptionAlgorithm.RsaOaep(Hash.Sha256), rsa.Policy.Algorithm);

        var aes = KeyAttributes.AesKey();

        Assert.Equal(KeyType.Aes, aes.Type);
        Assert.Equal(256u, aes.Bits);
        Assert.Equal(KeyUsages.Encrypt | KeyUsages.Decrypt, aes.Policy.Usage);
        Assert.Equal(AeadAlgorithm.Gcm, aes.Policy.Algorithm);
    }

    [Fact]
    public void ACallerCanChooseTheAlgorithmInsteadOfTheDefault()
    {
        var chosen = SignatureAlgorithm.RsaPss(Hash.Sha512);
        Assert.Equal(chosen, KeyAttributes.RsaSigningKey(algorithm: chosen).Policy.Algorithm);

        var curve = SignatureAlgorithm.DeterministicEcdsa(Hash.Sha384);
        Assert.Equal(curve, KeyAttributes.EccSigningKey(algorithm: curve).Policy.Algorithm);

        var padding = EncryptionAlgorithm.RsaPkcs1v15Crypt;
        Assert.Equal(padding, KeyAttributes.RsaEncryptionKey(algorithm: padding).Policy.Algorithm);

        var aead = AeadAlgorithm.Ccm;
        Assert.Equal(aead, KeyAttributes.AesKey(algorithm: aead).Policy.Algorithm);
    }

    [Fact]
    public void ACallerCanChooseTheSizeInsteadOfTheDefault()
    {
        Assert.Equal(4096u, KeyAttributes.RsaSigningKey(4096).Bits);
        Assert.Equal(384u, KeyAttributes.EccSigningKey(EccFamily.SecpR1, 384).Bits);
        Assert.Equal(3072u, KeyAttributes.RsaEncryptionKey(3072).Bits);
        Assert.Equal(128u, KeyAttributes.AesKey(128).Bits);
        Assert.Equal(
            KeyType.EccKeyPair(EccFamily.BrainpoolPR1),
            KeyAttributes.EccSigningKey(EccFamily.BrainpoolPR1).Type);
    }

    private static async Task AssertRefusesNullName(Func<Task> call)
    {
        var fault = await Assert.ThrowsAsync<ArgumentNullException>(call);

        Assert.Equal("name", fault.ParamName);
    }

    private static ParsecKeyOperations CreateOperations(ScriptedTransport transport) =>
        new(transport, new DirectAuthentication(ApplicationName), ProviderId.MbedCrypto);
}
