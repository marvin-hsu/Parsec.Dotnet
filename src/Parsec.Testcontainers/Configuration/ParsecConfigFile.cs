using System.Globalization;
using System.Text;

namespace Parsec.Testcontainers.Configuration;

/// <summary>
/// Writes the service configuration file that the container reads at start.
/// </summary>
/// <remarks>
/// The text follows the configuration schema of Parsec 1.5.0. It keeps the same providers and the
/// same key manager as the file in the image. Only the values that the test author can change are
/// different. The output always uses the line feed character, because the file goes into a Linux
/// container.
/// </remarks>
internal static class ParsecConfigFile
{
    /// <summary>
    /// The names of the administrators that the Direct authenticator accepts.
    /// </summary>
    /// <remarks>
    /// The bundled parsec-tool sends its own crate name as the application name, and version 0.7.0
    /// gives no way to change it. The second name is for test code, which can send any name.
    /// </remarks>
    internal static readonly string[] DirectAdmins = ["parsec-tool", "admin"];

    /// <summary>
    /// Builds the text of the service configuration file.
    /// </summary>
    /// <param name="authType">The authenticator of the service.</param>
    /// <param name="logLevel">The log level of the service.</param>
    /// <param name="socketDirectory">The directory in the container that holds the socket.</param>
    /// <returns>The text of the configuration file.</returns>
    internal static string Build(ParsecAuthType authType, ParsecLogLevel logLevel, string socketDirectory)
    {
        var builder = new StringBuilder();

        Line(builder, "# Parsec service configuration written by Parsec.Testcontainers.");
        Line(builder, "#");
        Line(builder, "# THIS FILE IS FOR INTEGRATION TESTING ONLY. It gives an application more");
        Line(builder, "# trust than a real system must give. Read the deployment guidance before");
        Line(builder, "# you copy any part of it:");
        Line(builder, "# https://parallaxsecond.github.io/parsec-book/parsec_security/secure_deployment.html");
        Line(builder, "#");
        Line(builder, "# Schema: Parsec " + ParsecImage.ParsecVersion + ".");
        Line(builder, string.Empty);

        Line(builder, "[core_settings]");
        Line(builder, "log_level = \"" + LogLevelValue(logLevel) + "\"");
        Line(builder, "allow_root = true");
        Line(builder, string.Empty);

        Line(builder, "[listener]");
        Line(builder, "listener_type = \"DomainSocket\"");
        Line(builder, "timeout = 200");
        Line(builder, "socket_path = \"" + socketDirectory + "/" + ParsecImage.SocketFileName + "\"");
        Line(builder, string.Empty);

        Line(builder, "[authenticator]");
        Line(builder, "auth_type = \"" + AuthTypeValue(authType) + "\"");

        if (authType == ParsecAuthType.Direct)
        {
            // With the Direct authenticator an administrator is an application name, so the names
            // are known before the container starts. With Unix peer credentials an administrator
            // is a user ID, which the module cannot know, so it declares no administrator.
            Line(builder, "admins = [" + string.Join(", ", DirectAdmins.Select(name => "{ name = \"" + name + "\" }")) + "]");
        }

        Line(builder, string.Empty);

        Line(builder, "[[key_manager]]");
        Line(builder, "name = \"sqlite-manager\"");
        Line(builder, "manager_type = \"SQLite\"");
        Line(builder, "sqlite_db_path = \"/var/lib/parsec/kim.sqlite3\"");
        Line(builder, string.Empty);

        Line(builder, "[[provider]]");
        Line(builder, "name = \"mbed-crypto-provider\"");
        Line(builder, "provider_type = \"MbedCrypto\"");
        Line(builder, "key_info_manager = \"sqlite-manager\"");

        return builder.ToString();
    }

    private static void Line(StringBuilder builder, string text)
        => builder.Append(text).Append('\n');

    private static string LogLevelValue(ParsecLogLevel logLevel) => logLevel switch
    {
        ParsecLogLevel.Error => "error",
        ParsecLogLevel.Warn => "warn",
        ParsecLogLevel.Info => "info",
        ParsecLogLevel.Debug => "debug",
        ParsecLogLevel.Trace => "trace",
        _ => throw new ArgumentOutOfRangeException(
            nameof(logLevel),
            logLevel,
            string.Format(CultureInfo.InvariantCulture, "The log level {0} is not a value of {1}.", logLevel, nameof(ParsecLogLevel))),
    };

    private static string AuthTypeValue(ParsecAuthType authType) => authType switch
    {
        ParsecAuthType.Direct => "Direct",
        ParsecAuthType.UnixPeerCredentials => "UnixPeerCredentials",
        _ => throw new ArgumentOutOfRangeException(
            nameof(authType),
            authType,
            string.Format(CultureInfo.InvariantCulture, "The authenticator {0} is not a value of {1}.", authType, nameof(ParsecAuthType))),
    };
}
