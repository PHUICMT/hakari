namespace Hakari.Core.SessionNotes;

/// <summary>What Claude Code runs a hook's command with on Windows.</summary>
public enum HookShell
{
    /// <summary>Git Bash, which Claude Code uses whenever it finds it.</summary>
    Bash,

    /// <summary>Windows PowerShell, when Git Bash is not installed.</summary>
    PowerShell,
}

/// <summary>
/// Finds Git Bash the way Claude Code does, so the hook is written for the shell that will
/// really run it: the path set in CLAUDE_CODE_GIT_BASH_PATH, then the Git found on PATH, then
/// Git's usual install folders.
/// </summary>
public static class HookShellDetector
{
    public const string GitBashPathVariable = "CLAUDE_CODE_GIT_BASH_PATH";

    private const string GitExecutable = "git.exe";
    private const string BashRelativeToGitRoot = @"bin\bash.exe";
    private const string GitFolder = "Git";

    public static HookShell Detect() =>
        FindGitBash() is null ? HookShell.PowerShell : HookShell.Bash;

    public static string? FindGitBash() =>
        Candidates().FirstOrDefault(File.Exists);

    private static IEnumerable<string> Candidates()
    {
        if (Environment.GetEnvironmentVariable(GitBashPathVariable) is { Length: > 0 } set)
        {
            yield return set;
        }

        foreach (var directory in PathDirectories())
        {
            // Git puts git.exe in "cmd" or "bin" under its root, which holds bin\bash.exe.
            if (File.Exists(Path.Combine(directory, GitExecutable))
                && Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory)) is { } root)
            {
                yield return Path.Combine(root, BashRelativeToGitRoot);
            }
        }

        foreach (var folder in new[]
        {
            Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86,
        })
        {
            yield return Path.Combine(
                Environment.GetFolderPath(folder), GitFolder, BashRelativeToGitRoot);
        }

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            GitFolder,
            BashRelativeToGitRoot);
    }

    private static IEnumerable<string> PathDirectories() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => directory.Trim('"'));
}
