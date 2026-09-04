using System.Net.Sockets;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the Unix domain socket transport against a real listener. The golden bytes are the
/// Ping exchange that a real Parsec 1.5.0 service answered.
/// </summary>
public sealed class UnixDomainSocketTransportTests
{
    /// <summary>The 36 bytes of a Ping request for the core provider with no authentication.</summary>
    private const string PingRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "01000000" + "0000" + "0000";

    /// <summary>
    /// The Ping answer of a real service: 36 header bytes, then the two body bytes 0801. The
    /// body holds the major version alone, because protobuf3 leaves a zero-valued scalar off the
    /// wire.
    /// </summary>
    private const string PingResponseHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "02000000" + "0000" + "01000000" + "0000" + "0000" + "0801";

    /// <summary>A response header that states a body of 100 bytes, with no body behind it.</summary>
    private const string HeaderStatingALongBodyHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "64000000" + "0000" + "01000000" + "0000" + "0000";

    private static readonly TimeSpan _shortTimeout = TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan _testTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SendsARequestAndReadsTheAnswerOverARealSocket()
    {
        await using var server = new UnixSocketServer();
        var exchange = server.ExchangeAsync(WireHeader.Size, Convert.FromHexString(PingResponseHex));

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _testTimeout);
        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        var request = ParsecRequest.Create(Opcode.Ping, ProviderId.Core, AuthType.None, default, default);
        await connection.SendAsync(request, TestContext.Current.CancellationToken);
        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Convert.FromHexString(PingRequestHex), await exchange);
        Assert.True(result.IsSuccess);
        Assert.Equal(ResponseStatus.Success, result.Response.Header.Status);
        Assert.Equal(Opcode.Ping, result.Response.Header.Opcode);
        Assert.Equal([0x08, 0x01], result.Response.Body.ToArray());
    }

    [Fact]
    public async Task ReadsAnAnswerThatArrivesInPieces()
    {
        await using var server = new UnixSocketServer();
        var frame = Convert.FromHexString(PingResponseHex);

        var serve = Task.Run(
            async () =>
            {
                using var client = await server.AcceptAsync();
                foreach (var one in frame)
                {
                    await client.SendAsync(new[] { one });
                }
            },
            TestContext.Current.CancellationToken);

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _testTimeout);
        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);
        await serve;

        Assert.True(result.IsSuccess);
        Assert.Equal([0x08, 0x01], result.Response.Body.ToArray());
    }

    [Fact]
    public async Task RefusesABodyThatIsLongerThanTheLimitWithoutWaitingForIt()
    {
        await using var server = new UnixSocketServer();

        // The listener sends the header alone and then holds the connection open. A reader that
        // asked for the body before it checked the length would wait here until the time limit.
        var accept = server.AcceptAsync();

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _testTimeout)
        {
            MaxBodyLength = 10,
        };

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        using var accepted = await accept;
        await accepted.SendAsync(Convert.FromHexString(HeaderStatingALongBodyHex), TestContext.Current.CancellationToken);

        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParsecFrameError.BodyTooLarge, result.Error);
    }

    [Fact]
    public async Task ReportsAnAnswerThatNeverComesAsATimeout()
    {
        await using var server = new UnixSocketServer();
        var accept = server.AcceptAsync();

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _shortTimeout);
        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        // The listener answers nothing at all. It keeps the connection open until the test ends.
        using var accepted = await accept;

        await Assert.ThrowsAsync<TimeoutException>(
            async () => await connection.ReceiveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReportsACancellationOfTheCallerAsACancellationAndNotATimeout()
    {
        await using var server = new UnixSocketServer();
        var accept = server.AcceptAsync();

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _testTimeout);
        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        // The listener answers nothing at all, so only the caller ends the wait.
        using var accepted = await accept;

        using var source = new CancellationTokenSource(_shortTimeout);
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await connection.ReceiveAsync(source.Token));

        Assert.IsNotType<TimeoutException>(exception);
    }

    [Fact]
    public async Task ClosesTheSocketWhenTheConnectionIsDisposed()
    {
        await using var server = new UnixSocketServer();
        var accept = server.AcceptAsync();

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _testTimeout);
        var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        using var accepted = await accept;

        await connection.DisposeAsync();

        var buffer = new byte[1];
        Assert.Equal(0, await accepted.ReceiveAsync(buffer, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReportsAMissingSocketFileAsATransportFault()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"psc-{Guid.NewGuid():N}"[..12] + ".sock");
        var transport = new UnixDomainSocketTransport(new Uri("unix:" + missing), _shortTimeout, _shortTimeout);

        var exception = await Assert.ThrowsAsync<ParsecTransportException>(
            async () => await transport.ConnectAsync(TestContext.Current.CancellationToken));

        // The fault of the platform stays reachable, and the message names the socket file.
        Assert.IsType<SocketException>(exception.InnerException);
        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsAFaultOfTheSocketDuringASendAsATransportFault()
    {
        await using var server = new UnixSocketServer();
        var accept = server.AcceptAsync();

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _testTimeout);
        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        // The service goes away with the connection still open, so the write meets a broken pipe.
        (await accept).Dispose();

        var request = ParsecRequest.Create(Opcode.Ping, ProviderId.Core, AuthType.None, default, default);

        var fault = await Assert.ThrowsAsync<ParsecTransportException>(
            async () => await connection.SendAsync(request, TestContext.Current.CancellationToken));

        // The fault of the platform stays reachable, and the message names the socket file.
        Assert.IsType<IOException>(fault.InnerException);
        Assert.Contains(server.SocketPath, fault.Message, StringComparison.Ordinal);

        // A read of the same connection reports the end of the stream. A lost peer of a Unix
        // socket closes the stream, so it does not raise a fault of the platform.
        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ReadsTheSocketPathOutOfTheEndpoint()
    {
        var transport = new UnixDomainSocketTransport(new Uri("unix:///tmp/parsec-test.sock"));

        Assert.Equal("/tmp/parsec-test.sock", transport.SocketPath);
        Assert.Equal(UnixDomainSocketTransport.DefaultTimeout, transport.ConnectTimeout);
        Assert.Equal(UnixDomainSocketTransport.DefaultTimeout, transport.IoTimeout);
        Assert.Equal(WireHeader.DefaultMaxContentLength, transport.MaxBodyLength);
    }

    [Fact]
    public void RefusesAnEndpointThatIsNotAUnixSocket()
    {
        Assert.Throws<ParsecConfigurationException>(
            () => new UnixDomainSocketTransport(new Uri("http://example.com/parsec")));
        Assert.Throws<ArgumentNullException>(() => new UnixDomainSocketTransport(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefusesATimeLimitThatIsNotPositive(int seconds)
    {
        var endpoint = new Uri("unix:/tmp/parsec-test.sock");
        var timeout = TimeSpan.FromSeconds(seconds);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UnixDomainSocketTransport(endpoint, timeout, _testTimeout));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UnixDomainSocketTransport(endpoint, _testTimeout, timeout));
    }

    [Fact]
    public void AcceptsAnInfiniteTimeLimit()
    {
        var transport = new UnixDomainSocketTransport(
            new Uri("unix:/tmp/parsec-test.sock"),
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        Assert.Equal(Timeout.InfiniteTimeSpan, transport.ConnectTimeout);
    }

    [Fact]
    public void RefusesABodyLimitThatNoArrayCanHold()
    {
        var transport = new UnixDomainSocketTransport(new Uri("unix:/tmp/parsec-test.sock"));

        Assert.Throws<ArgumentOutOfRangeException>(() => transport.MaxBodyLength = uint.MaxValue);
    }

    // The guard in this constructor is a duplicate: ParsecEndpoint.GetSocketPath guards again and
    // throws the same type. The test pins the contract a caller sees; it cannot prove which of
    // the two guards ran.
    [Fact]
    public void TheConstructorRefusesNoEndpoint()
        => Assert.Throws<ArgumentNullException>(
            () => new UnixDomainSocketTransport(null!, _testTimeout, _testTimeout));

    [Fact]
    public async Task AConnectThatFailsClosesTheSocketItOpened()
    {
        // A socket that stays open after every failed connect exhausts the descriptors of the
        // process, and the caller then fails for a reason that has nothing to do with its own
        // work. Counting descriptors is the only way to see it from outside.
        var endpoint = new Uri("unix:" + Path.Combine(Path.GetTempPath(), "parsec-no-listener.sock"));
        var transport = new UnixDomainSocketTransport(endpoint, _shortTimeout, _shortTimeout);

        var before = OpenDescriptorCount();

        for (var i = 0; i < 40; i++)
        {
            _ = await Assert.ThrowsAsync<ParsecTransportException>(
                async () => await transport.ConnectAsync(TestContext.Current.CancellationToken));
        }

        var after = OpenDescriptorCount();

        // The count moves a little on its own, so the check leaves room. A leak of one per
        // attempt would show as forty.
        Assert.True(
            after - before < 20,
            $"The descriptor count went from {before} to {after} over 40 failed connects.");
    }

    [Fact]
    public async Task AFaultOfTheSocketDuringAReceiveIsATransportFault()
    {
        await using var server = new UnixSocketServer();

        var accepted = Task.Run(
            async () =>
            {
                using var client = await server.AcceptAsync();

                // The peer goes away in the middle of the answer, so the read fails.
                await client.SendAsync(new byte[] { 0x10, 0xA7, 0xC0, 0x5E });
                client.Shutdown(SocketShutdown.Both);
            },
            TestContext.Current.CancellationToken);

        var transport = new UnixDomainSocketTransport(server.Endpoint, _testTimeout, _testTimeout);
        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        // A short answer is a framing fault, not a transport fault. Either way the caller gets a
        // Parsec exception and never a raw socket exception.
        Assert.False(result.IsSuccess);

        await accepted;
    }

    [Fact]
    public async Task AnInfiniteTimeoutRunsTheOperationWithTheTokenOfTheCaller()
    {
        // Timeout.InfiniteTimeSpan takes a path of its own that installs no timer. Without a
        // test the whole branch could go and nothing would notice.
        using var stop = new CancellationTokenSource();

        var seen = CancellationToken.None;

        var answer = await TimeoutOperation.RunAsync(
            Timeout.InfiniteTimeSpan,
            token =>
            {
                seen = token;
                return ValueTask.FromResult(7);
            },
            stop.Token);

        Assert.Equal(7, answer);
        Assert.Equal(stop.Token, seen);
    }

    /// <summary>
    /// Counts the file descriptors that this process holds open.
    /// </summary>
    /// <returns>The count, or -1 when the platform does not report it.</returns>
    private static int OpenDescriptorCount()
        => Directory.Exists("/dev/fd") ? Directory.GetFileSystemEntries("/dev/fd").Length : -1;
}
