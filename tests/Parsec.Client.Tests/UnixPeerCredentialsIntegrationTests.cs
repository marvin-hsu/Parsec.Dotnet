using DotNet.Testcontainers.Configurations;
using Parsec.Client.Authentication;
using Parsec.Client.Keys;
using Parsec.Client.Protocol;
using Parsec.Testcontainers;

namespace Parsec.Client.Tests;

/// <summary>
/// Proves that the user identifier this client sends actually authenticates against the service.
/// </summary>
/// <remarks>
/// Every other test of <see cref="UnixPeerCredentialsAuthentication"/> checks the bytes it
/// writes. None of them checks that a service accepts them, and the two are different claims:
/// the byte layout could be right and the identity still be rejected, or read as somebody else.
/// <para>
/// This runs only on Linux, and only where the socket is bind mounted rather than bridged.
/// Peer credentials come from the kernel, so anything that forwards the connection makes the
/// service see the credentials of the forwarder. On macOS and Windows the module bridges, and
/// there is no arrangement of this test that would work there.
/// </para>
/// <para>
/// It is also the first real use of <see cref="ParsecBuilder.WithConfigFile"/>: the image ships
/// with Direct authentication, and nothing short of replacing the configuration will change
/// that.
/// </para>
/// </remarks>
[Trait("Category", "IntegrationTests")]
[Collection(nameof(SocketTestGroup))]
public sealed class UnixPeerCredentialsIntegrationTests : IAsyncLifetime
{
    private const string Configuration = """
        # Parsec configuration for the peer credentials integration test.
        #
        # The same shape as the one in the image, with the authenticator swapped. See
        # docker/parsec/config.toml for what each section is for.

        [core_settings]
        log_level = "info"
        allow_root = true

        [listener]
        listener_type = "DomainSocket"
        timeout = 200
        socket_path = "/run/parsec/parsec.sock"

        [authenticator]
        auth_type = "UnixPeerCredentials"

        [[key_manager]]
        name = "sqlite-manager"
        manager_type = "SQLite"
        sqlite_db_path = "/var/lib/parsec/kim.sqlite3"

        [[provider]]
        name = "mbed-crypto-provider"
        provider_type = "MbedCrypto"
        key_info_manager = "sqlite-manager"

        """;

    private string? _configPath;
    private ParsecContainer? _container;
    private string? _skipReason;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            _skipReason =
                "Peer credentials come from the kernel, and this host reaches the service through "
                + "a bridge that would supply its own.";
            return;
        }

        if (TestcontainersSettings.OS.DockerEndpointAuthConfig is null)
        {
            _skipReason = "Testcontainers found no Docker endpoint on this machine.";
            return;
        }

        _configPath = Path.Combine(Path.GetTempPath(), $"parsec-peer-{Guid.NewGuid():N}.toml");

        await File.WriteAllTextAsync(
            _configPath,
            Configuration,
            TestContext.Current.CancellationToken);

        _container = new ParsecBuilder().WithConfigFile(_configPath).Build();

        await _container.StartAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        if (_configPath is not null && File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }

    [Fact]
    public async Task TheUserIdOfThisProcessOwnsTheKeysItCreates()
    {
        SkipWhenThisHostCannotCarryPeerCredentials();

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions
            {
                Endpoint = _container!.Endpoint,
                Authentication = new UnixPeerCredentialsAuthentication(),
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(ProviderId.MbedCrypto, client.Provider);

        var name = $"peer-{Guid.NewGuid():N}";

        // Creating a key is the operation that needs an identity. If the service had rejected
        // the credentials this would answer NotAuthenticated rather than succeeding.
        await client.Keys.GenerateKeyAsync(
            name,
            KeyAttributes.RsaSigningKey(),
            TestContext.Current.CancellationToken);

        try
        {
            var keys = await client.ListKeysAsync(TestContext.Current.CancellationToken);

            // And listing it back proves the service put it in a namespace it can find again
            // from the same credentials, rather than accepting the request and losing the key.
            Assert.Contains(keys, key => key.Name == name);
        }
        finally
        {
            await client.Keys.DestroyKeyAsync(name, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task TheServiceReportsTheAuthenticatorThisTestConfigured()
    {
        // If WithConfigFile silently failed to take, the test above would still pass under
        // Direct authentication, because Direct accepts whatever it is sent. This is what says
        // the service really is checking credentials.
        SkipWhenThisHostCannotCarryPeerCredentials();

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions
            {
                Endpoint = _container!.Endpoint,
                Authentication = new UnixPeerCredentialsAuthentication(),
            },
            TestContext.Current.CancellationToken);

        var authenticators = await client.ListAuthenticatorsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(authenticators, authenticator => authenticator.Id == AuthType.UnixPeerCredentials);
        Assert.DoesNotContain(authenticators, authenticator => authenticator.Id == AuthType.Direct);
    }

    [Fact]
    public async Task AnIdentityTheKernelDoesNotBackIsRefusedBeforeTheClientExists()
    {
        // Direct authentication sends a name the service has no way to check. Under a peer
        // credentials authenticator the service refuses it rather than falling back, and it
        // does so at the ListProviders that building a client makes: an application configured
        // with the wrong authentication type cannot get a client at all, which is a better
        // place to find out than the first operation that happens to touch a key.
        SkipWhenThisHostCannotCarryPeerCredentials();

        var fault = await Assert.ThrowsAnyAsync<Errors.ParsecServiceException>(
            () => ParsecClient.CreateAsync(
                new ParsecClientOptions
                {
                    Endpoint = _container!.Endpoint,
                    Authentication = new DirectAuthentication("not-a-user-id"),
                },
                TestContext.Current.CancellationToken));

        Assert.Equal(ResponseStatus.AuthenticatorNotRegistered, fault.Status);
        Assert.Equal(Opcode.ListProviders, fault.Operation);
    }

    [Fact]
    public async Task PingStillAnswersWhateverTheAuthenticatorIs()
    {
        // Ping carries no authentication whatever the client is configured with, which is what
        // lets an application find a service before it knows how to identify itself to it. If
        // that ever changed, the test above would be the only way to reach this service at all.
        SkipWhenThisHostCannotCarryPeerCredentials();

        await using var client = await ParsecClient.CreateAsync(
            new ParsecClientOptions
            {
                Endpoint = _container!.Endpoint,
                Authentication = new UnixPeerCredentialsAuthentication(),
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new Version(1, 0),
            await client.PingAsync(TestContext.Current.CancellationToken));
    }

    private void SkipWhenThisHostCannotCarryPeerCredentials()
    {
        if (_skipReason is { } reason)
        {
            Assert.Skip(reason);
        }
    }
}
