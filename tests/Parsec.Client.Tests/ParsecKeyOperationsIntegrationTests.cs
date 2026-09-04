using System.Security.Cryptography;
using Parsec.Client.Algorithms;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Runs the key management operations against the real Parsec service.
/// </summary>
/// <remarks>
/// These prove the key code against the software provider that the image carries. Every test
/// gives its key a name of its own and removes it afterwards, because the keys of one
/// application live in one namespace and the service keeps them between tests.
/// </remarks>
/// <param name="service">The service that the fixture started.</param>
[Trait("Category", "IntegrationTests")]
[Collection(nameof(SocketTestGroup))]
public sealed class ParsecKeyOperationsIntegrationTests(ParsecServiceFixture service)
    : IClassFixture<ParsecServiceFixture>
{
    [Fact]
    public async Task AnRsaKeyIsCreatedListedExportedAndRemoved()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var keys = service.CreateKeyOperations();
        var name = UniqueName();
        var attributes = KeyAttributes.RsaSigningKey();

        await keys.GenerateKeyAsync(name, attributes, TestContext.Current.CancellationToken);

        try
        {
            var listed = await service.CreateOperations().ListKeysAsync(TestContext.Current.CancellationToken);
            var mine = Assert.Single(listed, key => key.Name == name);

            Assert.Equal(ProviderId.MbedCrypto, mine.Provider);
            Assert.Equal(attributes.Type, mine.Attributes.Type);
            Assert.Equal(attributes.Bits, mine.Attributes.Bits);
            Assert.Equal(attributes.Policy.Algorithm, mine.Attributes.Policy.Algorithm);

            // The policy that comes back is wider than the one that went in. Mbed Crypto adds
            // SignMessage next to SignHash and VerifyMessage next to VerifyHash, because a key
            // that may sign a hash may sign a message it hashes itself. So the check is that
            // every permission asked for is present, not that the two sets are equal.
            Assert.Equal(attributes.Policy.Usage, mine.Attributes.Policy.Usage & attributes.Policy.Usage);
            Assert.True(mine.Attributes.Policy.Usage.HasFlag(KeyUsages.SignMessage));
            Assert.False(mine.Attributes.Policy.Usage.HasFlag(KeyUsages.Export));

            // The public half comes back as a DER SubjectPublicKeyInfo or RSAPublicKey. Reading
            // it with the platform is what proves the bytes are a real key and not a blob the
            // client mangled on the way through.
            var exported = await keys.ExportPublicKeyAsync(name, TestContext.Current.CancellationToken);

            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(exported, out var read);

            Assert.Equal(exported.Length, read);
            Assert.Equal(2048, rsa.KeySize);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }

        var after = await service.CreateOperations().ListKeysAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(after, key => key.Name == name);
    }

    [Fact]
    public async Task AnEllipticCurveKeyIsCreatedAndExported()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var keys = service.CreateKeyOperations();
        var name = UniqueName();

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.EccSigningKey(),
            TestContext.Current.CancellationToken);

        try
        {
            var exported = await keys.ExportPublicKeyAsync(name, TestContext.Current.CancellationToken);

            // An uncompressed point on a 256 bit curve is one marker byte and two 32 byte halves.
            Assert.Equal(65, exported.Length);
            Assert.Equal(0x04, exported[0]);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ANameThatIsAlreadyTakenIsRefused()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var keys = service.CreateKeyOperations();
        var name = UniqueName();

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(),
            TestContext.Current.CancellationToken);

        try
        {
            var fault = await Assert.ThrowsAsync<ParsecPsaException>(() => keys.GenerateKeyAsync(
                name,
                KeyAttributes.RsaSigningKey(),
                TestContext.Current.CancellationToken));

            Assert.Equal(ResponseStatus.PsaErrorAlreadyExists, fault.Status);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task RemovingAKeyThatIsNotThereIsRefused()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var keys = service.CreateKeyOperations();

        var fault = await Assert.ThrowsAsync<ParsecPsaException>(
            () => keys.DestroyKeyAsync(UniqueName(), TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorDoesNotExist, fault.Status);
    }

    [Fact]
    public async Task AKeyWithNoExportPermissionRefusesToLeave()
    {
        // This is the check that matters most in this file. A provider that hands out a private
        // key without the permission has lost the property the whole service exists to keep.
        service.SkipWhenTheServiceDoesNotRun();

        var keys = service.CreateKeyOperations();
        var name = UniqueName();

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(),
            TestContext.Current.CancellationToken);

        try
        {
            var fault = await Assert.ThrowsAsync<ParsecPsaException>(
                () => keys.ExportKeyAsync(name, TestContext.Current.CancellationToken));

            Assert.Equal(ResponseStatus.PsaErrorNotPermitted, fault.Status);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AKeyWithExportPermissionComesBackAndMatchesItsPublicHalf()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var keys = service.CreateKeyOperations();
        var name = UniqueName();

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(exportable: true),
            TestContext.Current.CancellationToken);

        try
        {
            var privateKey = await keys.ExportKeyAsync(name, TestContext.Current.CancellationToken);
            var publicKey = await keys.ExportPublicKeyAsync(name, TestContext.Current.CancellationToken);

            using var exported = RSA.Create();
            exported.ImportRSAPrivateKey(privateKey, out _);

            // The two exports must describe one key. Comparing the public half of the private
            // export against the public export is what shows the client did not cross them.
            Assert.Equal(publicKey, exported.ExportRSAPublicKey());
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AnImportedKeyComesBackAsTheKeyThatWentIn()
    {
        service.SkipWhenTheServiceDoesNotRun();

        var keys = service.CreateKeyOperations();
        var name = UniqueName();

        using var source = RSA.Create(2048);
        var material = source.ExportRSAPrivateKey();

        await keys.ImportKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(),
            material,
            TestContext.Current.CancellationToken);

        try
        {
            var exported = await keys.ExportPublicKeyAsync(name, TestContext.Current.CancellationToken);

            Assert.Equal(source.ExportRSAPublicKey(), exported);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Makes a key name that no other test uses.
    /// </summary>
    /// <returns>A name that is unique inside the application of the fixture.</returns>
    private static string UniqueName() => $"test-{Guid.NewGuid():N}";
}
