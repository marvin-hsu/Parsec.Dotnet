using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Parsec.Client.Interop;

/// <summary>
/// Holds the calls into the C library of the platform.
/// </summary>
internal static class LibC
{
    /// <summary>
    /// Gets the effective user ID of the calling process.
    /// </summary>
    /// <remarks>
    /// The Unix peer credentials authenticator of the service compares the declared ID against
    /// the ID that the kernel reports for the peer of the socket. That kernel value is the
    /// effective user ID, so the client declares the effective ID and not the real one.
    /// </remarks>
    /// <returns>The effective user ID. The call always answers and never fails.</returns>
    [DllImport("libc", EntryPoint = "geteuid")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [SuppressMessage(
        "Interoperability",
        "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute to generate P/Invoke marshalling code at compile time",
        Justification = "The source generator of LibraryImportAttribute needs AllowUnsafeBlocks for the whole project. The signature takes no argument and returns one blittable integer, so it needs no marshalling and it works in a native ahead-of-time build as it is.")]
    internal static extern uint GetEffectiveUserId();
}
