using System.Runtime.InteropServices;

namespace Parsec.Testcontainers.Tests;

// These tests need no Docker endpoint. They only look at a path, and two of them make a
// directory in the temporary area of this machine.
public sealed class ParsecHostSocketDirectoryTests
{
    [Fact]
    public void IsBindMountSupported_TellsIfTheHostIsLinux()
    {
        Assert.Equal(
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            ParsecHostSocketDirectory.IsBindMountSupported);
    }

    [Fact]
    public void Create_MakesNoDirectoryOnTheDisk()
    {
        var directory = ParsecHostSocketDirectory.Create();

        Assert.False(Directory.Exists(directory.DirectoryPath));
    }

    [Fact]
    public void Create_GivesAShortSocketPath()
    {
        var directory = ParsecHostSocketDirectory.Create();

        // A longer path gives an ArgumentOutOfRangeException from the socket, which reads like a
        // defect in the test and not like a path that is too long.
        Assert.True(
            directory.SocketPath.Length <= ParsecHostSocketDirectory.MaxSocketPathLength,
            directory.SocketPath + " has " + directory.SocketPath.Length + " characters.");
    }

    [Fact]
    public void Create_NamesTheDirectoryAfterTheTool()
    {
        var directory = ParsecHostSocketDirectory.Create();

        var name = Path.GetFileName(directory.DirectoryPath);

        Assert.StartsWith("parsec-", name, StringComparison.Ordinal);
        Assert.Equal("parsec-".Length + 8, name.Length);
    }

    [Fact]
    public void Create_PutsTheSocketInTheDirectory()
    {
        var directory = ParsecHostSocketDirectory.Create();

        Assert.Equal(
            Path.Combine(directory.DirectoryPath, ParsecImage.SocketFileName),
            directory.SocketPath);
    }

    [Fact]
    public void Create_GivesEachContainerItsOwnDirectory()
    {
        var first = ParsecHostSocketDirectory.Create();
        var second = ParsecHostSocketDirectory.Create();

        Assert.NotEqual(first.DirectoryPath, second.DirectoryPath);
    }

    [Fact]
    public void MakeDirectory_GivesFullPermissions()
    {
        var directory = ParsecHostSocketDirectory.Create();

        try
        {
            directory.MakeDirectory();

            Assert.True(Directory.Exists(directory.DirectoryPath));

            if (!OperatingSystem.IsWindows())
            {
                // The service in the container runs as another user, so every user must be able
                // to write the socket in the directory.
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute,
                    File.GetUnixFileMode(directory.DirectoryPath));
            }
        }
        finally
        {
            directory.Remove();
        }
    }

    [Fact]
    public void Remove_DeletesTheDirectoryAndItsContent()
    {
        var directory = ParsecHostSocketDirectory.Create();
        directory.MakeDirectory();
        File.WriteAllText(Path.Combine(directory.DirectoryPath, "leftover.txt"), "content");

        directory.Remove();

        Assert.False(Directory.Exists(directory.DirectoryPath));
    }

    [Fact]
    public void Remove_WithNoDirectory_DoesNothing()
    {
        var directory = ParsecHostSocketDirectory.Create();

        directory.Remove();

        Assert.False(Directory.Exists(directory.DirectoryPath));
    }
}
