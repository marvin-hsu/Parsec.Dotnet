using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Parsec.Testcontainers;

/// <summary>
/// A short-lived directory on this machine that holds the socket a client connects to.
/// </summary>
/// <remarks>
/// <para>
/// A Linux host shares the kernel with the container. A bind mount of this directory into the
/// container puts the socket of the service in the directory, and a client on this machine
/// connects to it with no bridge. Read <see cref="IsBindMountSupported"/> before you use an
/// instance for a bind mount, because other host systems run the container in a virtual machine.
/// There a bind mount shows the socket file but no client can connect to it.
/// </para>
/// <para>
/// The directory sits directly in the temporary area and has a short name, because a Unix socket
/// path has a low length limit. macOS allows <see cref="MaxSocketPathLength"/> bytes and Linux
/// allows 108. A long path gives an error that is easy to read as a connection failure.
/// </para>
/// </remarks>
internal sealed class ParsecHostSocketDirectory
{
    /// <summary>
    /// The largest number of bytes that a Unix socket path can have. This is the macOS limit,
    /// which is lower than the Linux limit.
    /// </summary>
    internal const int MaxSocketPathLength = 104;

    /// <summary>
    /// The first part of the name of the directory. It tells a user which tool made the directory.
    /// </summary>
    private const string NamePrefix = "parsec-";

    /// <summary>
    /// The number of random characters in the name of the directory. Each container gets its own
    /// directory, so parallel tests do not share a socket.
    /// </summary>
    private const int RandomPartLength = 8;

    /// <summary>
    /// The characters that the random part of the name can have. All of them are safe in a path.
    /// </summary>
    private const string RandomCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Full permissions for the owner, the group and all other users.
    /// </summary>
    /// <remarks>
    /// The service in the container runs as another user than the user on this machine, and the
    /// identifier of that user is not known here. Full permissions let the service write the
    /// socket in the directory. The directory holds only a socket of one test run.
    /// </remarks>
    private const UnixFileMode AllPermissions =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecHostSocketDirectory"/> class.
    /// </summary>
    /// <param name="directoryPath">The path of the directory on this machine.</param>
    private ParsecHostSocketDirectory(string directoryPath)
    {
        DirectoryPath = directoryPath;
        SocketPath = Path.Combine(directoryPath, ParsecImage.SocketFileName);
    }

    /// <summary>
    /// Gets a value indicating whether a bind mount of a host directory gives a usable socket.
    /// </summary>
    internal static bool IsBindMountSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Gets the path of the directory on this machine.
    /// </summary>
    internal string DirectoryPath { get; }

    /// <summary>
    /// Gets the path of the socket in the directory.
    /// </summary>
    internal string SocketPath { get; }

    /// <summary>
    /// Selects a directory for one container. The method makes no directory on the disk.
    /// </summary>
    /// <returns>A new instance with a name that no other instance has.</returns>
    /// <exception cref="InvalidOperationException">
    /// The temporary area of this machine is too deep for a Unix socket path.
    /// </exception>
    internal static ParsecHostSocketDirectory Create()
    {
        var name = NamePrefix + RandomNumberGenerator.GetString(RandomCharacters, RandomPartLength);
        var result = new ParsecHostSocketDirectory(Path.Combine(RootDirectory(), name));

        if (result.SocketPath.Length > MaxSocketPathLength)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "The socket path {0} has {1} characters. A Unix socket path can have {2} characters. Set the TMPDIR variable to a directory with a shorter path.",
                result.SocketPath,
                result.SocketPath.Length,
                MaxSocketPathLength));
        }

        return result;
    }

    /// <summary>
    /// Makes the directory on the disk and gives it full permissions.
    /// </summary>
    /// <remarks>
    /// The container must not make the directory. Docker makes a missing bind mount source as
    /// root, and then the service in the container cannot write the socket.
    /// </remarks>
    internal void MakeDirectory()
    {
        _ = Directory.CreateDirectory(DirectoryPath);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(DirectoryPath, AllPermissions);
        }
    }

    /// <summary>
    /// Removes the directory and everything in it.
    /// </summary>
    /// <remarks>
    /// A directory in the temporary area is not important enough to fail the dispose of a
    /// container, so the method keeps a failure to itself.
    /// </remarks>
    internal void Remove()
    {
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // The directory was never made, or something else removed it.
        }
        catch (IOException)
        {
            // The socket is still open, or the file system refused the delete.
        }
        catch (UnauthorizedAccessException)
        {
            // The container made a file that this user cannot remove.
        }
    }

    /// <summary>
    /// Gets the directory that holds the socket directories.
    /// </summary>
    /// <returns>The path of the temporary area of this machine.</returns>
    /// <remarks>
    /// Unix systems use <c>/tmp</c> directly. On macOS the temporary area of the process is a
    /// path under <c>/var/folders</c> that is long enough to break the socket path limit.
    /// </remarks>
    private static string RootDirectory()
        => OperatingSystem.IsWindows() ? Path.GetTempPath() : "/tmp";
}
