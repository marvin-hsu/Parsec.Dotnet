using System.Globalization;
using System.Text;

namespace Parsec.Client;

/// <summary>
/// Finds the address of the Parsec service.
/// </summary>
/// <remarks>
/// <para>
/// The service specification tells a client to read the <c>PARSEC_SERVICE_ENDPOINT</c>
/// environment variable, and to use <c>unix:/run/parsec/parsec.sock</c> when that variable is
/// not set. The variable holds one URI. The library reads the variable without help from the
/// application, and the application can still pass an endpoint of its own.
/// </para>
/// <para>
/// The only scheme that the service defines today is <c>unix</c>, which names a Unix domain
/// socket. Every other scheme raises <see cref="ParsecConfigurationException"/>.
/// </para>
/// </remarks>
public static class ParsecEndpoint
{
    /// <summary>The environment variable that holds the address of the service.</summary>
    public const string EnvironmentVariableName = "PARSEC_SERVICE_ENDPOINT";

    /// <summary>The scheme of an endpoint that names a Unix domain socket.</summary>
    public const string UnixScheme = "unix";

    /// <summary>The socket path that the service listens on by default.</summary>
    public const string DefaultSocketPath = "/run/parsec/parsec.sock";

    /// <summary>
    /// The byte count of the socket path field of an address on Linux. One byte of the field
    /// holds the terminator, so a path can use one byte less.
    /// </summary>
    internal const int SocketPathFieldBytesOnLinux = 108;

    /// <summary>
    /// The byte count of the socket path field of an address on every other platform, macOS
    /// included. One byte of the field holds the terminator.
    /// </summary>
    internal const int SocketPathFieldBytesElsewhere = 104;

    /// <summary>Gets the endpoint that the client uses when nothing else states one.</summary>
    public static Uri Default { get; } = new(UnixScheme + ":" + DefaultSocketPath);

    /// <summary>
    /// Gets the byte count of the socket path field of an address on this platform.
    /// </summary>
    internal static int SocketPathFieldBytes =>
        OperatingSystem.IsLinux() ? SocketPathFieldBytesOnLinux : SocketPathFieldBytesElsewhere;

    /// <summary>
    /// Finds the address of the service from the environment.
    /// </summary>
    /// <returns>
    /// The URI in <see cref="EnvironmentVariableName"/>, or <see cref="Default"/> when that
    /// variable is empty or absent.
    /// </returns>
    /// <exception cref="ParsecConfigurationException">
    /// The environment variable does not hold an absolute URI, or it holds a scheme other than
    /// <see cref="UnixScheme"/>.
    /// </exception>
    public static Uri Resolve() => Resolve(null);

    /// <summary>
    /// Finds the address of the service, with an address from the application first.
    /// </summary>
    /// <param name="endpoint">
    /// The address that the application states, or <see langword="null"/> to read the
    /// environment.
    /// </param>
    /// <returns>
    /// The address of <paramref name="endpoint"/>, or the URI in
    /// <see cref="EnvironmentVariableName"/>, or <see cref="Default"/>.
    /// </returns>
    /// <exception cref="ParsecConfigurationException">
    /// The chosen text is not an absolute URI, or it holds a scheme other than
    /// <see cref="UnixScheme"/>.
    /// </exception>
    public static Uri Resolve(string? endpoint) =>
        Resolve(endpoint, Environment.GetEnvironmentVariable(EnvironmentVariableName));

    /// <summary>
    /// Reads the Unix socket path out of an endpoint.
    /// </summary>
    /// <param name="endpoint">The address of the service.</param>
    /// <returns>The path of the socket file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ParsecConfigurationException">
    /// The endpoint is relative, its scheme is not <see cref="UnixScheme"/>, it names a host, it
    /// carries no path, or its path is longer than this platform accepts for a Unix domain
    /// socket.
    /// </exception>
    public static string GetSocketPath(Uri endpoint) => GetSocketPath(endpoint, SocketPathFieldBytes);

    /// <summary>
    /// Finds the address of the service from a stated address and a stated environment value.
    /// </summary>
    /// <param name="endpoint">The address that the application states, or <see langword="null"/>.</param>
    /// <param name="environmentValue">The value of the environment variable, or <see langword="null"/>.</param>
    /// <returns>The address of the service.</returns>
    /// <remarks>
    /// The public methods read the real environment. This method takes the value as an argument,
    /// so a test can cover the order of the three sources without a change to the process.
    /// </remarks>
    internal static Uri Resolve(string? endpoint, string? environmentValue)
    {
        var text = FirstNonEmpty(endpoint, environmentValue);
        if (text is null)
        {
            return Default;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            throw new ParsecConfigurationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The Parsec service endpoint '{text}' is not an absolute URI. Use a URI such as '{Default}'."));
        }

        RequireUnixScheme(uri);
        return uri;
    }

    /// <summary>
    /// Reads the Unix socket path out of an endpoint, against a stated path field size.
    /// </summary>
    /// <param name="endpoint">The address of the service.</param>
    /// <param name="socketPathFieldBytes">The byte count of the socket path field of an address.</param>
    /// <returns>The path of the socket file.</returns>
    /// <remarks>
    /// The path limit belongs to the platform. This method takes the limit as an argument, so a
    /// test can cover both limits on one machine.
    /// </remarks>
    internal static string GetSocketPath(Uri endpoint, int socketPathFieldBytes)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri)
        {
            throw new ParsecConfigurationException(
                $"The Parsec service endpoint '{endpoint}' is relative. Use an absolute URI such as '{Default}'.");
        }

        RequireUnixScheme(endpoint);

        // A URI such as unix://run/parsec.sock puts "run" in the host and leaves "/parsec.sock"
        // in the path. The Go reference client drops the host and connects to the short path.
        // This client refuses instead, because a silent connection to another path is worse than
        // an error that names the mistake.
        if (endpoint.Host.Length > 0)
        {
            throw new ParsecConfigurationException(
                $"The Parsec service endpoint '{endpoint}' names the host '{endpoint.Host}'. A Unix socket endpoint holds a path only, such as '{Default}'.");
        }

        var path = Uri.UnescapeDataString(endpoint.AbsolutePath);
        if (path.Length == 0)
        {
            throw new ParsecConfigurationException(
                $"The Parsec service endpoint '{endpoint}' carries no socket path. Use a URI such as '{Default}'.");
        }

        // The address of a Unix domain socket holds the path in a fixed field that ends with a
        // terminator byte, so the longest path is one byte shorter than the field.
        var maximum = socketPathFieldBytes - 1;
        var length = Encoding.UTF8.GetByteCount(path);
        if (length > maximum)
        {
            throw new ParsecConfigurationException(string.Create(
                CultureInfo.InvariantCulture,
                $"The Unix socket path '{path}' is {length} bytes. This platform accepts at most {maximum} bytes. Move the socket to a shorter path, then set {EnvironmentVariableName} to it."));
        }

        return path;
    }

    private static void RequireUnixScheme(Uri endpoint)
    {
        if (!string.Equals(endpoint.Scheme, UnixScheme, StringComparison.OrdinalIgnoreCase))
        {
            throw new ParsecConfigurationException(
                $"The Parsec service endpoint '{endpoint}' uses the scheme '{endpoint.Scheme}'. This client supports the scheme '{UnixScheme}' only, as in '{Default}'.");
        }
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? null : second;
    }
}
