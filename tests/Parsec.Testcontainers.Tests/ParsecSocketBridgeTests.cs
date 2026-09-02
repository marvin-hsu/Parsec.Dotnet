using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
    public async Task Start_CarriesTheBytesOfBothDirections()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            Assert.Equal(directory.SocketPath, bridge.SocketPath);

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
    public async Task Start_WithMoreBytesThanOneBuffer_CarriesEveryByte()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();
        using var wait = ShortWait();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            // An answer of the service, such as the public key of a key or the list of the keys,
            // is longer than one buffer of the copy loop. The read loop then runs more than one
            // time, and one send can move fewer bytes than the read gave. A request of nine
            // bytes never reaches either loop.
            var request = RandomNumberGenerator.GetBytes(200 * 1024);

            var answer = await SendAndReceiveAllAsync(bridge.SocketPath, request, wait.Token);

            Assert.Equal(request, answer);
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public async Task Start_WithAClientThatSendsNothing_EndsTheConnectionToThePort()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();
        using var wait = ShortWait();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            await client.ConnectAsync(new UnixDomainSocketEndPoint(bridge.SocketPath), wait.Token);

            // A client that connects and closes its half of the connection with no byte must
            // give the end of that half to the port. A copy loop that looks at the number of
            // bytes only after the send holds the connection to the port open instead.
            client.Shutdown(SocketShutdown.Send);

            var ended = server.ConnectionEnded;
            var first = await Task.WhenAny(ended, Task.Delay(_shortWait, wait.Token));

            Assert.Same(ended, first);

            var buffer = new byte[64];

            Assert.Equal(0, await client.ReceiveAsync(buffer, SocketFlags.None, wait.Token));
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

    [Fact]
    public async Task Start_WithAServerThatAnswersAfterTheRequest_CarriesTheEndOfTheRequest()
    {
        using var server = LateAnswerServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();
        using var wait = ShortWait();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            // A client of the service can close the half of the request and then wait for the
            // answer, and the option -t of socat in the container holds the connection open for
            // that. The bridge must give the end of the request to the port, or a server that
            // answers only after the end of the request never answers.
            var answer = await SendAsync(bridge.SocketPath, "a request", wait.Token);

            Assert.Equal("a request", answer);
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public async Task DisposeAsync_WithAnOpenConnection_EndsThatConnection()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();
        using var wait = ShortWait();

        try
        {
            var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            await client.ConnectAsync(new UnixDomainSocketEndPoint(bridge.SocketPath), wait.Token);

            var buffer = new byte[64];
            _ = await client.SendAsync("a request"u8.ToArray(), SocketFlags.None, wait.Token);

            // The answer shows that the bridge holds an open connection to the port now.
            Assert.True(await client.ReceiveAsync(buffer, SocketFlags.None, wait.Token) > 0);

            // The dispose must end the open connection instead of waiting for a client that keeps
            // it. The container disposes the bridge while a client can still hold a connection.
            var dispose = bridge.DisposeAsync().AsTask();
            var firstOfTheDispose = await Task.WhenAny(dispose, Task.Delay(_shortWait, wait.Token));

            Assert.Same(dispose, firstOfTheDispose);
            await dispose;

            try
            {
                Assert.Equal(0, await client.ReceiveAsync(buffer, SocketFlags.None, wait.Token));
            }
            catch (SocketException)
            {
                // The close of the socket can also come to the client as an error.
            }
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public async Task Start_WhenTheClientGoesAway_ClosesTheConnectionToThePort()
    {
        using var server = EchoServer.Start();
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();
        using var wait = ShortWait();

        try
        {
            await using var bridge = ParsecSocketBridge.Start(directory.SocketPath, "127.0.0.1", server.Port);

            using (var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await client.ConnectAsync(new UnixDomainSocketEndPoint(bridge.SocketPath), wait.Token);

                var buffer = new byte[64];
                _ = await client.SendAsync("a request"u8.ToArray(), SocketFlags.None, wait.Token);

                Assert.True(await client.ReceiveAsync(buffer, SocketFlags.None, wait.Token) > 0);
            }

            // The client is gone. The bridge must end its connection to the port as well, because
            // a client makes a connection for each request, and a connection that stays open for
            // every request of a test run fills the table of the container.
            var ended = server.ConnectionEnded;
            var first = await Task.WhenAny(ended, Task.Delay(_shortWait, wait.Token));

            Assert.Same(ended, first);
        }
        finally
        {
            directory.Remove();
        }
    }

    /// <summary>
    /// Makes a token source that cancels after <see cref="_shortWait"/>, so a bridge that carries
    /// nothing fails the test instead of holding the test run.
    /// </summary>
    /// <returns>A new token source. The caller disposes it.</returns>
    private static CancellationTokenSource ShortWait()
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        source.CancelAfter(_shortWait);

        return source;
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
    /// Sends bytes over a Unix socket and reads the answer until the other side closes.
    /// </summary>
    /// <param name="socketPath">The path of the socket.</param>
    /// <param name="request">The bytes to send.</param>
    /// <param name="cancellationToken">A token to cancel the wait for the answer.</param>
    /// <returns>The bytes that came back.</returns>
    /// <remarks>
    /// The read runs while the send runs. A request that is longer than the buffer of a socket
    /// gets an answer before the send is done, and a client that only sends would hold both
    /// sides.
    /// </remarks>
    private static async Task<byte[]> SendAndReceiveAllAsync(
        string socketPath,
        byte[] request,
        CancellationToken cancellationToken)
    {
        var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);

            var answer = ReceiveAllAsync(client, cancellationToken);
            var sent = 0;

            while (sent < request.Length)
            {
                sent += await client.SendAsync(
                    request.AsMemory(sent),
                    SocketFlags.None,
                    cancellationToken);
            }

            client.Shutdown(SocketShutdown.Send);

            // The read of the answer is done here, so nothing holds the socket after the method.
            return await answer;
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// Reads a socket until the other side closes its half of the connection.
    /// </summary>
    /// <param name="client">The socket to read.</param>
    /// <param name="cancellationToken">A token to cancel the wait for bytes.</param>
    /// <returns>Every byte that came back.</returns>
    private static async Task<byte[]> ReceiveAllAsync(Socket client, CancellationToken cancellationToken)
    {
        var answer = new List<byte>();
        var buffer = new byte[4096];

        while (true)
        {
            var read = await client.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

            if (read == 0)
            {
                return [.. answer];
            }

            answer.AddRange(buffer.AsSpan(0, read));
        }
    }

    /// <summary>
    /// A TCP server that sends back every byte that it reads. It stands for socat in the
    /// container.
    /// </summary>
    private sealed class EchoServer : IDisposable
    {
        private readonly Socket _listener;
        private readonly CancellationTokenSource _stop = new();
        private readonly TaskCompletionSource _connectionEnded =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

        /// <summary>
        /// Gets a task that completes when a connection of the server closes.
        /// </summary>
        internal Task ConnectionEnded => _connectionEnded.Task;

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
                finally
                {
                    _ = _connectionEnded.TrySetResult();
                }
            }
        }
    }

    /// <summary>
    /// A TCP server that reads a request until the end of it, and answers only then. It stands
    /// for a service that needs time for the answer, while the client already closed the half of
    /// the request.
    /// </summary>
    private sealed class LateAnswerServer : IDisposable
    {
        private readonly Socket _listener;
        private readonly CancellationTokenSource _stop = new();

        private LateAnswerServer(Socket listener)
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
        internal static LateAnswerServer Start()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(16);

            return new LateAnswerServer(listener);
        }

        private async Task AcceptAsync()
        {
            try
            {
                using var client = await _listener.AcceptAsync(_stop.Token);

                var request = new List<byte>();
                var buffer = new byte[64];

                while (true)
                {
                    var read = await client.ReceiveAsync(buffer, SocketFlags.None, _stop.Token);

                    if (read == 0)
                    {
                        break;
                    }

                    request.AddRange(buffer.AsSpan(0, read));
                }

                // The end of the request came through the bridge, so the answer can go back.
                _ = await client.SendAsync(request.ToArray(), SocketFlags.None, _stop.Token);
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
