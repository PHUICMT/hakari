namespace Hakari.Core.Sources;

internal static class ConfigDirectoryScanner
{
    public static IReadOnlyList<string> FindUnder(string homeDirectory)
    {
        try
        {
            return Directory
                .EnumerateDirectories(homeDirectory, ClaudeConfigNames.ConfigDirectorySearchPattern)
                .Where(HasProjectsDirectory)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (IsAccessProblem(exception))
        {
            return [];
        }
    }

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
