using Google.Protobuf;
using Parsec.Client.Attestation;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Operations;

/// <summary>
/// The operations that prove a key was created inside the device rather than brought to it.
/// </summary>
/// <remarks>
/// Only a provider backed by a TPM offers these. The Mbed Crypto provider that the image of
/// <c>Parsec.Testcontainers</c> carries is software, so it has no device to speak for a key and
/// the service answers <see cref="ResponseStatus.PsaErrorNotSupported"/>. The wire code is here
/// so that an application running against a TPM can use it, and the tests that need the hardware
/// say so rather than pretending to pass.
/// <para>
/// The specification defines one mechanism, activate credential, and the two methods name it. A
/// second mechanism would get its own pair rather than a discriminator on these, because the
/// blobs each mechanism carries have nothing in common.
/// </para>
/// </remarks>
/// <param name="transport">The transport that reaches the service.</param>
/// <param name="authentication">The authentication that the application chose.</param>
/// <param name="provider">The provider that holds the keys.</param>
internal sealed class ParsecAttestationOperations(
    IParsecTransport transport,
    IParsecAuthentication authentication,
    ProviderId provider)
{
    private const string OutputField = "attestation output";

    private readonly ParsecOperationClient _client =
        new(transport ?? throw new ArgumentNullException(nameof(transport)));

    private readonly IParsecAuthentication _authentication =
        authentication ?? throw new ArgumentNullException(nameof(authentication));

    /// <summary>
    /// Asks the device for what a challenger needs to build a credential.
    /// </summary>
    /// <param name="attestedKeyName">The name of the key whose provenance is in question.</param>
    /// <param name="attestingKeyName">The name of the key that speaks for the device.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The three blobs that the challenger needs.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="attestedKeyName"/> or <paramref name="attestingKeyName"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">
    /// The provider refused. <see cref="ResponseStatus.PsaErrorNotSupported"/> means it has no
    /// device to speak for a key.
    /// </exception>
    /// <exception cref="ParsecProtocolException">The answer names no mechanism.</exception>
    public async Task<ActivateCredentialPreparation> PrepareActivateCredentialAsync(
        string attestedKeyName,
        string attestingKeyName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attestedKeyName);
        ArgumentNullException.ThrowIfNull(attestingKeyName);

        var operation = new PrepareKeyAttestation.Operation
        {
            Parameters = new PrepareKeyAttestation.PrepareKeyAttestationParams
            {
                ActivateCredential = new PrepareKeyAttestation.PrepareKeyAttestationParams.Types.ActivateCredential
                {
                    AttestedKeyName = attestedKeyName,
                    AttestingKeyName = attestingKeyName,
                },
            },
        };

        var result = await _client.ExecuteAsync(
            Opcode.PrepareKeyAttestation,
            provider,
            _authentication,
            operation,
            PrepareKeyAttestation.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        var output = result.Output;

        if (output is null
            || output.MechanismCase != PrepareKeyAttestation.PrepareKeyAttestationOutput.MechanismOneofCase.ActivateCredential)
        {
            throw ParsecProtocolException.UnreadableField(
                Opcode.PrepareKeyAttestation,
                OutputField,
                "the message names no mechanism this client knows");
        }

        return new ActivateCredentialPreparation(
            output.ActivateCredential.Name.Memory,
            output.ActivateCredential.Public.Memory,
            output.ActivateCredential.AttestingKeyPub.Memory);
    }

    /// <summary>
    /// Asks the device to unwrap a credential that a challenger built.
    /// </summary>
    /// <param name="attestedKeyName">The name of the key whose provenance is in question.</param>
    /// <param name="attestingKeyName">The name of the key that speaks for the device.</param>
    /// <param name="credentialBlob">The wrapped credential from the challenger.</param>
    /// <param name="secret">The secret that goes with the credential.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// The unwrapped credential. Handing this back to the challenger is the proof: only a device
    /// holding both keys could have produced it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="attestedKeyName"/> or <paramref name="attestingKeyName"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ParsecServiceException">The provider refused.</exception>
    /// <exception cref="ParsecProtocolException">The answer names no mechanism.</exception>
    public async Task<byte[]> AttestKeyWithActivateCredentialAsync(
        string attestedKeyName,
        string attestingKeyName,
        ReadOnlyMemory<byte> credentialBlob,
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attestedKeyName);
        ArgumentNullException.ThrowIfNull(attestingKeyName);

        var operation = new AttestKey.Operation
        {
            AttestedKeyName = attestedKeyName,
            AttestingKeyName = attestingKeyName,
            Parameters = new AttestKey.AttestationMechanismParams
            {
                ActivateCredential = new AttestKey.AttestationMechanismParams.Types.ActivateCredential
                {
                    CredentialBlob = UnsafeByteOperations.UnsafeWrap(credentialBlob),
                    Secret = UnsafeByteOperations.UnsafeWrap(secret),
                },
            },
        };

        var result = await _client.ExecuteAsync(
            Opcode.AttestKey,
            provider,
            _authentication,
            operation,
            AttestKey.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        var output = result.Output;

        return output is not null
            && output.MechanismCase == AttestKey.AttestationOutput.MechanismOneofCase.ActivateCredential
            ? output.ActivateCredential.Credential.ToByteArray()
            : throw ParsecProtocolException.UnreadableField(
                Opcode.AttestKey,
                OutputField,
                "the message names no mechanism this client knows");
    }
}
