using Parsec.Client.Algorithms;
using Parsec.Client.Authentication;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Runs the quick start of the README against the real Parsec service.
/// </summary>
/// <remarks>
/// The body below is the sample, copied line for line apart from the endpoint, which has to
/// point at the container instead of at whatever this machine happens to run, and the cleanup
/// at the end. A sample that nobody runs is a sample that stops working, and the first thing a
/// reader tries is the worst place to find that out.
/// </remarks>
/// <param name="service">The service that the fixture started.</param>
[Trait("Category", "IntegrationTests")]
[Collection(nameof(SocketTestGroup))]
public sealed class ReadmeQuickStartTests(ParsecServiceFixture service)
{
    [Fact]
    public async Task TheQuickStartOfTheReadmeRuns()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keyName = $"my-key-{Guid.NewGuid():N}";

        // xUnit1051 wants every call to carry TestContext.Current.CancellationToken. Threading
        // one through would mean the code below is no longer the sample, which is the one thing
        // this test exists to check. The calls inherit the timeouts of the client instead.
#pragma warning disable xUnit1051

        // The client finds the service, agrees a protocol version and picks a provider.
        await using var client = await ParsecClient.CreateAsync(new ParsecClientOptions
        {
            Endpoint = service.Endpoint,
            Authentication = new DirectAuthentication("my-application"),
        });

        try
        {
            // Create a signing key. The private half never leaves the service.
            var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

            await client.Keys.GenerateKeyAsync(keyName, KeyAttributes.RsaSigningKey(algorithm: algorithm));

            // Sign a hash and check the signature.
            var digest = await client.Crypto.HashComputeAsync(Hash.Sha256, "sign me"u8.ToArray());
            var signature = await client.Crypto.SignHashAsync(keyName, algorithm, digest);

            var ok = await client.Crypto.VerifyHashAsync(keyName, algorithm, digest, signature);

            Assert.True(ok);
            Assert.Equal(32, digest.Length);
            Assert.Equal(256, signature.Length);

            // The description belongs to the Parsec project, so the check is that the client
            // bound to the software provider rather than that upstream still words it this way.
            Assert.Contains("Mbed Crypto", client.ProviderName, StringComparison.Ordinal);
            Assert.Equal(ProviderId.MbedCrypto, client.Provider);
            Assert.Equal(new Version(1, 0), client.WireProtocolVersion);
#pragma warning restore xUnit1051
        }
        finally
        {
            await client.Keys.DestroyKeyAsync(keyName, TestContext.Current.CancellationToken);
        }
    }
}
