using Google.Protobuf;

namespace Parsec.Client.Tests;

/// <summary>
/// Guards the protoc pipeline: the parsec-operations submodule must be checked out and
/// its messages compiled into Parsec.Client as internal types.
/// </summary>
public sealed class ProtobufTests
{
    [Fact]
    public void PingResultRoundTripsThroughWireFormat()
    {
        var original = new Ping.Result
        {
            WireProtocolVersionMaj = 1,
            WireProtocolVersionMin = 0,
        };

        var parsed = Ping.Result.Parser.ParseFrom(original.ToByteArray());

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void GeneratedMessagesAreInternal()
    {
        Assert.False(typeof(Ping.Result).IsPublic);
        Assert.Equal(typeof(IParsecClient).Assembly, typeof(Ping.Result).Assembly);
    }
}
