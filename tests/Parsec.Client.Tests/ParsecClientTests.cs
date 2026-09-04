using Google.Protobuf;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers how a client is built and which provider it binds to.
/// </summary>
/// <remarks>
/// <see cref="ParsecClient.CreateAsync"/> opens a real socket, so these tests run a listener of
/// their own rather than a scripted transport. The listener answers the Ping and the
/// ListProviders that building a client asks for, and nothing else.
/// </remarks>
[Collection(nameof(SocketTestGroup))]
public sealed class ParsecClientTests
{
    [Fact]
    public async Task ACreatedClientTakesTheFirstProviderThatIsNotTheCore()
    {
        await using var service = new FakeParsecService(
            Provider(ProviderId.Core, "Core provider"),
            Provider(ProviderId.MbedCrypto, "Mbed Crypto provider"),
            Provider(ProviderId.Tpm, "TPM provider"));

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions { Endpoint = service.Endpoint },
            TestContext.Current.CancellationToken);

        Assert.Equal(ProviderId.MbedCrypto, client.Provider);
        Assert.Equal("Mbed Crypto provider", client.ProviderName);
        Assert.Equal(new Version(1, 0), client.WireProtocolVersion);
    }

    [Fact]
    public async Task ACallerCanNameTheProviderInstead()
    {
        await using var service = new FakeParsecService(
            Provider(ProviderId.Core, "Core provider"),
            Provider(ProviderId.MbedCrypto, "Mbed Crypto provider"),
            Provider(ProviderId.Tpm, "TPM provider"));

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions { Endpoint = service.Endpoint, Provider = ProviderId.Tpm },
            TestContext.Current.CancellationToken);

        Assert.Equal(ProviderId.Tpm, client.Provider);
        Assert.Equal("TPM provider", client.ProviderName);
    }

    [Fact]
    public async Task AProviderTheServiceDoesNotRunIsRefusedWithTheOnesItDoes()
    {
        await using var service = new FakeParsecService(
            Provider(ProviderId.Core, "Core provider"),
            Provider(ProviderId.MbedCrypto, "Mbed Crypto provider"));

        var fault = await Assert.ThrowsAsync<ParsecConfigurationException>(
            () => ParsecClient.CreateAsync(
                new ParsecClientOptions { Endpoint = service.Endpoint, Provider = ProviderId.Tpm },
                TestContext.Current.CancellationToken));

        // The message has to list what there is, separated, or it sends the reader looking.
        Assert.Contains("no Tpm provider (3)", fault.Message, StringComparison.Ordinal);
        Assert.Contains("It runs: Core, MbedCrypto.", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServiceWithNothingButTheCoreProviderIsRefused()
    {
        // Binding to the core provider would give a client that cannot do the work a client
        // exists for, and every key operation would fail later for a reason that does not name
        // the real problem.
        await using var service = new FakeParsecService(Provider(ProviderId.Core, "Core provider"));

        var fault = await Assert.ThrowsAsync<ParsecConfigurationException>(
            () => ParsecClient.CreateAsync(
                new ParsecClientOptions { Endpoint = service.Endpoint },
                TestContext.Current.CancellationToken));

        Assert.Contains("runs the core provider and no other", fault.Message, StringComparison.Ordinal);
        Assert.Contains("runs no cryptography", fault.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildingAClientCostsExactlyOnePingAndOneListProviders()
    {
        // Doing this once at the start is the trade. If it grew a third round trip, or if an
        // operation started repeating it, that would be worth knowing.
        await using var service = new FakeParsecService(
            Provider(ProviderId.Core, "Core provider"),
            Provider(ProviderId.MbedCrypto, "Mbed Crypto provider"));

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions { Endpoint = service.Endpoint },
            TestContext.Current.CancellationToken);

        Assert.Equal([Opcode.Ping, Opcode.ListProviders], service.Received);
    }

    [Fact]
    public async Task TheSubOperationsAllBindToTheChosenProvider()
    {
        await using var service = new FakeParsecService(
            Provider(ProviderId.Core, "Core provider"),
            Provider(ProviderId.MbedCrypto, "Mbed Crypto provider"));

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions { Endpoint = service.Endpoint },
            TestContext.Current.CancellationToken);

        service.Reset();

        // Any operation will do. What matters is the provider field of the header, which is what
        // decides who runs the request.
        _ = await Assert.ThrowsAnyAsync<ParsecException>(() => client.Keys.DestroyKeyAsync(
            "gone",
            TestContext.Current.CancellationToken));

        Assert.Equal(ProviderId.MbedCrypto, Assert.Single(service.ReceivedProviders));
    }

    [Fact]
    public async Task TheCoreOperationsStillGoToTheCoreProvider()
    {
        await using var service = new FakeParsecService(
            Provider(ProviderId.Core, "Core provider"),
            Provider(ProviderId.MbedCrypto, "Mbed Crypto provider"));

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions { Endpoint = service.Endpoint },
            TestContext.Current.CancellationToken);

        service.Reset();

        _ = await client.ListProvidersAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderId.Core, Assert.Single(service.ReceivedProviders));
    }

    [Fact]
    public async Task TheDefaultsAreTheOnesTheOptionsDocument()
    {
        var options = new ParsecClientOptions();

        Assert.Null(options.Endpoint);
        Assert.Null(options.Provider);
        Assert.Same(NoAuthentication.Instance, options.Authentication);
        Assert.Equal(UnixDomainSocketTransport.DefaultTimeout, options.ConnectTimeout);
        Assert.Equal(UnixDomainSocketTransport.DefaultTimeout, options.IoTimeout);
        Assert.Equal(WireHeader.DefaultMaxContentLength, options.MaxBodyLength);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task NoOptionsAtAllReadsTheEndpointFromTheEnvironment()
    {
        // Passing null must behave as the defaults do rather than raising, because that is the
        // shortest call an application can write.
        await using var service = new FakeParsecService(
            Provider(ProviderId.Core, "Core provider"),
            Provider(ProviderId.MbedCrypto, "Mbed Crypto provider"));

        var previous = Environment.GetEnvironmentVariable(ParsecEndpoint.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(
            ParsecEndpoint.EnvironmentVariableName,
            service.Endpoint.ToString());

        try
        {
            await using var client = await ParsecClient.CreateAsync(
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(ProviderId.MbedCrypto, client.Provider);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ParsecEndpoint.EnvironmentVariableName, previous);
        }
    }

    [Fact]
    public async Task AServiceThatIsNotThereFailsWhenTheClientIsBuilt()
    {
        // The point of the two round trips is that this happens here rather than inside the
        // first operation an application happens to call.
        var missing = new Uri("unix:" + Path.Combine(Path.GetTempPath(), "no-parsec.sock")
            .Replace('\\', '/'));

        await Assert.ThrowsAsync<ParsecTransportException>(() => ParsecClient.CreateAsync(
            new ParsecClientOptions { Endpoint = missing },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DescribingAMissingProviderRefusesANullList()
    {
        var fault = Assert.Throws<ArgumentNullException>(
            () => ParsecErrorText.DescribeMissingProvider(ProviderId.Tpm, null!));

        Assert.Equal("providers", fault.ParamName);
    }

    private static ListProviders.ProviderInfo Provider(ProviderId id, string description) => new()
    {
        Id = (uint)id,
        Uuid = $"00000000-0000-0000-0000-00000000000{(uint)id}",
        Description = description,
        Vendor = "test",
        VersionMaj = 1,
        VersionMin = 0,
        VersionRev = 0,
    };
}
