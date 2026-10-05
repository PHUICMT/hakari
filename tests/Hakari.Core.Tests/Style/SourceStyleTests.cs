namespace Hakari.Core.Tests.Style;

public class SourceStyleTests
{
    private const int MaximumLineLength = 100;
    private const string SolutionFileName = "Hakari.sln";

    private static readonly string[] CheckedDirectories = ["src", "tests", "spikes"];
    private static readonly string[] ExcludedDirectoryNames = ["bin", "obj"];

    [Fact]
    public void Source_lines_stay_within_the_maximum_length()
    {
        var longLines = FindSourceFiles()
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, index)))
            .Where(entry => entry.line.Length > MaximumLineLength)
            .Select(entry => $"{entry.file}:{entry.index + 1} ({entry.line.Length} chars)")
            .ToList();

        Assert.True(longLines.Count == 0, string.Join(Environment.NewLine, longLines));
    }

    private static IEnumerable<string> FindSourceFiles()
    {
        var solutionDirectory = FindSolutionDirectory();

        return CheckedDirectories
            .Select(directory => Path.Combine(solutionDirectory, directory))
            .SelectMany(directory => Directory.EnumerateFiles(
                directory, "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsInExcludedDirectory(file));
    }

    private static bool ContainsSolution(DirectoryInfo directory) =>
        File.Exists(Path.Combine(directory.FullName, SolutionFileName));

    private static bool IsInExcludedDirectory(string file) =>
        file.Split(Path.DirectorySeparatorChar).Any(ExcludedDirectoryNames.Contains);

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !ContainsSolution(directory))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException($"{SolutionFileName} not found");
    }
}
