using System.Security.Cryptography;
using Parsec.Client.Algorithms;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Runs the signing, hashing and random operations against the real Parsec service.
/// </summary>
/// <remarks>
/// The image carries Mbed Crypto, which offers no message signing: it signs and verifies a hash
/// the caller brings, and nothing else. The tests that cover the message forms therefore prove
/// what the provider answers rather than that the operation works, which is still worth having,
/// because that answer is what an application will meet.
/// </remarks>
/// <param name="service">The service that the fixture started.</param>
[Trait("Category", "IntegrationTests")]
[Collection(nameof(SocketTestGroup))]
public sealed class ParsecCryptoOperationsIntegrationTests(ParsecServiceFixture service)
{
    [Fact]
    public async Task ASignatureOverAHashVerifiesAndATamperedHashDoesNot()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();
        var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(algorithm: algorithm),
            TestContext.Current.CancellationToken);

        try
        {
            var digest = SHA256.HashData("sign me"u8.ToArray());

            var signature = await crypto.SignHashAsync(
                name,
                algorithm,
                digest,
                TestContext.Current.CancellationToken);

            Assert.Equal(256, signature.Length);
            Assert.True(await crypto.VerifyHashAsync(
                name,
                algorithm,
                digest,
                signature,
                TestContext.Current.CancellationToken));

            // A hash that was not the one signed must answer false and raise nothing. This is
            // the case an application gets wrong when the client raises instead.
            var tampered = SHA256.HashData("sign me too"u8.ToArray());

            Assert.False(await crypto.VerifyHashAsync(
                name,
                algorithm,
                tampered,
                signature,
                TestContext.Current.CancellationToken));

            // The same holds for a signature that was tampered with rather than the hash.
            var broken = signature.ToArray();
            broken[^1] ^= 0xFF;

            Assert.False(await crypto.VerifyHashAsync(
                name,
                algorithm,
                digest,
                broken,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ASignatureVerifiesAgainstThePublicKeyOutsideTheService()
    {
        // Verifying with the platform rather than with the service is what shows the signature is
        // a real signature over the hash that went in, and not a value only the service agrees
        // with.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();
        var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(algorithm: algorithm),
            TestContext.Current.CancellationToken);

        try
        {
            var digest = SHA256.HashData("sign me"u8.ToArray());
            var signature = await crypto.SignHashAsync(
                name,
                algorithm,
                digest,
                TestContext.Current.CancellationToken);
            var publicKey = await keys.ExportPublicKeyAsync(name, TestContext.Current.CancellationToken);

            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(publicKey, out _);

            Assert.True(rsa.VerifyHash(
                digest,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1));
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AnEllipticCurveSignatureVerifies()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();
        var algorithm = SignatureAlgorithm.Ecdsa(Hash.Sha256);

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.EccSigningKey(algorithm: algorithm),
            TestContext.Current.CancellationToken);

        try
        {
            var digest = SHA256.HashData("sign me"u8.ToArray());
            var signature = await crypto.SignHashAsync(
                name,
                algorithm,
                digest,
                TestContext.Current.CancellationToken);

            // ECDSA on a 256 bit curve gives two 32 byte halves, and the service hands them over
            // concatenated rather than DER wrapped.
            Assert.Equal(64, signature.Length);
            Assert.True(await crypto.VerifyHashAsync(
                name,
                algorithm,
                digest,
                signature,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TheProviderDoesNotSignAMessage()
    {
        // Recording what the provider answers is the point. An application that reaches for
        // SignMessage against this provider meets this status, and the wire code has to carry it
        // through rather than turn it into something else.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();
        var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(algorithm: algorithm),
            TestContext.Current.CancellationToken);

        try
        {
            var fault = await Assert.ThrowsAsync<ParsecPsaException>(() => crypto.SignMessageAsync(
                name,
                algorithm,
                "sign me"u8.ToArray(),
                TestContext.Current.CancellationToken));

            Assert.Equal(ResponseStatus.PsaErrorNotSupported, fault.Status);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(Hash.Sha256, 32)]
    [InlineData(Hash.Sha384, 48)]
    [InlineData(Hash.Sha512, 64)]
    public async Task TheServiceComputesTheSameHashAsThePlatform(Hash algorithm, int length)
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var input = "hash me"u8.ToArray();

        var computed = await service.CreateCryptoOperations().HashComputeAsync(
            algorithm,
            input,
            TestContext.Current.CancellationToken);

        var expected = algorithm switch
        {
            Hash.Sha256 => SHA256.HashData(input),
            Hash.Sha384 => SHA384.HashData(input),
            _ => SHA512.HashData(input),
        };

        Assert.Equal(length, computed.Length);
        Assert.Equal(expected, computed);
    }

    [Fact]
    public async Task HashCompareAnswersTrueForTheHashAndFalseForAnother()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var crypto = service.CreateCryptoOperations();
        var input = "hash me"u8.ToArray();
        var digest = SHA256.HashData(input);

        Assert.True(await crypto.HashCompareAsync(
            Hash.Sha256,
            input,
            digest,
            TestContext.Current.CancellationToken));

        var other = SHA256.HashData("hash me too"u8.ToArray());

        Assert.False(await crypto.HashCompareAsync(
            Hash.Sha256,
            input,
            other,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(64)]
    public async Task RandomBytesComeBackAtTheLengthAskedFor(int length)
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var bytes = await service.CreateCryptoOperations().GenerateRandomAsync(
            length,
            TestContext.Current.CancellationToken);

        Assert.Equal(length, bytes.Length);
    }

    [Fact]
    public async Task TwoDrawsOfRandomBytesDiffer()
    {
        // This is not a test of the generator. It catches a client that hands back a buffer it
        // reused, or a service that answers with a constant, both of which would be silent.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var crypto = service.CreateCryptoOperations();

        var first = await crypto.GenerateRandomAsync(32, TestContext.Current.CancellationToken);
        var second = await crypto.GenerateRandomAsync(32, TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Makes a key name that no other test uses.
    /// </summary>
    /// <returns>A name that is unique inside the application of the fixture.</returns>
    private static string UniqueName() => $"test-{Guid.NewGuid():N}";
}
