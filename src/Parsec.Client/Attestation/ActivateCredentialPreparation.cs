namespace Parsec.Client.Attestation;

/// <summary>
/// What a challenger needs before it can build a credential for the activate credential
/// mechanism.
/// </summary>
/// <remarks>
/// The three blobs go to whoever is checking the key. That party wraps a secret so that only a
/// device holding the attesting key can unwrap it, and hands the result back to
/// <c>AttestKeyWithActivateCredentialAsync</c>. Getting the secret out again is the proof that
/// the attested key lives in the same device as the attesting key.
/// </remarks>
/// <param name="name">The name of the attested key as the device computes it.</param>
/// <param name="publicKey">The public area of the attested key.</param>
/// <param name="attestingKeyPublicKey">The public area of the attesting key.</param>
public sealed class ActivateCredentialPreparation(
    ReadOnlyMemory<byte> name,
    ReadOnlyMemory<byte> publicKey,
    ReadOnlyMemory<byte> attestingKeyPublicKey)
{
    /// <summary>Gets the name of the attested key as the device computes it.</summary>
    public ReadOnlyMemory<byte> Name { get; } = name;

    /// <summary>Gets the public area of the attested key.</summary>
    public ReadOnlyMemory<byte> PublicKey { get; } = publicKey;

    /// <summary>Gets the public area of the attesting key.</summary>
    public ReadOnlyMemory<byte> AttestingKeyPublicKey { get; } = attestingKeyPublicKey;
}
