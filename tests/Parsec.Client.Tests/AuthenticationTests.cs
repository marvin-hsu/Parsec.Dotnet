using System.Diagnostics;
using System.Globalization;
using System.Text;
using Parsec.Client.Protocol;

namespace Parsec.Client.Tests;

/// <summary>
/// Locks the byte layout of the authentication field and the rule that the core provider takes
/// no authentication. The expected bytes are written out by hand, so the test does not agree
/// with a wrong encoder.
/// </summary>
public sealed class AuthenticationTests
{
    /// <summary>
    /// A signature request for the Mbed Crypto provider with direct authentication. The body is
    /// AABBCC and the authentication field is the UTF-8 text "app", which is 617070. The header
    /// states provider 1, auth type 1, content length 3, auth length 3 and opcode 4.
    /// </summary>
    private const string DirectRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "01" + "0000000000000000" +
        "00" + "00" + "01" + "03000000" + "0300" + "04000000" + "0000" + "0000" +
        "AABBCC" + "617070";

    /// <summary>
    /// The same request with Unix peer credentials for user 4660, which is 0x1234. The
    /// authentication field is the four bytes 34120000, the little-endian form of the user ID.
    /// The header states auth type 3 and auth length 4.
    /// </summary>
    private const string UnixPeerRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "01" + "0000000000000000" +
        "00" + "00" + "03" + "03000000" + "0400" + "04000000" + "0000" + "0000" +
        "AABBCC" + "34120000";

    /// <summary>
    /// A Ping request for the core provider. The authentication type is 0 and the authentication
    /// length is 0, so the message is the header alone.
    /// </summary>
    private const string CorePingRequestHex =
        "10A7C05E" + "1E00" + "01" + "00" + "0000" + "00" + "0000000000000000" +
        "00" + "00" + "00" + "00000000" + "0000" + "01000000" + "0000" + "0000";

    [Fact]
    public void NoAuthenticationCarriesTypeZeroAndNoBytes()
    {
        var authentication = NoAuthentication.Instance;
        var buffer = new byte[4];
        Array.Fill(buffer, (byte)0xCC);

        Assert.Equal(AuthType.None, authentication.Type);
        Assert.Equal(0, authentication.AuthBytesLength);
        Assert.Equal(0, authentication.WriteAuthBytes(buffer));
        Assert.All(buffer, b => Assert.Equal(0xCC, b));
    }

    [Fact]
    public void NoAuthenticationSharesOneInstance() =>
        Assert.Same(NoAuthentication.Instance, NoAuthentication.Instance);

    [Theory]
    [InlineData("app", "617070")]
    [InlineData("my-app", "6D792D617070")]
    [InlineData("/keys/rsa", "2F6B6579732F727361")]
    [InlineData("café", "636166C3A9")]
    [InlineData("密鑰", "E5AF86E991B0")]
    public void DirectAuthenticationWritesTheApplicationNameAsUtf8(string name, string expectedHex)
    {
        var expected = Convert.FromHexString(expectedHex);
        var authentication = new DirectAuthentication(name);
        var buffer = new byte[expected.Length];

        Assert.Equal(AuthType.Direct, authentication.Type);
        Assert.Equal(name, authentication.ApplicationName);
        Assert.Equal(expected.Length, authentication.AuthBytesLength);
        Assert.Equal(expected.Length, authentication.WriteAuthBytes(buffer));
        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void DirectAuthenticationCountsBytesAndNotCharacters()
    {
        // The name holds four characters and five UTF-8 bytes. The header states a byte count,
        // so a client that counts characters writes a length that does not match the field.
        var authentication = new DirectAuthentication("café");

        Assert.Equal(4, "café".Length);
        Assert.Equal(5, authentication.AuthBytesLength);
    }

    [Fact]
    public void DirectAuthenticationLeavesTheRestOfALargerBufferAlone()
    {
        var authentication = new DirectAuthentication("app");
        var buffer = new byte[6];
        Array.Fill(buffer, (byte)0xCC);

        Assert.Equal(3, authentication.WriteAuthBytes(buffer));
        Assert.Equal(Convert.FromHexString("617070CCCCCC"), buffer);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void DirectAuthenticationRejectsANameThatIsNotAnIdentity(string name) =>
        Assert.Throws<ArgumentException>(() => new DirectAuthentication(name));

    [Fact]
    public void DirectAuthenticationRejectsANullName() =>
        Assert.Throws<ArgumentNullException>(() => new DirectAuthentication(null!));

    [Fact]
    public void DirectAuthenticationRejectsANameThatTheHeaderCannotDescribe()
    {
        var longest = new DirectAuthentication(new string('a', ushort.MaxValue));

        Assert.Equal(ushort.MaxValue, longest.AuthBytesLength);
        Assert.Throws<ArgumentException>(() => new DirectAuthentication(new string('a', ushort.MaxValue + 1)));
    }

    [Fact]
    public void DirectAuthenticationRejectsABufferThatIsTooSmall()
    {
        var authentication = new DirectAuthentication("app");

        var fault = Assert.Throws<ArgumentException>(() => authentication.WriteAuthBytes(new byte[2]));
        Assert.Contains("2 bytes", fault.Message, StringComparison.Ordinal);
        Assert.Contains("3 bytes", fault.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0u, "00000000")]
    [InlineData(1u, "01000000")]
    [InlineData(0x1234u, "34120000")]
    [InlineData(0x12345678u, "78563412")]
    [InlineData(uint.MaxValue, "FFFFFFFF")]
    public void UnixPeerCredentialsWritesTheUserIdAsLittleEndian(uint userId, string expectedHex)
    {
        var authentication = new UnixPeerCredentialsAuthentication(userId);
        var buffer = new byte[4];

        Assert.Equal(AuthType.UnixPeerCredentials, authentication.Type);
        Assert.Equal(userId, authentication.UserId);
        Assert.Equal(4, authentication.AuthBytesLength);
        Assert.Equal(4, authentication.WriteAuthBytes(buffer));
        Assert.Equal(Convert.FromHexString(expectedHex), buffer);
    }

    [Fact]
    public void UnixPeerCredentialsRejectsABufferThatIsTooSmall()
    {
        var authentication = new UnixPeerCredentialsAuthentication(1u);

        Assert.Throws<ArgumentException>(() => authentication.WriteAuthBytes(new byte[3]));
    }

    [Fact]
    public async Task UnixPeerCredentialsReadsTheUserIdOfThisProcess()
    {
        Assert.SkipUnless(
            OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            "The user ID comes from the C library of a Unix platform.");

        // The expected value comes from a separate program, so the test does not compare the
        // library against itself. The -u option of id reports the effective user ID.
        var expected = await RunAsync("/usr/bin/id", "-u");
        var authentication = new UnixPeerCredentialsAuthentication();

        Assert.Equal(uint.Parse(expected, CultureInfo.InvariantCulture), authentication.UserId);
    }

    [Fact]
    public void JwtSvidAuthenticationWritesTheTokenAsUtf8()
    {
        const string Token = "eyJhbGciOiJFUzI1NiJ9.e30.sig";
        var expected = Encoding.UTF8.GetBytes(Token);
        var authentication = new JwtSvidAuthentication(Token);
        var buffer = new byte[expected.Length];

        Assert.Equal(AuthType.JwtSvid, authentication.Type);
        Assert.Equal(expected.Length, authentication.AuthBytesLength);
        Assert.Equal(expected.Length, authentication.WriteAuthBytes(buffer));
        Assert.Equal(expected, buffer);
        Assert.Equal("65794A68", Convert.ToHexString(buffer[..4]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void JwtSvidAuthenticationRejectsATokenThatIsNotAToken(string token) =>
        Assert.Throws<ArgumentException>(() => new JwtSvidAuthentication(token));

    [Fact]
    public void JwtSvidAuthenticationRejectsATokenThatTheHeaderCannotDescribe() =>
        Assert.Throws<ArgumentException>(() => new JwtSvidAuthentication(new string('a', ushort.MaxValue + 1)));

    [Fact]
    public void CoreRequestWithNoAuthenticationWritesTheGoldenBytes()
    {
        var request = ParsecRequest.Create(
            Opcode.Ping,
            ProviderId.Core,
            NoAuthentication.Instance,
            ReadOnlyMemory<byte>.Empty);

        Assert.Equal(AuthType.None, request.Header.AuthType);
        Assert.Equal(0, request.Header.AuthLength);
        Assert.Equal(Convert.FromHexString(CorePingRequestHex), request.ToArray());
    }

    [Fact]
    public void DirectRequestWritesTheGoldenBytes()
    {
        var request = ParsecRequest.Create(
            Opcode.PsaSignHash,
            ProviderId.MbedCrypto,
            new DirectAuthentication("app"),
            new byte[] { 0xAA, 0xBB, 0xCC });

        Assert.Equal(AuthType.Direct, request.Header.AuthType);
        Assert.Equal(3, request.Header.AuthLength);
        Assert.Equal(Convert.FromHexString(DirectRequestHex), request.ToArray());
    }

    [Fact]
    public void UnixPeerCredentialsRequestWritesTheGoldenBytes()
    {
        var request = ParsecRequest.Create(
            Opcode.PsaSignHash,
            ProviderId.MbedCrypto,
            new UnixPeerCredentialsAuthentication(0x1234u),
            new byte[] { 0xAA, 0xBB, 0xCC });

        Assert.Equal(AuthType.UnixPeerCredentials, request.Header.AuthType);
        Assert.Equal(4, request.Header.AuthLength);
        Assert.Equal(Convert.FromHexString(UnixPeerRequestHex), request.ToArray());
    }

    [Fact]
    public void CoreRequestRefusesDirectAuthentication()
    {
        var fault = Assert.Throws<ParsecConfigurationException>(() => ParsecRequest.Create(
            Opcode.Ping,
            ProviderId.Core,
            new DirectAuthentication("app"),
            ReadOnlyMemory<byte>.Empty));

        Assert.Contains("Direct", fault.Message, StringComparison.Ordinal);
        Assert.Contains("None", fault.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AuthType.Direct)]
    [InlineData(AuthType.Jwt)]
    [InlineData(AuthType.UnixPeerCredentials)]
    [InlineData(AuthType.JwtSvid)]
    [InlineData((AuthType)200)]
    public void CoreRequestRefusesEveryAuthenticationTypeExceptNone(AuthType type)
    {
        var authentication = new StubAuthentication(type, 1, 1);

        Assert.Throws<ParsecConfigurationException>(
            () => AuthenticationField.Create(authentication, ProviderId.Core));
    }

    [Theory]
    [InlineData(ProviderId.MbedCrypto)]
    [InlineData(ProviderId.Pkcs11)]
    [InlineData(ProviderId.Tpm)]
    [InlineData(ProviderId.TrustedService)]
    [InlineData(ProviderId.CryptoAuthLib)]
    [InlineData((ProviderId)200)]
    public void EveryOtherProviderTakesDirectAuthentication(ProviderId provider)
    {
        var field = AuthenticationField.Create(new DirectAuthentication("app"), provider);

        Assert.Equal("617070", Convert.ToHexString(field.Span));
    }

    [Fact]
    public void CoreRequestTakesNoAuthentication()
    {
        var field = AuthenticationField.Create(NoAuthentication.Instance, ProviderId.Core);

        Assert.True(field.IsEmpty);
    }

    [Fact]
    public void AuthenticationFieldRejectsANullAuthentication() =>
        Assert.Throws<ArgumentNullException>(
            () => AuthenticationField.Create(null!, ProviderId.MbedCrypto));

    [Theory]
    [InlineData(-1)]
    [InlineData(ushort.MaxValue + 1)]
    public void AuthenticationFieldRejectsAByteCountThatTheHeaderCannotDescribe(int length)
    {
        var authentication = new StubAuthentication(AuthType.Direct, length, 0);

        Assert.Throws<ParsecConfigurationException>(
            () => AuthenticationField.Create(authentication, ProviderId.MbedCrypto));
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(4, 5)]
    public void AuthenticationFieldRejectsAWriteThatDoesNotMatchTheReportedCount(
        int reportedLength,
        int writtenLength)
    {
        var authentication = new StubAuthentication(AuthType.Direct, reportedLength, writtenLength);

        var fault = Assert.Throws<ParsecConfigurationException>(
            () => AuthenticationField.Create(authentication, ProviderId.MbedCrypto));
        Assert.Contains(
            reportedLength.ToString(CultureInfo.InvariantCulture),
            fault.Message,
            StringComparison.Ordinal);
    }

    private static async Task<string> RunAsync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
            },
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return output.Trim();
    }

    /// <summary>
    /// An authentication that an application could write. It reports one byte count and writes
    /// another, so it shows what the library does with an implementation that it cannot trust.
    /// </summary>
    private sealed class StubAuthentication(AuthType type, int reportedLength, int writtenLength)
        : IParsecAuthentication
    {
        public AuthType Type => type;

        public int AuthBytesLength => reportedLength;

        public int WriteAuthBytes(Span<byte> destination) => writtenLength;
    }
}
