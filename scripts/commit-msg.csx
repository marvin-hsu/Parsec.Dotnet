// Commit message linter — runs from the commit-msg hook via `dotnet husky exec`.
// Args[0] = path to the commit message file supplied by git.
//   1. Subject must follow Conventional Commits: type(scope)!: description
//   2. Body must contain a DCO sign-off line (git commit -s)

using System.Text.RegularExpressions;

private const string Pattern =
    @"^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-z0-9./-]+\))?!?: .+$";

if (Args.Count == 0 || !File.Exists(Args[0]))
{
    Fail("commit-msg.csx: commit message file not provided.");
    return 1;
}

private var lines = File.ReadAllLines(Args[0]);
private var subject = string.Empty;
private var signedOff = false;

foreach (var line in lines)
{
    if (line.StartsWith("#", StringComparison.Ordinal))
    {
        continue;
    }

    if (subject.Length == 0 && line.Trim().Length > 0)
    {
        subject = line;
    }

    if (line.StartsWith("Signed-off-by: ", StringComparison.Ordinal))
    {
        signedOff = true;
    }
}

private var ok = true;

if (!Regex.IsMatch(subject, Pattern))
{
    Fail("Commit message must follow Conventional Commits, e.g.:");
    Console.Error.WriteLine("    feat(client): add key attestation support");
    Console.Error.WriteLine($"  Got: {subject}");
    ok = false;
}

if (!signedOff)
{
    Fail("Missing DCO sign-off. Commit with: git commit -s");
    ok = false;
}

return ok ? 0 : 1;

private static void Fail(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"✖ {message}");
    Console.ResetColor();
}
