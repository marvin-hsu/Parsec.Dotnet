using Google.Protobuf;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Operations;

/// <summary>
/// The operations that create, remove and read back the keys of one provider.
/// </summary>
/// <remarks>
/// Every one of these goes to a provider rather than to the core, and every one needs an
/// application identity, because a key belongs to the application that created it. A key name is
/// unique inside the pair of the provider and the application.
/// </remarks>
/// <param name="transport">The transport that reaches the service.</param>
/// <param name="authentication">The authentication that the application chose.</param>
/// <param name="provider">The provider that holds the keys.</param>
/// <remarks>
/// Only the key name is guarded here. A null set of attributes reaches
/// <see cref="KeyAttributesCodec.ToWire"/> on the next line, which raises the same exception
/// naming the same parameter, so a second guard would be a check no caller can tell apart. A
/// null name would otherwise be raised by a generated setter that names a protobuf field.
/// </remarks>
internal sealed class ParsecKeyOperations(
    IParsecTransport transport,
    IParsecAuthentication authentication,
    ProviderId provider) : IParsecKeyOperations
{
    private readonly ParsecOperationClient _client =
        new(transport ?? throw new ArgumentNullException(nameof(transport)));

    private readonly IParsecAuthentication _authentication =
        authentication ?? throw new ArgumentNullException(nameof(authentication));

    /// <inheritdoc/>
    public async Task GenerateKeyAsync(
        string name,
        KeyAttributes attributes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaGenerateKey.Operation
        {
            KeyName = name,
            Attributes = KeyAttributesCodec.ToWire(attributes),
        };

        _ = await _client.ExecuteAsync(
            Opcode.PsaGenerateKey,
            provider,
            _authentication,
            operation,
            PsaGenerateKey.Result.Parser,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ImportKeyAsync(
        string name,
        KeyAttributes attributes,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var operation = new PsaImportKey.Operation
        {
            KeyName = name,
            Attributes = KeyAttributesCodec.ToWire(attributes),
            Data = UnsafeByteOperations.UnsafeWrap(data),
        };

        _ = await _client.ExecuteAsync(
            Opcode.PsaImportKey,
            provider,
            _authentication,
            operation,
            PsaImportKey.Result.Parser,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DestroyKeyAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        _ = await _client.ExecuteAsync(
            Opcode.PsaDestroyKey,
            provider,
            _authentication,
            new PsaDestroyKey.Operation { KeyName = name },
            PsaDestroyKey.Result.Parser,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportPublicKeyAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var result = await _client.ExecuteAsync(
            Opcode.PsaExportPublicKey,
            provider,
            _authentication,
            new PsaExportPublicKey.Operation { KeyName = name },
            PsaExportPublicKey.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Data.ToByteArray();
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportKeyAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        var result = await _client.ExecuteAsync(
            Opcode.PsaExportKey,
            provider,
            _authentication,
            new PsaExportKey.Operation { KeyName = name },
            PsaExportKey.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        return result.Data.ToByteArray();
    }
}
