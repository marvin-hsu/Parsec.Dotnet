namespace Parsec.Client.Tests;

/// <summary>
/// A read-only stream that hands out at most a fixed number of bytes per read.
/// It reproduces a socket that delivers a message in pieces.
/// </summary>
/// <param name="content">The bytes to deliver.</param>
/// <param name="chunkSize">The largest number of bytes that one read returns.</param>
internal sealed class DripStream(byte[] content, int chunkSize) : Stream
{
    private int _position;

    /// <summary>Gets the number of times that a caller read from the stream.</summary>
    public int ReadCount { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => content.Length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        ReadCount++;
        var available = Math.Min(Math.Min(chunkSize, buffer.Length), content.Length - _position);
        content.AsSpan(_position, available).CopyTo(buffer);
        _position += available;
        return available;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
