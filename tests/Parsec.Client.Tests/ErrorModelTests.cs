using System.Globalization;
using System.Net.Sockets;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Covers the exception hierarchy and the map from a response status to an exception type.
/// The expected shape of the map comes from the status tables of parsec-book: values 1 to 999
/// come from the service, and values 1000 to 1999 come from the PSA Crypto layer.
/// </summary>
public sealed class ErrorModelTests
{
    /// <summary>The lowest status of the PSA Crypto range.</summary>
    private const ushort PsaRangeStart = 1000;

    /// <summary>The highest status of the PSA Crypto range.</summary>
    private const ushort PsaRangeEnd = 1999;

    /// <summary>A value that the opcode table does not assign.</summary>
    private const uint UnassignedOpcode = 0x1D;

    /// <summary>A status that no table assigns, and that sits outside the PSA range.</summary>
    private const ushort UnknownServiceStatus = 900;

    /// <summary>A status that no table assigns, and that sits inside the PSA range.</summary>
    private const ushort UnknownPsaStatus = 1900;

    public static TheoryData<ResponseStatus> KnownStatuses()
    {
        var data = new TheoryData<ResponseStatus>();
        foreach (var status in Enum.GetValues<ResponseStatus>())
        {
            data.Add(status);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(KnownStatuses))]
    public void EveryKnownStatusMapsToTheTypeThatItsNameStates(ResponseStatus status)
    {
        if (status == ResponseStatus.Success)
        {
            // A success is not a fault. The map refuses it instead of inventing an exception.
            Assert.Throws<ArgumentOutOfRangeException>(() => ParsecServiceException.FromStatus(status));
            return;
        }

        // The expected type comes from the name of the status, not from its value. The two are
        // independent, so a wrong value in the enum cannot make this test pass.
        var isPsa = Enum.GetName(status)!.StartsWith("PsaError", StringComparison.Ordinal);
        var exception = ParsecServiceException.FromStatus(status);

        if (isPsa)
        {
            Assert.IsType<ParsecPsaException>(exception);
        }
        else
        {
            Assert.IsType<ParsecServiceException>(exception);
        }

        Assert.Equal(status, exception.Status);
        Assert.Equal(Enum.GetName(status), exception.StatusName);
    }

    [Fact]
    public void EveryStatusOfTheWireFieldMapsWithoutAThrow()
    {
        // The status field is 16 bits wide. A service can send any of these values, and none of
        // them is allowed to break the client.
        for (var value = 1; value <= ushort.MaxValue; value++)
        {
            var status = (ResponseStatus)value;
            var exception = ParsecServiceException.FromStatus(status, Opcode.Ping);

            Assert.IsAssignableFrom<ParsecServiceException>(exception);
            Assert.Equal(status, exception.Status);

            var expectsPsa = value is >= PsaRangeStart and <= PsaRangeEnd;
            Assert.Equal(expectsPsa, exception is ParsecPsaException);
        }
    }

    [Fact]
    public void AnUnknownStatusOutsideThePsaRangeIsAServiceFault()
    {
        var status = (ResponseStatus)UnknownServiceStatus;
        Assert.False(status.IsKnown());

        var exception = ParsecServiceException.FromStatus(status, Opcode.ListProviders);

        Assert.IsType<ParsecServiceException>(exception);
        Assert.Equal("Unknown", exception.StatusName);
        Assert.Contains("900", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownStatusInsideThePsaRangeIsAPsaFault()
    {
        var status = (ResponseStatus)UnknownPsaStatus;
        Assert.False(status.IsKnown());

        var exception = ParsecServiceException.FromStatus(status, Opcode.PsaSignHash);

        Assert.IsType<ParsecPsaException>(exception);
        Assert.Equal("Unknown", exception.StatusName);
    }

    [Fact]
    public void TheGapInThePsaTableStillMapsToAPsaFault()
    {
        // 1144 is the one value of the PSA range that the protocol does not assign.
        var status = (ResponseStatus)1144;
        Assert.False(status.IsKnown());

        Assert.IsType<ParsecPsaException>(ParsecServiceException.FromStatus(status));
    }

    [Fact]
    public void TheMessageNamesTheOperationAndTheStatus()
    {
        var exception = ParsecServiceException.FromStatus(
            ResponseStatus.PsaErrorInvalidSignature,
            Opcode.PsaVerifyHash);

        Assert.Contains("PsaVerifyHash", exception.Message, StringComparison.Ordinal);
        Assert.Contains("5", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PsaErrorInvalidSignature", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1149", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Opcode.PsaVerifyHash, exception.Operation);
    }

    [Fact]
    public void TheMessageOfAnUnknownOperationCarriesItsWireValue()
    {
        var operation = (Opcode)UnassignedOpcode;
        Assert.False(operation.IsKnown());

        var exception = ParsecServiceException.FromStatus(ResponseStatus.OpcodeDoesNotExist, operation);

        Assert.Contains("Unknown", exception.Message, StringComparison.Ordinal);
        Assert.Contains("29", exception.Message, StringComparison.Ordinal);
        Assert.Contains("OpcodeDoesNotExist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMessageOfEveryOperationNamesThatOperation()
    {
        foreach (var operation in Enum.GetValues<Opcode>())
        {
            var exception = ParsecServiceException.FromStatus(ResponseStatus.AdminOperation, operation);

            Assert.Contains(Enum.GetName(operation)!, exception.Message, StringComparison.Ordinal);
            Assert.Contains(
                ((uint)operation).ToString(CultureInfo.InvariantCulture),
                exception.Message,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AMessageWithoutAnOperationStillNamesTheStatus()
    {
        var exception = ParsecServiceException.FromStatus(ResponseStatus.NotAuthenticated);

        Assert.Null(exception.Operation);
        Assert.Contains("NotAuthenticated", exception.Message, StringComparison.Ordinal);
        Assert.Contains("19", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("request", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OneCatchClauseTakesEveryExceptionOfTheLibrary()
    {
        var exceptions = new ParsecException[]
        {
            ParsecServiceException.FromStatus(ResponseStatus.AdminOperation),
            ParsecServiceException.FromStatus(ResponseStatus.PsaErrorNotPermitted),
            ParsecProtocolException.FromFrameError(ParsecFrameError.BadMagicNumber, Opcode.Ping),
            ParsecTransportException.FromSocketFault("/tmp/p.sock", new SocketException(2)),
            new ParsecConfigurationException("bad"),
        };

        foreach (var exception in exceptions)
        {
            Assert.IsAssignableFrom<ParsecException>(exception);
            Assert.NotEmpty(exception.Message);
        }

        // A PSA fault is also a service fault, so a caller that handles the service can handle it.
        Assert.IsAssignableFrom<ParsecServiceException>(exceptions[1]);
    }

    [Fact]
    public void EveryFrameFaultHasItsOwnSentence()
    {
        var messages = new List<string>();

        foreach (var error in Enum.GetValues<ParsecFrameError>())
        {
            if (error == ParsecFrameError.None)
            {
                continue;
            }

            var exception = ParsecProtocolException.FromFrameError(error, Opcode.Ping);

            Assert.Equal(Opcode.Ping, exception.Operation);
            Assert.Contains("Ping", exception.Message, StringComparison.Ordinal);
            messages.Add(exception.Message);
        }

        Assert.Equal(4, messages.Count);
        Assert.Equal(messages.Count, messages.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AFrameFaultWithNoOperationNamesNoOperation()
    {
        var exception = ParsecProtocolException.FromFrameError(ParsecFrameError.UnexpectedEndOfStream, null);

        Assert.Null(exception.Operation);
        Assert.StartsWith("The connection closed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADecodeFaultKeepsTheFaultOfTheDecoder()
    {
        var inner = new InvalidOperationException("bad varint");

        var exception = ParsecProtocolException.DecodeFailed(Opcode.ListProviders, inner);

        Assert.Same(inner, exception.InnerException);
        Assert.Equal(Opcode.ListProviders, exception.Operation);
        Assert.Contains("ListProviders", exception.Message, StringComparison.Ordinal);
        Assert.Contains("8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASocketFaultKeepsThePathAndTheFaultOfThePlatform()
    {
        var inner = new SocketException((int)SocketError.ConnectionRefused);

        var exception = ParsecTransportException.FromSocketFault("/run/parsec/parsec.sock", inner);

        Assert.Same(inner, exception.InnerException);
        Assert.Contains("/run/parsec/parsec.sock", exception.Message, StringComparison.Ordinal);
        Assert.Contains(inner.Message, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStatusOfAnExceptionThatCarriesOnlyATextIsSuccess()
    {
        // The three standard constructors exist for callers that wrap a fault of their own. They
        // set no status, so the status stays at its default value.
        var exception = new ParsecServiceException("text");

        Assert.Equal(ResponseStatus.Success, exception.Status);
        Assert.Null(exception.Operation);
        Assert.Equal("text", exception.Message);
    }
}
