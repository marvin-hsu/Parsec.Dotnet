using Google.Protobuf;
using Parsec.Client.Operations;

namespace Parsec.Client.Tests;

/// <summary>
/// Locks the request bytes and the decoding of the core operations.
/// </summary>
/// <remarks>
/// The expected request bytes are written out by hand, field by field, so a test never agrees
/// with a wrong encoder. The answers go through the real frame reader of the scripted transport,
/// so a malformed answer reaches the operations the way a real one would.
/// </remarks>
public sealed class ParsecCoreOperationsTests
{
    /// <summary>
    /// A Ping request for the core provider. The header states version 1.0, provider 0, auth type
    /// 0, content length 0, auth length 0 and opcode 1. Ping has no body and no authentication
    /// field, so the message is the header alone.
    /// </summary>
    private const string PingRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "01000000" + "0000" + "0000";

    /// <summary>
    /// A ListProviders request for the core provider with direct authentication. The header
    /// states auth type 1, auth length 3 and opcode 8. The field is the UTF-8 text "app".
    /// </summary>
    private const string ListProvidersRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "01" + "00000000" + "0300" + "08000000" + "0000" + "0000" +
        "617070";

    /// <summary>
    /// A ListOpcodes request that asks about the Mbed Crypto provider. The question goes to the
    /// core provider, so the provider field of the header stays 0 and the provider that the
    /// question is about travels in the body. The body is field 1 of the operation message with
    /// the value 1, which protobuf writes as 08 01. The header states content length 2, auth
    /// type 1, auth length 3 and opcode 9.
    /// </summary>
    private const string ListOpcodesRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "01" + "02000000" + "0300" + "09000000" + "0000" + "0000" +
        "0801" + "617070";

    /// <summary>
    /// A ListKeys request for the core provider with direct authentication. The header states
    /// content length 0, auth type 1, auth length 3 and opcode 26, which is 0x1A.
    /// </summary>
    private const string ListKeysRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "01" + "00000000" + "0300" + "1A000000" + "0000" + "0000" +
        "617070";

    /// <summary>
    /// A ListAuthenticators request for the core provider with direct authentication. The header
    /// states content length 0, auth type 1, auth length 3 and opcode 14, which is 0x0E.
    /// </summary>
    private const string ListAuthenticatorsRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "01" + "00000000" + "0300" + "0E000000" + "0000" + "0000" +
        "617070";

    /// <summary>
    /// A CanDoCrypto request for the Mbed Crypto provider. This question goes to the provider
    /// itself, so the provider field of the header holds 1. The body is field 1 with the check
    /// type Use, which is 08 01, and field 2 with an empty attributes message, which is 12 00.
    /// The header states content length 4, auth type 1, auth length 3 and opcode 32, which is
    /// 0x20.
    /// </summary>
    private const string CanDoCryptoRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "01" + "0000000000000000" +
        "00" + "00" + "01" + "04000000" + "0300" + "20000000" + "0000" + "0000" +
        "08011200" + "617070";

    /// <summary>
    /// The body of the Ping answer of the real service. It holds the major version 1. The minor
    /// version is 0, and protobuf leaves a field that holds a default value off the wire.
    /// </summary>
    private static readonly byte[] _pingBody = [0x08, 0x01];

    [Fact]
    public async Task PingWritesTheGoldenRequestBytes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success, _pingBody);
        var operations = CreateOperations(transport);

        await operations.PingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(PingRequestHex, Convert.ToHexString(Assert.Single(transport.SentRequestBytes)));
    }

    [Fact]
    public async Task PingReadsTheVersionOfTheRealAnswer()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success, _pingBody);
        var operations = CreateOperations(transport);

        Assert.Null(operations.NegotiatedWireProtocolVersion);

        var version = await operations.PingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Version(1, 0), version);
        Assert.Equal(new Version(1, 0), operations.NegotiatedWireProtocolVersion);
    }

    [Fact]
    public async Task PingReadsAVersionThatCarriesBothNumbers()
    {
        var body = new Ping.Result { WireProtocolVersionMaj = 2, WireProtocolVersionMin = 1 }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success, body);

        var version = await CreateOperations(transport).PingAsync(TestContext.Current.CancellationToken);

        Assert.Equal("0802 1001", Convert.ToHexString(body).Insert(4, " "));
        Assert.Equal(new Version(2, 1), version);
    }

    [Fact]
    public async Task PingTakesAnEmptyBodyAsTheDefaultVersion()
    {
        // Protobuf leaves every field that holds a default value off the wire, so a version 0.0
        // encodes to no bytes at all. An empty body is a value, not a fault.
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success);

        var version = await CreateOperations(transport).PingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Version(0, 0), version);
    }

    [Theory]
    [InlineData(256u, 0u, "wire protocol major version")]
    [InlineData(0u, 256u, "wire protocol minor version")]
    [InlineData(uint.MaxValue, 0u, "wire protocol major version")]
    public async Task PingRefusesAVersionNumberThatTheHeaderCannotCarry(
        uint major,
        uint minor,
        string expectedField)
    {
        // Each number travels in one byte of the header of every other message.
        var body = new Ping.Result { WireProtocolVersionMaj = major, WireProtocolVersionMin = minor }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success, body);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.PingAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.Ping, fault.Operation);
        Assert.Contains(expectedField, fault.Message, StringComparison.Ordinal);
        Assert.Null(operations.NegotiatedWireProtocolVersion);
    }

    [Fact]
    public async Task PingCarriesNoAuthenticationEvenWhenTheApplicationHasOne()
    {
        // Ping needs no identity, and an application calls it before it knows which authenticator
        // the service runs.
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success, _pingBody);
        var operations = new ParsecCoreOperations(transport, new DirectAuthentication("app"));

        await operations.PingAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(transport.SentRequests);

        Assert.Equal(AuthType.None, request.Header.AuthType);
        Assert.Equal(0, request.Header.AuthLength);
        Assert.True(request.Auth.IsEmpty);
    }

    [Fact]
    public async Task ListProvidersWritesTheGoldenRequestBytes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success);

        await CreateOperations(transport).ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ListProvidersRequestHex, Convert.ToHexString(Assert.Single(transport.SentRequestBytes)));
    }

    [Fact]
    public async Task ListProvidersReadsEveryFieldOfEveryProvider()
    {
        var body = new ListProviders.Result
        {
            Providers =
            {
                new ListProviders.ProviderInfo
                {
                    Id = 1,
                    Uuid = "1c1139dc-ad7c-47dc-ad6b-db6fdb466552",
                    Description = "User space software provider",
                    Vendor = "Arm",
                    VersionMaj = 1,
                    VersionMin = 2,
                    VersionRev = 3,
                },
                new ListProviders.ProviderInfo
                {
                    Id = 0,
                    Uuid = "47049873-2a43-4845-9d72-831eab668784",
                    Description = "Software provider",
                    Vendor = string.Empty,
                    VersionMaj = 1,
                    VersionMin = 5,
                    VersionRev = 0,
                },
            },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success, body);

        var providers = await CreateOperations(transport).ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, providers.Count);
        Assert.Equal(ProviderId.MbedCrypto, providers[0].Id);
        Assert.Equal("1c1139dc-ad7c-47dc-ad6b-db6fdb466552", providers[0].Uuid);
        Assert.Equal("User space software provider", providers[0].Description);
        Assert.Equal("Arm", providers[0].Vendor);
        Assert.Equal(new Version(1, 2, 3), providers[0].Version);
        Assert.Equal(ProviderId.Core, providers[1].Id);
        Assert.Equal(string.Empty, providers[1].Vendor);
        Assert.Equal(new Version(1, 5, 0), providers[1].Version);
    }

    [Fact]
    public async Task ListProvidersKeepsAProviderThatThisVersionDoesNotName()
    {
        var body = new ListProviders.Result
        {
            Providers = { new ListProviders.ProviderInfo { Id = 200 } },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success, body);

        var providers = await CreateOperations(transport).ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Equal((ProviderId)200, Assert.Single(providers).Id);
        Assert.False(providers[0].Id.IsKnown());
    }

    [Fact]
    public async Task ListProvidersRefusesAProviderIdThatTheHeaderCannotCarry()
    {
        // The provider field of the header holds one byte, so the client could never send a
        // request to a provider of 256.
        var body = new ListProviders.Result
        {
            Providers = { new ListProviders.ProviderInfo { Id = 256 } },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success, body);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.ListProvidersAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.ListProviders, fault.Operation);
        Assert.Contains("256", fault.Message, StringComparison.Ordinal);
        Assert.Contains("provider identifier", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListProvidersTakesAnEmptyAnswer()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success);

        var providers = await CreateOperations(transport).ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Empty(providers);
    }

    [Fact]
    public async Task ListOpcodesWritesTheGoldenRequestBytes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListOpcodes, ResponseStatus.Success);

        await CreateOperations(transport).ListOpcodesAsync(
            ProviderId.MbedCrypto,
            TestContext.Current.CancellationToken);

        Assert.Equal(ListOpcodesRequestHex, Convert.ToHexString(Assert.Single(transport.SentRequestBytes)));
    }

    [Fact]
    public async Task ListOpcodesKeepsAnOperationThatThisVersionDoesNotName()
    {
        // 0x1D is not assigned today. A service that assigns it must not break the client.
        var body = new ListOpcodes.Result { Opcodes = { 1u, 8u, 0x1Du } }.ToByteArray();
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListOpcodes, ResponseStatus.Success, body);

        var opcodes = await CreateOperations(transport).ListOpcodesAsync(
            ProviderId.Core,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, opcodes.Count);
        Assert.Contains(Opcode.Ping, opcodes);
        Assert.Contains(Opcode.ListProviders, opcodes);
        Assert.Contains((Opcode)0x1D, opcodes);
        Assert.False(((Opcode)0x1D).IsKnown());
    }

    [Fact]
    public async Task ListAuthenticatorsWritesTheGoldenRequestBytes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListAuthenticators, ResponseStatus.Success);

        await CreateOperations(transport).ListAuthenticatorsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ListAuthenticatorsRequestHex, Convert.ToHexString(Assert.Single(transport.SentRequestBytes)));
    }

    [Fact]
    public async Task ListAuthenticatorsReadsEveryFieldOfEveryAuthenticator()
    {
        var body = new ListAuthenticators.Result
        {
            Authenticators =
            {
                new ListAuthenticators.AuthenticatorInfo
                {
                    Id = 1,
                    Description = "Directly says which application is asking",
                    VersionMaj = 0,
                    VersionMin = 1,
                    VersionRev = 0,
                },
                new ListAuthenticators.AuthenticatorInfo { Id = 200 },
            },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.ListAuthenticators,
            ResponseStatus.Success,
            body);

        var authenticators = await CreateOperations(transport).ListAuthenticatorsAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(2, authenticators.Count);
        Assert.Equal(AuthType.Direct, authenticators[0].Id);
        Assert.Equal("Directly says which application is asking", authenticators[0].Description);
        Assert.Equal(new Version(0, 1, 0), authenticators[0].Version);
        Assert.Equal((AuthType)200, authenticators[1].Id);
        Assert.False(authenticators[1].Id.IsKnown());
    }

    [Fact]
    public async Task ListAuthenticatorsRefusesAnIdentifierThatTheHeaderCannotCarry()
    {
        var body = new ListAuthenticators.Result
        {
            Authenticators = { new ListAuthenticators.AuthenticatorInfo { Id = 256 } },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.ListAuthenticators,
            ResponseStatus.Success,
            body);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.ListAuthenticatorsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.ListAuthenticators, fault.Operation);
        Assert.Contains("authenticator identifier", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListKeysWritesTheGoldenRequestBytes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListKeys, ResponseStatus.Success);

        await CreateOperations(transport).ListKeysAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ListKeysRequestHex, Convert.ToHexString(Assert.Single(transport.SentRequestBytes)));
    }

    [Fact]
    public async Task ListKeysReadsTheProviderAndTheNameOfEveryKey()
    {
        var body = new ListKeys.Result
        {
            Keys =
            {
                new ListKeys.KeyInfo { ProviderId = 1, Name = "signing-key" },
                new ListKeys.KeyInfo { ProviderId = 3, Name = "密鑰" },
            },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListKeys, ResponseStatus.Success, body);

        var keys = await CreateOperations(transport).ListKeysAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, keys.Count);
        Assert.Equal(ProviderId.MbedCrypto, keys[0].Provider);
        Assert.Equal("signing-key", keys[0].Name);
        Assert.Equal(ProviderId.Tpm, keys[1].Provider);
        Assert.Equal("密鑰", keys[1].Name);
    }

    [Fact]
    public async Task CanDoCryptoWritesTheGoldenRequestBytes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.CanDoCrypto,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        await CreateOperations(transport).CanDoCryptoAsync(
            ProviderId.MbedCrypto,
            CanDoCrypto.CheckType.Use,
            new PsaKeyAttributes.KeyAttributes(),
            TestContext.Current.CancellationToken);

        Assert.Equal(CanDoCryptoRequestHex, Convert.ToHexString(Assert.Single(transport.SentRequestBytes)));
    }

    [Fact]
    public async Task CanDoCryptoAnswersTrueOnSuccess()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.CanDoCrypto,
            ResponseStatus.Success,
            default,
            ProviderId.MbedCrypto);

        Assert.True(await CreateOperations(transport).CanDoCryptoAsync(
            ProviderId.MbedCrypto,
            CanDoCrypto.CheckType.Generate,
            new PsaKeyAttributes.KeyAttributes(),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanDoCryptoAnswersFalseWhenTheProviderSaysNotSupported()
    {
        // A provider that cannot work with the attributes answers PsaErrorNotSupported. That is
        // the normal way to say no, so it must not raise an exception.
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.CanDoCrypto,
            ResponseStatus.PsaErrorNotSupported,
            default,
            ProviderId.MbedCrypto);

        Assert.False(await CreateOperations(transport).CanDoCryptoAsync(
            ProviderId.MbedCrypto,
            CanDoCrypto.CheckType.Generate,
            new PsaKeyAttributes.KeyAttributes(),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ResponseStatus.PsaErrorInvalidArgument)]
    [InlineData(ResponseStatus.PsaErrorHardwareFailure)]
    [InlineData(ResponseStatus.NotAuthenticated)]
    public async Task CanDoCryptoRaisesEveryOtherFailedStatus(ResponseStatus status)
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.CanDoCrypto,
            status,
            default,
            ProviderId.MbedCrypto);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAnyAsync<ParsecServiceException>(() => operations.CanDoCryptoAsync(
            ProviderId.MbedCrypto,
            CanDoCrypto.CheckType.Use,
            new PsaKeyAttributes.KeyAttributes(),
            TestContext.Current.CancellationToken));

        Assert.Equal(status, fault.Status);
        Assert.Equal(Opcode.CanDoCrypto, fault.Operation);
    }

    [Fact]
    public async Task AFailedStatusBecomesTheExceptionOfTheStatus()
    {
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.ListProviders,
            ResponseStatus.ProviderNotRegistered);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecServiceException>(
            () => operations.ListProvidersAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.ProviderNotRegistered, fault.Status);
        Assert.Equal(Opcode.ListProviders, fault.Operation);
        Assert.Contains("ListProviders", fault.Message, StringComparison.Ordinal);
        Assert.Contains("ProviderNotRegistered", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APsaStatusBecomesThePsaException()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListKeys, ResponseStatus.PsaErrorDoesNotExist);
        var operations = CreateOperations(transport);

        await Assert.ThrowsAsync<ParsecPsaException>(
            () => operations.ListKeysAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnAnswerThatDoesNotParseBecomesAProtocolException()
    {
        var answer = ScriptedTransport.BuildResponseBytes(Opcode.Ping, ResponseStatus.Success, _pingBody);
        answer[0] ^= 0xFF;

        var operations = CreateOperations(new ScriptedTransport().EnqueueResponse(answer));

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.PingAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.Ping, fault.Operation);
        Assert.Contains("magic number", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAnswerThatNamesAnotherOperationBecomesAProtocolException()
    {
        // The service copies the opcode of the request into the answer. Another value means the
        // two sides do not agree on where a message starts.
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListOpcodes, ResponseStatus.Success);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.ListProvidersAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.ListProviders, fault.Operation);
        Assert.Contains("ListProviders", fault.Message, StringComparison.Ordinal);
        Assert.Contains("ListOpcodes", fault.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ResponseStatus.BodySizeExceedsLimit)]
    [InlineData(ResponseStatus.WireProtocolVersionNotSupported)]
    public async Task AFailedAnswerThatNamesAnotherOperationStillReportsItsStatus(ResponseStatus status)
    {
        // The service cannot read the opcode of a request that it refuses before it parses the
        // header. It answers with a default header, which names Ping and the core provider, and
        // carries the true status. The status is what the application must see.
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, status);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecServiceException>(
            () => operations.ListKeysAsync(TestContext.Current.CancellationToken));

        Assert.Equal(status, fault.Status);
        Assert.Equal(Opcode.ListKeys, fault.Operation);
    }

    [Fact]
    public async Task ABodyThatDoesNotDecodeBecomesAProtocolException()
    {
        // Two bytes of a varint that never ends are not a protobuf message.
        var transport = new ScriptedTransport().EnqueueResponse(
            Opcode.Ping,
            ResponseStatus.Success,
            new byte[] { 0xFF, 0xFF });
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.PingAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.Ping, fault.Operation);
        Assert.IsAssignableFrom<InvalidProtocolBufferException>(fault.InnerException);
    }

    [Fact]
    public async Task EveryOperationOpensOneConnectionAndClosesIt()
    {
        var transport = new ScriptedTransport()
            .EnqueueResponse(Opcode.Ping, ResponseStatus.Success, _pingBody)
            .EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success);
        var operations = CreateOperations(transport);

        await operations.PingAsync(TestContext.Current.CancellationToken);
        await operations.ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, transport.ConnectCount);
        Assert.Equal(2, transport.DisposedConnectionCount);
        Assert.Equal(0, transport.PendingResponseCount);
    }

    [Fact]
    public async Task AFailedOperationStillClosesTheConnection()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListKeys, ResponseStatus.NotAuthenticated);
        var operations = CreateOperations(transport);

        await Assert.ThrowsAsync<ParsecServiceException>(
            () => operations.ListKeysAsync(TestContext.Current.CancellationToken));

        Assert.Equal(1, transport.ConnectCount);
        Assert.Equal(1, transport.DisposedConnectionCount);
    }

    [Fact]
    public async Task AnAnswerThatArrivesOneByteAtATimeStillDecodes()
    {
        var transport = new ScriptedTransport { ChunkSize = 1 };
        transport.EnqueueResponse(Opcode.Ping, ResponseStatus.Success, _pingBody);

        var version = await CreateOperations(transport).PingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Version(1, 0), version);
        Assert.True(transport.LastResponseReadCount >= 38, "The answer did not arrive in pieces.");
    }

    [Fact]
    public async Task ACancelledTokenStopsTheOperation()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success, _pingBody);
        var operations = CreateOperations(transport);
        using var source = new CancellationTokenSource();

        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operations.PingAsync(source.Token));
    }

    [Fact]
    public void TheOperationsRefuseANullAuthentication() =>
        Assert.Throws<ArgumentNullException>(() => new ParsecCoreOperations(new ScriptedTransport(), null!));

    [Fact]
    public async Task ListProvidersTakesTheLargestIdentifierThatTheHeaderCanCarry()
    {
        // 255 is the last value that the one-byte provider field of the header holds, so it is
        // an answer that the client must keep and not refuse.
        var body = new ListProviders.Result
        {
            Providers = { new ListProviders.ProviderInfo { Id = 255 } },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success, body);

        var providers = await CreateOperations(transport).ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Equal((ProviderId)255, Assert.Single(providers).Id);
        Assert.False(providers[0].Id.IsKnown());
    }

    [Fact]
    public async Task ListProvidersTakesTheLargestVersionNumberThatAVersionHolds()
    {
        // A version number is a 32-bit value on the wire and a signed number in Version, so the
        // last value that both hold is int.MaxValue.
        var body = new ListProviders.Result
        {
            Providers = { new ListProviders.ProviderInfo { VersionMaj = int.MaxValue } },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success, body);

        var providers = await CreateOperations(transport).ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Version(int.MaxValue, 0, 0), Assert.Single(providers).Version);
    }

    [Fact]
    public async Task ListProvidersRefusesAVersionNumberThatAVersionCannotHold()
    {
        var body = new ListProviders.Result
        {
            Providers = { new ListProviders.ProviderInfo { VersionMaj = (uint)int.MaxValue + 1 } },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListProviders, ResponseStatus.Success, body);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.ListProvidersAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.ListProviders, fault.Operation);
        Assert.Contains("version number", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListKeysRefusesAProviderIdThatTheHeaderCannotCarry()
    {
        var body = new ListKeys.Result
        {
            Keys = { new ListKeys.KeyInfo { ProviderId = 256, Name = "signing-key" } },
        }.ToByteArray();

        var transport = new ScriptedTransport().EnqueueResponse(Opcode.ListKeys, ResponseStatus.Success, body);
        var operations = CreateOperations(transport);

        var fault = await Assert.ThrowsAsync<ParsecProtocolException>(
            () => operations.ListKeysAsync(TestContext.Current.CancellationToken));

        Assert.Equal(Opcode.ListKeys, fault.Operation);
        Assert.Contains("provider identifier", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOperationsRefuseANullTransport() =>
        Assert.Throws<ArgumentNullException>(() => new ParsecCoreOperations(null!, new DirectAuthentication("app")));

    /// <summary>
    /// Makes the operations with the direct authentication that the golden request bytes hold.
    /// </summary>
    /// <param name="transport">The transport that plays the service.</param>
    /// <returns>The operations to call.</returns>
    private static ParsecCoreOperations CreateOperations(ScriptedTransport transport) =>
        new(transport, new DirectAuthentication("app"));
}
