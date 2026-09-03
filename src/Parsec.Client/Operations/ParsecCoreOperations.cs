using Parsec.Client.Transport;

namespace Parsec.Client.Operations;

/// <summary>
/// Runs the operations of the core provider.
/// </summary>
/// <remarks>
/// <para>
/// The core provider reports the state of the service: the version of the wire protocol, the
/// providers, the operations of a provider, the authenticators and the keys of the application.
/// A client asks these questions first, because the answers tell it which provider and which
/// operations it can use.
/// </para>
/// <para>
/// Every method turns the protobuf answer into the public model of the library. A value that
/// does not fit the wire field it belongs to raises <see cref="ParsecProtocolException"/>,
/// because such an answer cannot be sent back to the service in a later request. A value that
/// fits but that this version of the client does not name is kept as it came, and the
/// <c>IsKnown</c> method of its type answers <see langword="false"/>.
/// </para>
/// </remarks>
/// <param name="transport">Opens the connections to the service.</param>
/// <param name="authentication">The authentication that the application chose.</param>
internal sealed class ParsecCoreOperations(IParsecTransport transport, IParsecAuthentication authentication)
{
    private readonly ParsecOperationClient _client =
        new(transport ?? throw new ArgumentNullException(nameof(transport)));

    private readonly IParsecAuthentication _authentication =
        authentication ?? throw new ArgumentNullException(nameof(authentication));

    /// <summary>
    /// Gets the highest version of the wire protocol that the service reported, or
    /// <see langword="null"/> while no <see cref="PingAsync"/> answered.
    /// </summary>
    /// <remarks>
    /// The client sends version 1.0 and the service answers with the highest version that it
    /// supports. The client keeps sending 1.0, because that is the version it implements. The
    /// recorded value tells the application what the service could do.
    /// </remarks>
    public Version? NegotiatedWireProtocolVersion { get; private set; }

    /// <summary>
    /// Asks the service for the highest version of the wire protocol that it supports.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The version that the service reported.</returns>
    /// <remarks>
    /// The request carries no authentication. Ping needs no identity, and an application calls it
    /// to find the service before it knows which authenticator the service runs.
    /// </remarks>
    public async Task<Version> PingAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.ExecuteAsync(
            Opcode.Ping,
            ProviderId.Core,
            NoAuthentication.Instance,
            new Ping.Operation(),
            Ping.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        // The service sends both numbers as 32-bit values, but each one travels in a single byte
        // of the header of every other message.
        var version = new Version(
            ToWireByte(Opcode.Ping, "wire protocol major version", result.WireProtocolVersionMaj),
            ToWireByte(Opcode.Ping, "wire protocol minor version", result.WireProtocolVersionMin));

        NegotiatedWireProtocolVersion = version;
        return version;
    }

    /// <summary>
    /// Asks the service which providers it runs.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// The providers, in the order of the answer. The service puts the core provider last.
    /// </returns>
    public async Task<IReadOnlyList<ProviderInfo>> ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.ExecuteAsync(
            Opcode.ListProviders,
            ProviderId.Core,
            _authentication,
            new ListProviders.Operation(),
            ListProviders.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        var providers = new List<ProviderInfo>(result.Providers.Count);

        foreach (var provider in result.Providers)
        {
            providers.Add(new ProviderInfo(
                (ProviderId)ToWireByte(Opcode.ListProviders, "provider identifier", provider.Id),
                provider.Uuid,
                provider.Description,
                provider.Vendor,
                ToVersion(
                    Opcode.ListProviders,
                    provider.VersionMaj,
                    provider.VersionMin,
                    provider.VersionRev)));
        }

        return providers;
    }

    /// <summary>
    /// Asks the service which operations a provider runs.
    /// </summary>
    /// <param name="provider">The provider to ask about.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The operations of the provider.</returns>
    /// <remarks>
    /// An operation that this version of the client does not name stays in the set. Its
    /// <see cref="OpcodeExtensions.IsKnown"/> answers <see langword="false"/>.
    /// </remarks>
    public async Task<IReadOnlySet<Opcode>> ListOpcodesAsync(
        ProviderId provider,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.ExecuteAsync(
            Opcode.ListOpcodes,
            ProviderId.Core,
            _authentication,
            new ListOpcodes.Operation { ProviderId = (uint)provider },
            ListOpcodes.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        var opcodes = new HashSet<Opcode>(result.Opcodes.Count);

        foreach (var opcode in result.Opcodes)
        {
            // The opcode field of the header holds four bytes, so every value of the answer fits.
            opcodes.Add((Opcode)opcode);
        }

        return opcodes;
    }

    /// <summary>
    /// Asks the service which authenticators it runs.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The authenticators, in the order of the answer.</returns>
    public async Task<IReadOnlyList<AuthenticatorInfo>> ListAuthenticatorsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _client.ExecuteAsync(
            Opcode.ListAuthenticators,
            ProviderId.Core,
            _authentication,
            new ListAuthenticators.Operation(),
            ListAuthenticators.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        var authenticators = new List<AuthenticatorInfo>(result.Authenticators.Count);

        foreach (var authenticator in result.Authenticators)
        {
            authenticators.Add(new AuthenticatorInfo(
                (AuthType)ToWireByte(Opcode.ListAuthenticators, "authenticator identifier", authenticator.Id),
                authenticator.Description,
                ToVersion(
                    Opcode.ListAuthenticators,
                    authenticator.VersionMaj,
                    authenticator.VersionMin,
                    authenticator.VersionRev)));
        }

        return authenticators;
    }

    /// <summary>
    /// Asks the service for the keys of the application.
    /// </summary>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The keys of every provider, in the order of the answer.</returns>
    /// <remarks>
    /// The service reports the keys of the application that authenticated the request. It answers
    /// <see cref="ResponseStatus.NotAuthenticated"/> when the request carries no authentication,
    /// so this operation needs an authentication that names the application.
    /// </remarks>
    public async Task<IReadOnlyList<KeyInfo>> ListKeysAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.ExecuteAsync(
            Opcode.ListKeys,
            ProviderId.Core,
            _authentication,
            new ListKeys.Operation(),
            ListKeys.Result.Parser,
            cancellationToken).ConfigureAwait(false);

        var keys = new List<KeyInfo>(result.Keys.Count);

        foreach (var key in result.Keys)
        {
            keys.Add(new KeyInfo(
                (ProviderId)ToWireByte(Opcode.ListKeys, "provider identifier", key.ProviderId),
                key.Name));
        }

        return keys;
    }

    /// <summary>
    /// Asks a provider whether it can work with a set of key attributes.
    /// </summary>
    /// <param name="provider">The provider to ask.</param>
    /// <param name="checkType">The use that the question is about.</param>
    /// <param name="attributes">The attributes of the key.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>
    /// <see langword="true"/> when the provider accepts the attributes, and
    /// <see langword="false"/> when it does not support them.
    /// </returns>
    /// <remarks>
    /// A provider that does not support the attributes answers
    /// <see cref="ResponseStatus.PsaErrorNotSupported"/>. That answer is the normal way to say
    /// no, so it becomes <see langword="false"/> and not an exception. Every other failed status
    /// raises <see cref="ParsecServiceException"/>.
    /// </remarks>
    public async Task<bool> CanDoCryptoAsync(
        ProviderId provider,
        CanDoCrypto.CheckType checkType,
        PsaKeyAttributes.KeyAttributes attributes,
        CancellationToken cancellationToken = default)
    {
        var operation = new CanDoCrypto.Operation
        {
            CheckType = checkType,
            Attributes = attributes,
        };

        var response = await _client.ExchangeAsync(
            Opcode.CanDoCrypto,
            provider,
            _authentication,
            operation,
            cancellationToken).ConfigureAwait(false);

        if (response.Header.Status == ResponseStatus.PsaErrorNotSupported)
        {
            return false;
        }

        ParsecOperationClient.ThrowIfFailed(Opcode.CanDoCrypto, response);

        return true;
    }

    /// <summary>
    /// Builds a version from the three numbers that the service reports.
    /// </summary>
    /// <param name="operation">The operation that answered.</param>
    /// <param name="major">The major number.</param>
    /// <param name="minor">The minor number.</param>
    /// <param name="revision">The revision number.</param>
    /// <returns>The version, with the revision as the build part.</returns>
    /// <exception cref="ParsecProtocolException">A number is above <see cref="int.MaxValue"/>.</exception>
    private static Version ToVersion(Opcode operation, uint major, uint minor, uint revision) => new(
        ToVersionPart(operation, major),
        ToVersionPart(operation, minor),
        ToVersionPart(operation, revision));

    /// <summary>
    /// Turns one number of a version into the type that <see cref="Version"/> takes.
    /// </summary>
    /// <param name="operation">The operation that answered.</param>
    /// <param name="value">The number that the service reported.</param>
    /// <returns>The same number.</returns>
    /// <exception cref="ParsecProtocolException">The number is above <see cref="int.MaxValue"/>.</exception>
    private static int ToVersionPart(Opcode operation, uint value) => value <= int.MaxValue
        ? (int)value
        : throw ParsecProtocolException.OutOfRangeField(operation, "version number", value, int.MaxValue);

    /// <summary>
    /// Turns a number of an answer into the single byte that the wire field of it holds.
    /// </summary>
    /// <param name="operation">The operation that answered.</param>
    /// <param name="field">The name of the field, for the message of the exception.</param>
    /// <param name="value">The number that the service reported.</param>
    /// <returns>The same number as one byte.</returns>
    /// <exception cref="ParsecProtocolException">The number does not fit in one byte.</exception>
    private static byte ToWireByte(Opcode operation, string field, uint value) => value <= byte.MaxValue
        ? (byte)value
        : throw ParsecProtocolException.OutOfRangeField(operation, field, value, byte.MaxValue);
}
