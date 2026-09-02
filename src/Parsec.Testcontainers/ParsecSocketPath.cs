namespace Parsec.Testcontainers;

/// <summary>
/// Finds the socket of the service in the container.
/// </summary>
/// <remarks>
/// The builder and the container both need these values. The builder writes them into the
/// configuration file and into the endpoint variable. The container reports them to the test
/// author.
/// </remarks>
internal static class ParsecSocketPath
{
    /// <summary>
    /// Gets the directory in the container that holds the socket, without a trailing slash.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    /// <returns>The directory in the container that holds the socket.</returns>
    internal static string DirectoryInContainer(ParsecConfiguration configuration)
        => configuration.SocketDirectory?.TrimEnd('/') is { Length: > 0 } directory
            ? directory
            : ParsecImage.SocketDirectory;

    /// <summary>
    /// Gets the path of the socket in the container.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    /// <returns>The path of the socket in the container.</returns>
    internal static string InContainer(ParsecConfiguration configuration)
        => DirectoryInContainer(configuration) + "/" + ParsecImage.SocketFileName;
}
