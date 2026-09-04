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
    ProviderId provider) : IParsecAttestationOperations
{
    private const string OutputField = "attestation output";

    private readonly ParsecOperationClient _client =
        new(transport ?? throw new ArgumentNullException(nameof(transport)));

    private readonly IParsecAuthentication _authentication =
        authentication ?? throw new ArgumentNullException(nameof(authentication));

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
