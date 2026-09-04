using DotNet.Testcontainers;

namespace Parsec.Testcontainers.Sockets;

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

    /// <summary>
    /// Checks that a socket directory of a caller can hold the socket of the service.
    /// </summary>
    /// <param name="socketDirectory">The directory that the caller gave, or <c>null</c> when the caller gave none.</param>
    /// <exception cref="ArgumentException">The directory cannot hold the socket.</exception>
    /// <remarks>
    /// A <c>null</c> value is correct. It means that the container keeps the directory of the
    /// image.
    /// </remarks>
    internal static void ValidateDirectory(string? socketDirectory)
    {
        if (socketDirectory is null)
        {
            return;
        }

        _ = Guard.Argument(socketDirectory, nameof(ParsecConfiguration.SocketDirectory))
            .NotEmpty()
            .ThrowIf(
                argument => !argument.Value.StartsWith('/'),
                argument => new ArgumentException(
                    "The socket directory must be an absolute path in the container. Give a path that starts with a slash.",
                    argument.Name))
            .ThrowIf(
                argument => argument.Value.TrimEnd('/').Length == 0,
                argument => new ArgumentException(
                    "The socket directory must not be the root directory. Give a directory that the service user can write.",
                    argument.Name));
    }
}
