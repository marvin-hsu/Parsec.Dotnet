using System.Net.Sockets;
using Google.Protobuf;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// A Unix domain socket listener that answers the two requests a client is built from.
/// </summary>
/// <remarks>
/// <see cref="ParsecClient.CreateAsync"/> opens a real socket, so a scripted transport cannot
/// reach it. This listener accepts one connection per request, the way the service does, answers
/// Ping and ListProviders, and refuses everything else so that a test which sends something
/// unexpected fails rather than hanging.
/// </remarks>
internal sealed class FakeParsecService : IAsyncDisposable
{
    private readonly UnixSocketServer _server;
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<Opcode> _received = [];
    private readonly List<ProviderId> _receivedProviders = [];

    // net8.0 has no System.Threading.Lock, and this project still targets it.
    private readonly object _gate = new();
    private readonly Task _loop;
    private readonly byte[] _providersBody;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeParsecService"/> class and starts
    /// listening.
    /// </summary>
    /// <param name="providers">The providers to report from ListProviders.</param>
    public FakeParsecService(params ListProviders.ProviderInfo[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var result = new ListProviders.Result();
        result.Providers.AddRange(providers);
        _providersBody = result.ToByteArray();

        _server = new UnixSocketServer();
        _loop = Task.Run(RunAsync);
    }

    /// <summary>Gets the address of the listener.</summary>
    public Uri Endpoint => _server.Endpoint;

    /// <summary>Gets the operations that reached the listener, in order.</summary>
    public IReadOnlyList<Opcode> Received
    {
        get
        {
            lock (_gate)
            {
                return [.. _received];
            }
        }
    }

    /// <summary>Gets the provider that each request named, in order.</summary>
    public IReadOnlyList<ProviderId> ReceivedProviders
    {
        get
        {
            lock (_gate)
            {
                return [.. _receivedProviders];
            }
        }
    }

    /// <summary>
    /// Forgets what has been received so far.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _received.Clear();
            _receivedProviders.Clear();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        await _server.DisposeAsync();

        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // Stopping the listener is how the loop ends.
        }
        catch (SocketException)
        {
            // The accept fails once the listener is closed, which is the same thing.
        }
        catch (ObjectDisposedException)
        {
            // Likewise.
        }

        _stopping.Dispose();
    }

    private static async Task ReadExactlyAsync(Socket socket, byte[] buffer)
    {
        var read = 0;

        while (read < buffer.Length)
        {
            var got = await socket.ReceiveAsync(buffer.AsMemory(read), SocketFlags.None);

            if (got == 0)
            {
                return;
            }

            read += got;
        }
    }

    private async Task RunAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            using var connection = await _server.AcceptAsync();

            var header = new byte[WireHeader.Size];
            await ReadExactlyAsync(connection, header);

            if (!WireHeader.TryParse(header, out var parsed, out _))
            {
                return;
            }

            await ReadExactlyAsync(connection, new byte[parsed.AuthLength + parsed.ContentLength]);

            lock (_gate)
            {
                _received.Add(parsed.Opcode);
                _receivedProviders.Add(parsed.Provider);
            }

            var (status, body) = Answer(parsed.Opcode);

            await connection.SendAsync(
                ScriptedTransport.BuildResponseBytes(parsed.Opcode, status, body, parsed.Provider),
                SocketFlags.None);
        }
    }

    private (ResponseStatus Status, byte[] Body) Answer(Opcode opcode) => opcode switch
    {
        Opcode.Ping => (
            ResponseStatus.Success,
            new Ping.Result { WireProtocolVersionMaj = 1, WireProtocolVersionMin = 0 }.ToByteArray()),
        Opcode.ListProviders => (ResponseStatus.Success, _providersBody),
        _ => (ResponseStatus.PsaErrorNotSupported, []),
    };
}
