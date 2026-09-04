using System.Security.Cryptography;
using Parsec.Client.Algorithms;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Runs the encryption, code and agreement operations against the real Parsec service.
/// </summary>
/// <remarks>
/// Mbed Crypto offers asymmetric encryption, authenticated encryption and raw key agreement. It
/// offers no cipher and no code operation, and the tests that cover those record what it answers
/// instead, because that answer is what an application will meet.
/// </remarks>
/// <param name="service">The service that the fixture started.</param>
[Trait("Category", "IntegrationTests")]
[Collection(nameof(SocketTestGroup))]
public sealed class ParsecCipherOperationsIntegrationTests(ParsecServiceFixture service)
{
    [Fact]
    public async Task AuthenticatedEncryptionMakesARoundTrip()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.AesKey(),
            TestContext.Current.CancellationToken);

        try
        {
            var nonce = await crypto.GenerateRandomAsync(12, TestContext.Current.CancellationToken);
            var plaintext = "the message"u8.ToArray();
            var aad = "the header"u8.ToArray();

            var ciphertext = await crypto.AeadEncryptAsync(
                name,
                AeadAlgorithm.Gcm,
                nonce,
                aad,
                plaintext,
                TestContext.Current.CancellationToken);

            // Galois/counter mode adds a sixteen byte tag and does not pad, so the ciphertext is
            // the plaintext plus the tag.
            Assert.Equal(plaintext.Length + 16, ciphertext.Length);
            Assert.NotEqual(plaintext, ciphertext.AsSpan(0, plaintext.Length).ToArray());

            var back = await crypto.AeadDecryptAsync(
                name,
                AeadAlgorithm.Gcm,
                nonce,
                aad,
                ciphertext,
                TestContext.Current.CancellationToken);

            Assert.Equal(plaintext, back);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ChangingTheCiphertextOrTheAdditionalDataIsCaught()
    {
        // This is the property the whole mode exists for. If either check went missing the round
        // trip above would still pass.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.AesKey(),
            TestContext.Current.CancellationToken);

        try
        {
            var nonce = await crypto.GenerateRandomAsync(12, TestContext.Current.CancellationToken);
            var aad = "the header"u8.ToArray();
            var ciphertext = await crypto.AeadEncryptAsync(
                name,
                AeadAlgorithm.Gcm,
                nonce,
                aad,
                "the message"u8.ToArray(),
                TestContext.Current.CancellationToken);

            var tampered = ciphertext.ToArray();
            tampered[^1] ^= 0xFF;

            var brokenTag = await Assert.ThrowsAsync<ParsecPsaException>(() => crypto.AeadDecryptAsync(
                name,
                AeadAlgorithm.Gcm,
                nonce,
                aad,
                tampered,
                TestContext.Current.CancellationToken));

            Assert.Equal(ResponseStatus.PsaErrorInvalidSignature, brokenTag.Status);

            var otherAad = await Assert.ThrowsAsync<ParsecPsaException>(() => crypto.AeadDecryptAsync(
                name,
                AeadAlgorithm.Gcm,
                nonce,
                "another header"u8.ToArray(),
                ciphertext,
                TestContext.Current.CancellationToken));

            Assert.Equal(ResponseStatus.PsaErrorInvalidSignature, otherAad.Status);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task AsymmetricEncryptionMakesARoundTripAndTakesWhatThePlatformEncrypted()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();
        var algorithm = EncryptionAlgorithm.RsaOaep(Hash.Sha256);

        await keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaEncryptionKey(algorithm: algorithm),
            TestContext.Current.CancellationToken);

        try
        {
            var plaintext = "the secret"u8.ToArray();

            var ciphertext = await crypto.AsymmetricEncryptAsync(
                name,
                algorithm,
                plaintext,
                default,
                TestContext.Current.CancellationToken);

            Assert.Equal(256, ciphertext.Length);

            var back = await crypto.AsymmetricDecryptAsync(
                name,
                algorithm,
                ciphertext,
                default,
                TestContext.Current.CancellationToken);

            Assert.Equal(plaintext, back);

            // Encrypting with the exported public key and decrypting through the service is what
            // shows the two agree on the padding, which a round trip inside the service cannot.
            var publicKey = await keys.ExportPublicKeyAsync(name, TestContext.Current.CancellationToken);

            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(publicKey, out _);

            var fromPlatform = rsa.Encrypt(plaintext, RSAEncryptionPadding.OaepSHA256);

            var decrypted = await crypto.AsymmetricDecryptAsync(
                name,
                algorithm,
                fromPlatform,
                default,
                TestContext.Current.CancellationToken);

            Assert.Equal(plaintext, decrypted);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TwoSidesAgreeOnTheSameSecret()
    {
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();

        var attributes = new KeyAttributes(
            KeyType.EccKeyPair(EccFamily.SecpR1),
            256,
            new KeyPolicy(KeyUsages.Derive, KeyAgreementAlgorithm.Ecdh));

        await keys.GenerateKeyAsync(name, attributes, TestContext.Current.CancellationToken);

        try
        {
            // The other side is this machine, so the shared secret can be worked out twice and
            // compared. A client that sent the wrong key or the wrong algorithm would not match.
            using var peer = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var peerPoint = peer.PublicKey.ExportParameters().Q;
            var peerKey = new byte[65];
            peerKey[0] = 0x04;
            peerPoint.X.AsSpan().CopyTo(peerKey.AsSpan(1));
            peerPoint.Y.AsSpan().CopyTo(peerKey.AsSpan(33));

            var fromService = await crypto.RawKeyAgreementAsync(
                name,
                KeyAgreementKind.Ecdh,
                peerKey,
                TestContext.Current.CancellationToken);

            Assert.Equal(32, fromService.Length);

            var ours = await keys.ExportPublicKeyAsync(name, TestContext.Current.CancellationToken);

            using var mine = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = ours.AsSpan(1, 32).ToArray(),
                    Y = ours.AsSpan(33, 32).ToArray(),
                },
            });

            var fromPlatform = peer.DeriveRawSecretAgreement(mine.PublicKey);

            Assert.Equal(fromPlatform, fromService);
        }
        finally
        {
            await keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TheProviderOffersNoCipherOperation()
    {
        // Recording what the provider answers is the point, and the answer has a second half
        // worth writing down. A key bound to a cipher mode this provider cannot run is created
        // without complaint, refuses the operation with PsaErrorNotSupported, and then reports
        // the same status when it is removed even though it does go away. A caller that reads a
        // failed destroy as a key still standing would be wrong, and one that leaves the key
        // alone would leak it.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var keys = service.CreateKeyOperations();
        var crypto = service.CreateCryptoOperations();
        var name = UniqueName();

        await keys.GenerateKeyAsync(
            name,
            new KeyAttributes(
                KeyType.Aes,
                256,
                new KeyPolicy(
                    KeyUsages.Encrypt | KeyUsages.Decrypt,
                    Algorithm.FromCipher(Cipher.CbcPkcs7))),
            TestContext.Current.CancellationToken);

        var fault = await Assert.ThrowsAsync<ParsecPsaException>(() => crypto.CipherEncryptAsync(
            name,
            Cipher.CbcPkcs7,
            "the message"u8.ToArray(),
            TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorNotSupported, fault.Status);

        var removal = await Assert.ThrowsAsync<ParsecPsaException>(
            () => keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.PsaErrorNotSupported, removal.Status);

        // The status said no and the key went anyway.
        var listed = await service.CreateOperations().ListKeysAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(listed, key => key.Name == name);
    }

    [Fact]
    public async Task WhatTheProviderReportsIsWhatTheOperationsFindOutTheHardWay()
    {
        // ListOpcodes is the provider saying what it runs. Every integration test in this project
        // that succeeds names an operation on this list, and every one that records a refusal
        // names one that is missing from it. Pinning the list is what keeps those two halves
        // honest: an image with another provider would change the list and fail here first,
        // rather than quietly making a refusal test pass for a new reason.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var supported = await service.CreateOperations().ListOpcodesAsync(
            ProviderId.MbedCrypto,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                Opcode.PsaGenerateKey,
                Opcode.PsaDestroyKey,
                Opcode.PsaSignHash,
                Opcode.PsaVerifyHash,
                Opcode.PsaImportKey,
                Opcode.PsaExportPublicKey,
                Opcode.PsaAsymmetricEncrypt,
                Opcode.PsaAsymmetricDecrypt,
                Opcode.PsaExportKey,
                Opcode.PsaGenerateRandom,
                Opcode.PsaHashCompute,
                Opcode.PsaHashCompare,
                Opcode.PsaAeadEncrypt,
                Opcode.PsaAeadDecrypt,
                Opcode.PsaRawKeyAgreement,
                Opcode.CanDoCrypto,
            ],
            supported.OrderBy(opcode => (uint)opcode));
    }

    [Fact]
    public async Task AnOperationTheServiceNeverImplementedFailsOneLayerEarlier()
    {
        // Not every refusal is the same shape, and the difference matters to a caller deciding
        // whether another provider would help. A cipher request reaches the provider and comes
        // back with a PSA status, so a different provider might run it. A code request never
        // gets that far: the service answers OpcodeDoesNotExist, which is a service status, so
        // no provider on this service will ever run it.
        await service.StartOrSkipAsync(TestContext.Current.CancellationToken);

        var fault = await Assert.ThrowsAsync<ParsecServiceException>(
            () => service.CreateCryptoOperations().MacComputeAsync(
                UniqueName(),
                MacAlgorithm.Hmac(Hash.Sha256),
                "the message"u8.ToArray(),
                TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.OpcodeDoesNotExist, fault.Status);
        Assert.IsNotType<ParsecPsaException>(fault);
    }

    /// <summary>
    /// Makes a key name that no other test uses.
    /// </summary>
    /// <returns>A name that is unique inside the application of the fixture.</returns>
    private static string UniqueName() => $"test-{Guid.NewGuid():N}";
}
