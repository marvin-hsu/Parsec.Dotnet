using System.Net.Sockets;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Tests;

/// <summary>
/// Locks the behaviour of the two fake transports. The later steps of the wire protocol test
/// against these fakes alone, so a fake that lies would hide a fault in the client.
/// The golden bytes come from the observed exchange with a real Parsec 1.5.0 service.
/// </summary>
public sealed class FakeTransportTests
{
    /// <summary>The 36 bytes of a Ping request for the core provider with no authentication.</summary>
    private const string PingRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "01000000" + "0000" + "0000";

    /// <summary>
    /// The Ping answer of a real service: the 36 header bytes, then the two body bytes 0801.
    /// The body holds only the major version, because protobuf3 leaves a zero-valued scalar off
    /// the wire.
    /// </summary>
    private const string PingResponseHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "02000000" + "0000" + "01000000" + "0000" + "0000" + "0801";

    /// <summary>
    /// An answer that reports a failure: opcode PsaVerifyHash, provider Mbed Crypto,
    /// status 1149 (PsaErrorInvalidSignature) and no body.
    /// </summary>
    private const string FailedResponseHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "01" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "05000000" + "7D04" + "0000";

    [Fact]
    public async Task ScriptedTransportAnswersWithTheScriptedBytes()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Convert.FromHexString(PingResponseHex));

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        await connection.SendAsync(MakePingRequest(), TestContext.Current.CancellationToken);
        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ParsecFrameError.None, result.Error);
        Assert.Equal(ResponseStatus.Success, result.Response.Header.Status);
        Assert.Equal(Opcode.Ping, result.Response.Header.Opcode);
        Assert.Equal(new byte[] { 0x08, 0x01 }, result.Response.Body.ToArray());
        Assert.Equal(0, transport.PendingResponseCount);
    }

    [Fact]
    public async Task ScriptedTransportKeepsTheWireBytesOfEverySentRequest()
    {
        var transport = new ScriptedTransport();

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        await connection.SendAsync(MakePingRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(Convert.FromHexString(PingRequestHex), Assert.Single(transport.SentRequestBytes));
        Assert.Equal(Opcode.Ping, Assert.Single(transport.SentRequests).Header.Opcode);
    }

    [Theory]
    [InlineData(1, 38)]
    [InlineData(2, 19)]
    [InlineData(37, 2)]
    [InlineData(int.MaxValue, 1)]
    public async Task ScriptedTransportReassemblesAnAnswerThatArrivesInPieces(
        int chunkSize,
        int leastReadCount)
    {
        var transport = new ScriptedTransport { ChunkSize = chunkSize }
            .EnqueueResponse(Convert.FromHexString(PingResponseHex));

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 0x08, 0x01 }, result.Response.Body.ToArray());

        // The answer is 38 bytes. A chunk size of 1 therefore costs at least 38 reads, which
        // proves that the fake really delivered the answer in pieces.
        Assert.True(
            transport.LastResponseReadCount >= leastReadCount,
            $"The read count was {transport.LastResponseReadCount}, below {leastReadCount}.");
    }

    [Fact]
    public void ScriptedTransportRefusesAChunkSizeBelowOne()
    {
        var transport = new ScriptedTransport();

        Assert.Throws<ArgumentOutOfRangeException>(() => transport.ChunkSize = 0);
    }

    [Fact]
    public async Task ScriptedTransportAnswersInTheOrderOfTheScript()
    {
        var transport = new ScriptedTransport()
            .EnqueueResponse(Opcode.Ping, ResponseStatus.Success)
            .EnqueueResponse(Opcode.PsaVerifyHash, ResponseStatus.PsaErrorInvalidSignature)
            .EnqueueResponse(Opcode.ListProviders, ResponseStatus.DeserializingBodyFailed);

        Assert.Equal(3, transport.PendingResponseCount);

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        var first = await connection.ReceiveAsync(TestContext.Current.CancellationToken);
        var second = await connection.ReceiveAsync(TestContext.Current.CancellationToken);
        var third = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ResponseStatus.Success, first.Response.Header.Status);
        Assert.Equal(ResponseStatus.PsaErrorInvalidSignature, second.Response.Header.Status);
        Assert.Equal(ResponseStatus.DeserializingBodyFailed, third.Response.Header.Status);
    }

    [Fact]
    public async Task ScriptedTransportReportsAMalformedAnswerAndThrowsNothing()
    {
        var corrupted = Convert.FromHexString(PingResponseHex);
        corrupted[0] ^= 0xFF;

        var transport = new ScriptedTransport().EnqueueResponse(corrupted);

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParsecFrameError.BadMagicNumber, result.Error);
    }

    [Fact]
    public async Task ScriptedTransportReportsAnAnswerThatEndsTooSoon()
    {
        var truncated = Convert.FromHexString(PingResponseHex)[..20];
        var transport = new ScriptedTransport().EnqueueResponse(truncated);

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ParsecFrameError.UnexpectedEndOfStream, result.Error);
    }

    [Fact]
    public async Task ScriptedTransportPassesItsBodyLimitToTheFrameReader()
    {
        var transport = new ScriptedTransport { MaxBodyLength = 1 }
            .EnqueueResponse(Convert.FromHexString(PingResponseHex));

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        var result = await connection.ReceiveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ParsecFrameError.BodyTooLarge, result.Error);
    }

    [Fact]
    public async Task ScriptedTransportFailsTheTestWhenTheScriptIsEmpty()
    {
        var transport = new ScriptedTransport();

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.ReceiveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScriptedTransportCountsConnectionsAndDisposals()
    {
        var transport = new ScriptedTransport();

        var first = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        await using (var second = await transport.ConnectAsync(TestContext.Current.CancellationToken))
        {
            Assert.Equal(2, transport.ConnectCount);
            Assert.Equal(0, transport.DisposedConnectionCount);
        }

        Assert.Equal(1, transport.DisposedConnectionCount);

        await first.DisposeAsync();
        await first.DisposeAsync();

        Assert.Equal(2, transport.DisposedConnectionCount);
    }

    [Fact]
    public async Task ScriptedTransportRecordsTheRequestsOfEveryConnectionInOrder()
    {
        var transport = new ScriptedTransport();

        await using (var first = await transport.ConnectAsync(TestContext.Current.CancellationToken))
        {
            await first.SendAsync(MakePingRequest(), TestContext.Current.CancellationToken);
        }

        var listProviders = ParsecRequest.Create(
            Opcode.ListProviders,
            ProviderId.Core,
            AuthType.None,
            ReadOnlyMemory<byte>.Empty,
            ReadOnlyMemory<byte>.Empty);

        await using (var second = await transport.ConnectAsync(TestContext.Current.CancellationToken))
        {
            await second.SendAsync(listProviders, TestContext.Current.CancellationToken);
        }

        Opcode[] expected = [Opcode.Ping, Opcode.ListProviders];

        Assert.Equal(expected, transport.SentRequests.Select(request => request.Header.Opcode));
    }

    [Fact]
    public async Task ScriptedTransportRefusesUseAfterDisposal()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success);
        var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        await connection.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await connection.SendAsync(MakePingRequest(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await connection.ReceiveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScriptedTransportObeysACancelledToken()
    {
        var transport = new ScriptedTransport().EnqueueResponse(Opcode.Ping, ResponseStatus.Success);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await transport.ConnectAsync(cancellation.Token));

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connection.SendAsync(MakePingRequest(), cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connection.ReceiveAsync(cancellation.Token));
    }

    [Fact]
    public void BuiltAnswersMatchTheObservedBytes()
    {
        Assert.Equal(
            Convert.FromHexString(PingResponseHex),
            ScriptedTransport.BuildResponseBytes(
                Opcode.Ping,
                ResponseStatus.Success,
                new byte[] { 0x08, 0x01 }));

        Assert.Equal(
            Convert.FromHexString(FailedResponseHex),
            ScriptedTransport.BuildResponseBytes(
                Opcode.PsaVerifyHash,
                ResponseStatus.PsaErrorInvalidSignature,
                ReadOnlyMemory<byte>.Empty,
                ProviderId.MbedCrypto));
    }

    [Fact]
    public async Task FailingTransportThrowsWhenItOpensAConnection()
    {
        var transport = new FailingTransport(TransportFailureStage.Connect);

        await Assert.ThrowsAsync<IOException>(async () => await transport.ConnectAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, transport.ConnectCount);
    }

    [Fact]
    public async Task FailingTransportThrowsWhenTheClientSends()
    {
        var transport = new FailingTransport(TransportFailureStage.Send);

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, transport.ConnectCount);
        await Assert.ThrowsAsync<IOException>(async () => await connection.SendAsync(MakePingRequest(), TestContext.Current.CancellationToken));
        Assert.Empty(transport.SentRequests);
    }

    [Fact]
    public async Task FailingTransportTakesTheRequestAndThrowsWhenTheClientReads()
    {
        var transport = new FailingTransport(TransportFailureStage.Receive);

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        await connection.SendAsync(MakePingRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(Opcode.Ping, Assert.Single(transport.SentRequests).Header.Opcode);
        await Assert.ThrowsAsync<IOException>(async () => await connection.ReceiveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FailingTransportThrowsTheStatedFaultAndANewObjectEachTime()
    {
        var transport = new FailingTransport(
            TransportFailureStage.Receive,
            () => new SocketException((int)SocketError.ConnectionReset));

        await using var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);

        var first = await Assert.ThrowsAsync<SocketException>(async () => await connection.ReceiveAsync(TestContext.Current.CancellationToken));
        var second = await Assert.ThrowsAsync<SocketException>(async () => await connection.ReceiveAsync(TestContext.Current.CancellationToken));

        Assert.Equal(SocketError.ConnectionReset, first.SocketErrorCode);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task FailingTransportCountsDisposalOnce()
    {
        var transport = new FailingTransport(TransportFailureStage.Receive);

        var connection = await transport.ConnectAsync(TestContext.Current.CancellationToken);
        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.Equal(1, transport.DisposedConnectionCount);
    }

    private static ParsecRequest MakePingRequest() => ParsecRequest.Create(
        Opcode.Ping,
        ProviderId.Core,
        AuthType.None,
        ReadOnlyMemory<byte>.Empty,
        ReadOnlyMemory<byte>.Empty);
}
