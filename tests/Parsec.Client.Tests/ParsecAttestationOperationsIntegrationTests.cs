using Parsec.Client.Errors;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Runs the attestation operations against the real Parsec service, where a provider allows it.
/// </summary>
/// <remarks>
/// Attestation needs a device that can speak for a key, which means a TPM. The image of
/// <c>Parsec.Testcontainers</c> carries the software provider and nothing else, so these tests
/// skip. They check first that the reason to skip is still true rather than assuming it: an image
/// that gains a TPM provider should start running them, not go on skipping quietly.
/// </remarks>
/// <param name="service">The service that the fixture started.</param>
[Trait("Category", "IntegrationTests")]
[Collection(nameof(SocketTestGroup))]
public sealed class ParsecAttestationOperationsIntegrationTests(ParsecServiceFixture service)
{
    [Fact]
    public async Task TheServiceRunsNoProviderThatCanAttestAKey()
    {
        // This one does not skip. It is the evidence for the skip in the tests below, and if the
        // image ever gains a provider that attests, this is what says so.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var providers = await service.CreateOperations().ListProvidersAsync(
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(providers, p => p.Id is ProviderId.Tpm);
        Assert.Contains(providers, p => p.Id is ProviderId.MbedCrypto);
    }

    [Fact]
    public async Task PreparingAnAttestationNeedsADeviceThatCanSpeakForAKey()
    {
        var provider = await SkipUnlessAProviderCanAttestAsync();

        var preparation = await service.CreateAttestationOperations(provider)
            .PrepareActivateCredentialAsync(
                "attested",
                "attesting",
                TestContext.Current.CancellationToken);

        Assert.False(preparation.Name.IsEmpty);
    }

    [Fact]
    public async Task AttestingAKeyNeedsADeviceThatCanSpeakForIt()
    {
        var provider = await SkipUnlessAProviderCanAttestAsync();

        var credential = await service.CreateAttestationOperations(provider)
            .AttestKeyWithActivateCredentialAsync(
                "attested",
                "attesting",
                default,
                default,
                TestContext.Current.CancellationToken);

        Assert.NotEmpty(credential);
    }

    [Fact]
    public async Task TheSoftwareProviderSaysItCannotAttest()
    {
        // The software provider is the one the image runs, so this is the only attestation answer
        // these tests can actually see. Recording it is what tells an application what to expect
        // before it goes looking for hardware.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var fault = await Assert.ThrowsAnyAsync<ParsecServiceException>(
            () => service.CreateAttestationOperations(ProviderId.MbedCrypto)
                .PrepareActivateCredentialAsync(
                    "attested",
                    "attesting",
                    TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorNotSupported, fault.Status);
    }

    /// <summary>
    /// Finds a provider that attests, and skips the test when the service runs none.
    /// </summary>
    /// <returns>The provider to ask.</returns>
    private async Task<ProviderId> SkipUnlessAProviderCanAttestAsync()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var providers = await service.CreateOperations().ListProvidersAsync(
            TestContext.Current.CancellationToken);

        if (providers.FirstOrDefault(p => p.Id is ProviderId.Tpm) is { } tpm)
        {
            return tpm.Id;
        }

        Assert.Skip(
            "Attestation needs a device that can speak for a key. This service runs the software "
            + "provider only, so there is nothing to attest with.");

        return ProviderId.Core;
    }
}
