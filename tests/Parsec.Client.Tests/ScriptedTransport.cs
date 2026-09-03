using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Tests;

/// <summary>
/// A transport that answers from a script and keeps every request that it was given.
/// It stands in for the Parsec service in a unit test, so the test needs no socket and no
/// container and finishes in microseconds. It plays the part of MockIpc in the Rust client.
/// </summary>
/// <remarks>
/// <para>
/// A test puts response bytes in the script with <see cref="EnqueueResponse(byte[])"/> or
/// <see cref="EnqueueResponse(Opcode, ResponseStatus, ReadOnlyMemory{byte}, ProviderId)"/>.
/// Each read takes the next entry of the script and parses it with the real
/// <see cref="ParsecFrameReader"/>. A malformed script therefore produces the same outcome as a
/// malformed answer from a real service, and the fake cannot hide a framing fault.
/// </para>
/// <para>
/// The script holds bytes, not objects, so a test states the exact wire content that the
/// service sends.
/// </para>
/// </remarks>
internal sealed class ScriptedTransport : IParsecTransport
{
    private readonly Queue<byte[]> _script = new();
    private readonly List<ParsecRequest> _sentRequests = [];
    private readonly List<byte[]> _sentRequestBytes = [];

    /// <summary>
    /// Gets or sets the largest number of bytes that one read of a scripted answer returns.
    /// The default hands out the whole answer at once. A value of 1 reproduces a socket that
    /// delivers a message one byte at a time.
    /// </summary>
    public int ChunkSize
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    }

    = int.MaxValue;

    /// <summary>
    /// Gets or sets the largest response body that a read accepts, in bytes.
    /// It goes to the frame reader of each connection.
    /// </summary>
    public uint MaxBodyLength { get; set; } = WireHeader.DefaultMaxContentLength;

    /// <summary>Gets the requests that were sent, in the order that they were sent.</summary>
    public IReadOnlyList<ParsecRequest> SentRequests => _sentRequests;

    /// <summary>Gets the wire bytes of each sent request, in the order that they were sent.</summary>
    public IReadOnlyList<byte[]> SentRequestBytes => _sentRequestBytes;

    /// <summary>Gets the number of connections that were opened.</summary>
    public int ConnectCount { get; private set; }

    /// <summary>Gets the number of connections that were disposed.</summary>
    public int DisposedConnectionCount { get; private set; }

    /// <summary>
    /// Gets the number of stream reads that the last answer needed. A test uses it to prove
    /// that a small <see cref="ChunkSize"/> really delivered the answer in pieces.
    /// </summary>
    public int LastResponseReadCount { get; private set; }

    /// <summary>Gets the number of scripted answers that no read has taken yet.</summary>
    public int PendingResponseCount => _script.Count;

    /// <summary>
    /// Makes the wire bytes of one response message.
    /// </summary>
    /// <param name="opcode">The operation that the response answers.</param>
    /// <param name="status">The outcome that the service reports.</param>
    /// <param name="body">The encoded body. It can be empty.</param>
    /// <param name="provider">The provider that the header names.</param>
    /// <returns>The header bytes followed by the body bytes.</returns>
    public static byte[] BuildResponseBytes(
        Opcode opcode,
        ResponseStatus status,
        ReadOnlyMemory<byte> body,
        ProviderId provider = ProviderId.Core)
    {
        var header = new WireHeader
        {
            MajorVersion = WireHeader.CurrentMajorVersion,
            MinorVersion = WireHeader.CurrentMinorVersion,
            Provider = provider,
            ContentType = BodyType.Protobuf,
            AcceptType = BodyType.Protobuf,
            ContentLength = (uint)body.Length,
            Opcode = opcode,
            Status = status,
        };

        var bytes = new byte[WireHeader.Size + body.Length];
        header.TryWrite(bytes);
        body.Span.CopyTo(bytes.AsSpan(WireHeader.Size));
        return bytes;
    }

    /// <summary>
    /// Puts the wire bytes of one answer at the end of the script.
    /// </summary>
    /// <param name="responseBytes">The bytes that the service sends. They need not be valid.</param>
    /// <returns>The same transport, so a test can chain the calls.</returns>
    public ScriptedTransport EnqueueResponse(byte[] responseBytes)
    {
        _script.Enqueue(responseBytes);
        return this;
    }

    /// <summary>
    /// Builds one answer and puts it at the end of the script.
    /// </summary>
    /// <param name="opcode">The operation that the response answers.</param>
    /// <param name="status">The outcome that the service reports.</param>
    /// <param name="body">The encoded body. It can be empty.</param>
    /// <param name="provider">The provider that the header names.</param>
    /// <returns>The same transport, so a test can chain the calls.</returns>
    public ScriptedTransport EnqueueResponse(
        Opcode opcode,
        ResponseStatus status,
        ReadOnlyMemory<byte> body = default,
        ProviderId provider = ProviderId.Core) =>
        EnqueueResponse(BuildResponseBytes(opcode, status, body, provider));

    /// <inheritdoc/>
    public ValueTask<IParsecConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConnectCount++;

        // The caller owns the connection and disposes it, so the fake does not.
#pragma warning disable CA2000
        return ValueTask.FromResult<IParsecConnection>(new ScriptedConnection(this));
#pragma warning restore CA2000
    }

    private void Record(ParsecRequest request)
    {
        _sentRequests.Add(request);
        _sentRequestBytes.Add(request.ToArray());
    }

    private async ValueTask<FrameReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        if (_script.Count == 0)
        {
            throw new InvalidOperationException(
                "The test read an answer that it did not script. Call EnqueueResponse first.");
        }

        // The real frame reader parses the scripted bytes, so the fake cannot hide a fault in
        // the framing. The drip stream reproduces an answer that arrives in pieces.
        using var stream = new DripStream(_script.Dequeue(), ChunkSize);
        var reader = new ParsecFrameReader(stream) { MaxBodyLength = MaxBodyLength };
        var result = await reader.ReadResponseAsync(cancellationToken);
        LastResponseReadCount = stream.ReadCount;
        return result;
    }

    private sealed class ScriptedConnection(ScriptedTransport owner) : IParsecConnection
    {
        /// <summary>Gets a value indicating whether the connection was disposed.</summary>
        public bool IsDisposed { get; private set; }

        public ValueTask SendAsync(ParsecRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            owner.Record(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask<FrameReadResult> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return owner.ReadAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                owner.DisposedConnectionCount++;
            }

            return ValueTask.CompletedTask;
        }
    }
}
