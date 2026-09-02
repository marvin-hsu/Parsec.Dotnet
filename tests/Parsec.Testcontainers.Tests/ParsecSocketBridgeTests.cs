using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Parsec.Testcontainers.Tests;

// These tests need no Docker endpoint. They put the bridge between a Unix socket and a TCP
// server of this test process, which is the shape that a container gives it.
public sealed class ParsecSocketBridgeTests
{
    /// <summary>
    /// The time that a test waits for an event of the bridge.
    /// </summary>
    private static readonly TimeSpan _shortWait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Start_MakesTheSocketOfTheGivenPath()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            Assert.Equal(directory.SocketPath, bridge.SocketPath);
            Assert.True(File.Exists(bridge.SocketPath));
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public async Task Start_CarriesTheBytesOfBothDirections()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            var answer = await SendAsync(bridge.SocketPath, "a request", TestContext.Current.CancellationToken);

            Assert.Equal("a request", answer);
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public async Task Start_TakesOneConnectionAfterTheOther()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            var first = await SendAsync(bridge.SocketPath, "first", TestContext.Current.CancellationToken);
            var second = await SendAsync(bridge.SocketPath, "second", TestContext.Current.CancellationToken);

            Assert.Equal("first", first);
            Assert.Equal("second", second);
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public async Task DisposeAsync_ClosesTheSocket()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();

        try
        {
            var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            await bridge.DisposeAsync();

            _ = await Assert.ThrowsAnyAsync<SocketException>(
                () => SendAsync(bridge.SocketPath, "a request", TestContext.Current.CancellationToken));
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public async Task Start_WithAPortThatAborts_EndsBothDirectionsOfTheConnection()
    {
        using var server = AbortingServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            await client.ConnectAsync(
                new UnixDomainSocketEndPoint(bridge.SocketPath),
                TestContext.Current.CancellationToken);

            _ = await client.SendAsync(
                "a request"u8.ToArray(),
                SocketFlags.None,
                TestContext.Current.CancellationToken);

            // The port drops the connection. The direction of the request then reads a client
            // that sends nothing more, so the bridge must end that direction as well, and the
            // connection of the client must close before the dispose of the bridge.
            var buffer = new byte[64];
            var receive = client
                .ReceiveAsync(buffer, SocketFlags.None, TestContext.Current.CancellationToken)
                .AsTask();

            var first = await Task.WhenAny(
                receive,
                Task.Delay(_shortWait, TestContext.Current.CancellationToken));

            Assert.Same(receive, first);

            try
            {
                Assert.Equal(0, await receive);
            }
            catch (SocketException)
            {
                // The reset of the port can also come to the client as an error.
            }
        }
        finally
        {
            directory.Remove();
        }
    }

    /// <summary>
    /// Sends text over a Unix socket and reads the answer.
    /// </summary>
    /// <param name="socketPath">The path of the socket.</param>
    /// <param name="text">The text to send.</param>
    /// <param name="cancellationToken">A token to cancel the wait for the answer.</param>
    /// <returns>The text that came back.</returns>
    private static async Task<string> SendAsync(string socketPath, string text, CancellationToken cancellationToken)
    {
        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);

        _ = await client.SendAsync(Encoding.UTF8.GetBytes(text), SocketFlags.None, cancellationToken);
        client.Shutdown(SocketShutdown.Send);

        var buffer = new byte[64];
        var read = await client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    /// <summary>
    /// A TCP server that sends back every byte that it reads. It stands for socat in the
    /// container.
    /// </summary>
    private sealed class EchoServer : IDisposable
    {
        private readonly Socket _listener;
        private readonly CancellationTokenSource _stop = new();

        private EchoServer(Socket listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            _ = AcceptAsync();
        }

        /// <summary>
        /// Gets the port that the server listens on.
        /// </summary>
        internal int Port { get; }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Dispose();
            _stop.Dispose();
        }

        /// <summary>
        /// Starts a server on a free port of the loopback address.
        /// </summary>
        /// <returns>A server that listens.</returns>
        internal static EchoServer Start()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(16);

            return new EchoServer(listener);
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptAsync(_stop.Token);

                    _ = EchoAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        private async Task EchoAsync(Socket client)
        {
            using (client)
            {
                try
                {
                    var buffer = new byte[64];

                    while (true)
                    {
                        var read = await client.ReceiveAsync(buffer, SocketFlags.None, _stop.Token);

                        if (read == 0)
                        {
                            break;
                        }

                        _ = await client.SendAsync(buffer.AsMemory(0, read), SocketFlags.None, _stop.Token);
                    }

                    client.Shutdown(SocketShutdown.Send);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (SocketException)
                {
                }
            }
        }
    }

    /// <summary>
    /// A TCP server that reads one request and then drops the connection with a reset. It stands
    /// for a socat in a container that goes away.
    /// </summary>
    private sealed class AbortingServer : IDisposable
    {
        private readonly Socket _listener;
        private readonly CancellationTokenSource _stop = new();

        private AbortingServer(Socket listener)
        {
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndPoint!).Port;
            _ = AcceptAsync();
        }

        /// <summary>
        /// Gets the port that the server listens on.
        /// </summary>
        internal int Port { get; }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Dispose();
            _stop.Dispose();
        }

        /// <summary>
        /// Starts a server on a free port of the loopback address.
        /// </summary>
        /// <returns>A server that listens.</returns>
        internal static AbortingServer Start()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(16);

            return new AbortingServer(listener);
        }

        private async Task AcceptAsync()
        {
            try
            {
                using var client = await _listener.AcceptAsync(_stop.Token);

                var buffer = new byte[64];
                _ = await client.ReceiveAsync(buffer, SocketFlags.None, _stop.Token);

                // A close with this option sends a reset instead of the end of the connection.
                client.LingerState = new LingerOption(true, 0);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }
    }
}
