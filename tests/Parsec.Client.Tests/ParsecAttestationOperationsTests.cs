using Google.Protobuf;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Operations;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the attestation operations against a scripted service.
/// </summary>
/// <remarks>
/// Only a provider backed by a TPM answers these, and the image carries none, so a scripted
/// service is the only place the success path can be exercised at all. That makes these tests
/// the whole of the coverage rather than a fast substitute for it.
/// </remarks>
public sealed class ParsecAttestationOperationsTests
{
    private const string ApplicationName = "app";

    private static readonly byte[] KeyName = [0x00, 0x0B, 0x01];

    private static readonly byte[] PublicArea = [0x01, 0x16];

    private static readonly byte[] AttestingPublicArea = [0x02, 0x27];

    [Fact]
    public async Task PrepareSendsBothKeyNamesAndReadsTheThreeBlobsBack()
    {
        var body = new PrepareKeyAttestation.Result
        {
            Output = new PrepareKeyAttestation.PrepareKeyAttestationOutput
            {
                ActivateCredential = new PrepareKeyAttestation.PrepareKeyAttestationOutput.Types.ActivateCredential
                {
                    Name = ByteString.CopyFrom(KeyName),
                    Public = ByteString.CopyFrom(PublicArea),
                    AttestingKeyPub = ByteString.CopyFrom(AttestingPublicArea),
                },
            },
        }.ToByteArray();
        var transport = Script(Opcode.PrepareKeyAttestation, ResponseStatus.Success, body);

        var answer = await CreateOperations(transport).PrepareActivateCredentialAsync(
            "attested",
            "attesting",
            TestContext.Current.CancellationToken);

        Assert.Equal(KeyName, answer.Name.ToArray());
        Assert.Equal(PublicArea, answer.PublicKey.ToArray());
        Assert.Equal(AttestingPublicArea, answer.AttestingKeyPublicKey.ToArray());

        var request = Assert.Single(transport.SentRequests);
        Assert.Equal(Opcode.PrepareKeyAttestation, request.Header.Opcode);

        var sent = PrepareKeyAttestation.Operation.Parser.ParseFrom(request.Body.Span);
        Assert.Equal("attested", sent.Parameters.ActivateCredential.AttestedKeyName);
        Assert.Equal("attesting", sent.Parameters.ActivateCredential.AttestingKeyName);
    }

    [Fact]
    public async Task AttestSendsTheCredentialAndTheSecretAndReadsTheCredentialBack()
    {
        var credential = new byte[] { 0xC0, 0xDE };
        var body = new AttestKey.Result
        {
            Output = new AttestKey.AttestationOutput
            {
                ActivateCredential = new AttestKey.AttestationOutput.Types.ActivateCredential
                {
                    Credential = ByteString.CopyFrom(credential),
                },
            },
        }.ToByteArray();
        var transport = Script(Opcode.AttestKey, ResponseStatus.Success, body);
        var blob = new byte[] { 0xAA };
        var secret = new byte[] { 0xBB };

        var answer = await CreateOperations(transport).AttestKeyWithActivateCredentialAsync(
            "attested",
            "attesting",
            blob,
            secret,
            TestContext.Current.CancellationToken);

        Assert.Equal(credential, answer);

        var request = Assert.Single(transport.SentRequests);
        Assert.Equal(Opcode.AttestKey, request.Header.Opcode);

        var sent = AttestKey.Operation.Parser.ParseFrom(request.Body.Span);
        Assert.Equal("attested", sent.AttestedKeyName);
        Assert.Equal("attesting", sent.AttestingKeyName);
        Assert.Equal(blob, sent.Parameters.ActivateCredential.CredentialBlob.ToByteArray());
        Assert.Equal(secret, sent.Parameters.ActivateCredential.Secret.ToByteArray());
    }

    [Fact]
    public async Task AnAnswerWithNoMechanismIsAProtocolFault()
    {
        // The mechanism is a oneof with one member today. A service that adds another and picks
        // it would leave this client with nothing to read, and saying so beats handing back an
        // empty credential that a caller would treat as proof.
        var body = new PrepareKeyAttestation.Result
        {
            Output = new PrepareKeyAttestation.PrepareKeyAttestationOutput(),
        }.ToByteArray();
        var transport = Script(Opcode.PrepareKeyAttestation, ResponseStatus.Success, body);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.PrepareActivateCredentialAsync(
                "attested",
                "attesting",
                TestContext.Current.CancellationToken));

        Assert.Contains("names no mechanism", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAnswerWithNoOutputIsAProtocolFault()
    {
        var transport = Script(Opcode.AttestKey, ResponseStatus.Success);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.AttestKeyWithActivateCredentialAsync(
                "attested",
                "attesting",
                default,
                default,
                TestContext.Current.CancellationToken));

        Assert.Contains("attestation output", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProviderWithNoDeviceRaisesTheStatusItSent()
    {
        var transport = Script(Opcode.PrepareKeyAttestation, ResponseStatus.PsaErrorNotSupported);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecPsaException>(
            () => operations.PrepareActivateCredentialAsync(
                "attested",
                "attesting",
                TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorNotSupported, fault.Status);
    }

    [Fact]
    public async Task BothOperationsRefuseANullKeyName()
    {
        var operations = CreateOperations(new ScriptedTransport());
        var token = TestContext.Current.CancellationToken;

        await AssertRefuses(
            "attestedKeyName",
            () => operations.PrepareActivateCredentialAsync(null!, "attesting", token));
        await AssertRefuses(
            "attestingKeyName",
            () => operations.PrepareActivateCredentialAsync("attested", null!, token));
        await AssertRefuses(
            "attestedKeyName",
            () => operations.AttestKeyWithActivateCredentialAsync(null!, "attesting", default, default, token));
        await AssertRefuses(
            "attestingKeyName",
            () => operations.AttestKeyWithActivateCredentialAsync("attested", null!, default, default, token));
    }

    [Fact]
    public void TheOperationsRefuseANullTransportOrAuthentication()
    {
        Assert.Throws<ArgumentNullException>(() => new ParsecAttestationOperations(
            null!,
            new DirectAuthentication(ApplicationName),
            ProviderId.Tpm));
        Assert.Throws<ArgumentNullException>(() => new ParsecAttestationOperations(
            new ScriptedTransport(),
            null!,
            ProviderId.Tpm));
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
        new ScriptedTransport().EnqueueResponse(opcode, status, body, ProviderId.Tpm);

    private static ParsecAttestationOperations CreateOperations(ScriptedTransport transport) =>
        new(transport, new DirectAuthentication(ApplicationName), ProviderId.Tpm);
}
