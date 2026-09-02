// pre-push: strict Release build (warnings are errors) followed by the test suite.

using System.Diagnostics;

private var build = Run("dotnet", "build --configuration Release --nologo -v q");
if (build != 0)
{
    return build;
}

return Run("dotnet", "test --configuration Release --no-build --nologo");

private static int Run(string file, string arguments)
{
    var psi = new ProcessStartInfo(file, arguments)
    {
        UseShellExecute = false,
    };

    using var process = Process.Start(psi)!;
    process.WaitForExit();
    return process.ExitCode;
}
