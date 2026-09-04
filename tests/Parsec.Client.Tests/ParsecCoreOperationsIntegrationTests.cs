using Parsec.Client.Algorithms;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Runs the core operations against the real Parsec service.
/// </summary>
/// <remarks>
/// Every other test of this project plays the service with a fake. These tests send the bytes of
/// the client over a Unix domain socket to the service itself, so they are the ones that prove
/// the wire code against the software that it must talk to.
/// </remarks>
/// <param name="service">The service that the fixture started.</param>
[Trait("Category", "IntegrationTests")]
[Collection(nameof(SocketTestGroup))]
public sealed class ParsecCoreOperationsIntegrationTests(ParsecServiceFixture service)
    : IClassFixture<ParsecServiceFixture>
{
    [Fact]
    public async Task PingAnswersTheVersionOfTheWireProtocol()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var operations = service.CreateOperations();

        var version = await operations.PingAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Version(1, 0), version);
        Assert.Equal(version, operations.NegotiatedWireProtocolVersion);
    }

    [Fact]
    public async Task ListProvidersHoldsTheMbedCryptoProvider()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var providers = await service.CreateOperations().ListProvidersAsync(TestContext.Current.CancellationToken);

        var mbed = Assert.Single(providers, provider => provider.Id == ProviderId.MbedCrypto);

        Assert.NotEmpty(mbed.Uuid);
        Assert.NotEmpty(mbed.Description);
        Assert.Contains(providers, provider => provider.Id == ProviderId.Core);
        Assert.All(providers, provider => Assert.True(provider.Id.IsKnown()));
    }

    [Fact]
    public async Task ListOpcodesHoldsTheOperationsOfTheMbedCryptoProvider()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var opcodes = await service.CreateOperations().ListOpcodesAsync(
            ProviderId.MbedCrypto,
            TestContext.Current.CancellationToken);

        Assert.Contains(Opcode.PsaGenerateKey, opcodes);
        Assert.Contains(Opcode.PsaSignHash, opcodes);
        Assert.DoesNotContain(Opcode.Ping, opcodes);
    }

    [Fact]
    public async Task ListOpcodesHoldsPingForTheCoreProvider()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var opcodes = await service.CreateOperations().ListOpcodesAsync(
            ProviderId.Core,
            TestContext.Current.CancellationToken);

        Assert.Contains(Opcode.Ping, opcodes);
        Assert.Contains(Opcode.ListProviders, opcodes);
    }

    [Fact]
    public async Task ListAuthenticatorsHoldsTheDirectAuthenticator()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var authenticators = await service.CreateOperations().ListAuthenticatorsAsync(
            TestContext.Current.CancellationToken);

        // The container runs the direct authenticator, which is the one the tests use.
        Assert.Contains(authenticators, authenticator => authenticator.Id == AuthType.Direct);
    }

    [Fact]
    public async Task ListKeysAnswersTheKeysOfThisApplication()
    {
        service.SkipWhenTheServiceDoesNotRun();

        // The application made no key, so the answer is empty. It must still be an answer and not
        // a fault: the request carries the identity of the application, so the service does not
        // refuse it.
        var keys = await service.CreateOperations().ListKeysAsync(TestContext.Current.CancellationToken);

        Assert.Empty(keys);
    }

    [Fact]
    public async Task CanDoCryptoAnswersTrueForAKeyThatTheProviderSupports()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var answer = await service.CreateOperations().CanDoCryptoAsync(
            ProviderId.MbedCrypto,
            KeyCheckType.Use,
            CreateRsaSigningAttributes(),
            TestContext.Current.CancellationToken);

        Assert.True(answer);
    }

    [Fact]
    public async Task CanDoCryptoAnswersFalseForACheckThatTheProviderDoesNotSupport()
    {
        service.SkipWhenTheServiceDoesNotRun();

        // The service supports no key derivation, so it answers PsaErrorNotSupported. The client
        // turns that answer into false, because it is the normal way to say no.
        var answer = await service.CreateOperations().CanDoCryptoAsync(
            ProviderId.MbedCrypto,
            KeyCheckType.Derive,
            CreateRsaSigningAttributes(),
            TestContext.Current.CancellationToken);

        Assert.False(answer);
    }

    /// <summary>
    /// Builds the attributes of an RSA key of 2048 bits that signs a SHA-256 hash.
    /// </summary>
    /// <returns>Attributes that the Mbed Crypto provider supports.</returns>
    private static KeyAttributes CreateRsaSigningAttributes() => new(
        KeyType.RsaKeyPair,
        2048,
        new KeyPolicy(
            KeyUsages.SignHash | KeyUsages.VerifyHash,
            SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256)));
}
