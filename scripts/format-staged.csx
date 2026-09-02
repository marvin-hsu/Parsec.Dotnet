// pre-commit: run `dotnet format` on staged .cs files, then re-stage them.
// Args = staged file paths (expanded from ${staged} in task-runner.json).

using System.Diagnostics;
using System.Text;

if (Args.Count == 0)
{
    Console.WriteLine("format-staged.csx: no staged .cs files, skipping.");
    return 0;
}

private var files = new StringBuilder();
foreach (var arg in Args)
{
    files.Append(" \"").Append(arg).Append('"');
}

private var format = Run("dotnet", $"format --include{files} --verbosity quiet");
if (format != 0)
{
    return format;
}

// Re-stage anything dotnet format rewrote so the fixes land in this commit.
return Run("git", "update-index --again");

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
