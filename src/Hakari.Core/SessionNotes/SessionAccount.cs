using Hakari.Core.Accounts;
using Hakari.Core.Sources;

namespace Hakari.Core.SessionNotes;

/// <summary>Which account a Claude Code session runs under, from where its log is.</summary>
public static class SessionAccount
{
    private const string SessionSourceId = "session";

    /// <summary>
    /// The folder that holds "projects", which is the session's config folder, also when it
    /// was moved with CLAUDE_CONFIG_DIR. Null when the log is not under a projects folder.
    /// </summary>
    public static string? ConfigDirectoryOf(string transcriptPath)
    {
        var directory = Path.GetDirectoryName(transcriptPath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (string.Equals(
                Path.GetFileName(directory),
                ClaudeConfigNames.ProjectsDirectoryName,
                StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(directory);
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    /// <summary>The account signed in to the session's config folder, read from its file.</summary>
    public static string? IdOf(string transcriptPath) =>
        ConfigDirectoryOf(transcriptPath) is { } configDirectory
            ? AccountFileReader.Read(new UsageSource(
                SessionSourceId,
                SourceKind.ConfigDirectory,
                SessionSourceId,
                configDirectory))?.AccountId
            : null;
}
