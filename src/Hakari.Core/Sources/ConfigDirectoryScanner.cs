namespace Hakari.Core.Sources;

internal static class ConfigDirectoryScanner
{
    public static IReadOnlyList<string> FindUnder(string homeDirectory)
    {
        try
        {
            return Directory
                .EnumerateDirectories(homeDirectory, ClaudeConfigNames.ConfigDirectorySearchPattern)
                .Where(IsConfigDirectory)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (IsAccessProblem(exception))
        {
            return [];
        }
    }

    /// <summary>
    /// Has session logs, or is signed in and simply not used yet, so a newly added account
    /// shows up before its first session.
    /// </summary>
    public static bool IsConfigDirectory(string configDirectory) =>
        HasProjectsDirectory(configDirectory)
        || File.Exists(Path.Combine(configDirectory, ClaudeConfigNames.CredentialsFileName));

    public static bool HasProjectsDirectory(string configDirectory)
    {
        var projectsDirectory = Path.Combine(
            configDirectory,
            ClaudeConfigNames.ProjectsDirectoryName);
        return Directory.Exists(projectsDirectory);
    }

    internal static bool IsAccessProblem(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;
}
