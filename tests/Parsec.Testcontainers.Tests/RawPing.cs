using System.Buffers.Binary;
using System.Net.Sockets;

namespace Parsec.Testcontainers.Tests;

/// <summary>
/// Sends a Ping request to the Parsec service over a Unix domain socket.
/// </summary>
/// <remarks>
/// The tests of this module must show that a client on this machine reaches the service through
/// the socket that <see cref="ParsecContainer.SocketPath"/> gives. The protocol code of the
/// client library does not exist yet, so the helper writes the bytes of the request itself. The
/// layout follows the wire protocol of Parsec: a header of 36 bytes, then the body, then the
/// authentication field. Ping needs no body and no authentication.
/// </remarks>
internal static class RawPing
{
    /// <summary>
    /// The number of bytes of the header of a request and of a response.
    /// </summary>
    private const int HeaderLength = 36;

    /// <summary>
    /// The value of the magic number field. The service and the client both reject a message
    /// with another value.
    /// </summary>
    private const uint MagicNumber = 0x5EC0A710;

    /// <summary>
    /// The value of the header size field. It counts the bytes of the header after the field.
    /// </summary>
    private const ushort HeaderSize = 30;

    /// <summary>
    /// The operation code of Ping.
    /// </summary>
    private const uint PingOpcode = 1;

    /// <summary>
    /// Sends a Ping request and reads the answer.
    /// </summary>
    /// <param name="socketPath">The path of the socket of the service.</param>
    /// <param name="cancellationToken">A token to cancel the wait for the answer.</param>
    /// <returns>The status field of the response and the body of the response.</returns>
    internal static async Task<(ushort Status, byte[] Body)> SendAsync(string socketPath, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);

        var request = BuildRequest();
        _ = await socket.SendAsync(request, SocketFlags.None, cancellationToken);

        var header = await ReceiveExactlyAsync(socket, HeaderLength, cancellationToken);

        Assert.Equal(MagicNumber, BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)));

        var status = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(32, 2));
        var bodyLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(22, 4));
        var body = await ReceiveExactlyAsync(socket, bodyLength, cancellationToken);

        return (status, body);
    }

    /// <summary>
    /// Builds the bytes of a Ping request.
    /// </summary>
    /// <returns>The header of the request. Ping has no body and no authentication field.</returns>
    private static byte[] BuildRequest()
    {
        var header = new byte[HeaderLength];

        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0, 4), MagicNumber);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4, 2), HeaderSize);
        header[6] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28, 4), PingOpcode);

        // Every other field is zero: the minor version, the flags, the core provider, no session,
        // a protobuf body, a protobuf answer, no authentication, no body and no reserved value.
        return header;
    }

    /// <summary>
    /// Reads an exact number of bytes from the socket.
    /// </summary>
    /// <param name="socket">The connected socket.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <param name="cancellationToken">A token to cancel the wait for the bytes.</param>
    /// <returns>The bytes that the service sent.</returns>
    private static async Task<byte[]> ReceiveExactlyAsync(Socket socket, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var read = 0;

        while (read < count)
        {
            var received = await socket.ReceiveAsync(buffer.AsMemory(read), SocketFlags.None, cancellationToken);

            Assert.NotEqual(0, received);

            read += received;
        }

        return buffer;
    }
}
