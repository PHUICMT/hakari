namespace Hakari.Core.Sources;

public static class SourceDiscovery
{
    private const string WindowsIdPrefix = "windows";
    private const string WslIdPrefix = "wsl";
    private const string ConfigDirectoryIdPrefix = "directory";
    private const char IdSeparator = ':';

    public static IReadOnlyList<UsageSource> Discover(SourceDiscoveryOptions options)
    {
        var sources = new List<UsageSource>();
        AddWindowsSources(sources);
        AddWslSources(sources, options.WslMode);
        AddExtraConfigDirectories(sources, ConfiguredElsewhere());
        AddExtraConfigDirectories(sources, options.ExtraConfigDirectories);
        return sources;
    }

    private static void AddWindowsSources(List<UsageSource> sources)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userName = Environment.UserName;

        foreach (var configDirectory in ConfigDirectoryScanner.FindUnder(home))
        {
            var directoryName = Path.GetFileName(configDirectory);
            AddUnique(sources, new UsageSource(
                Id: BuildId(WindowsIdPrefix, userName, directoryName),
                Kind: SourceKind.Windows,
                DisplayName: $"Windows {directoryName}",
                ConfigDirectory: configDirectory));
        }
    }

    private static void AddWslSources(List<UsageSource> sources, WslScanMode mode)
    {
        foreach (var distribution in WslLocator.FindDistributions(mode))
        {
            foreach (var userHome in WslLocator.FindUserHomes(distribution))
            {
                AddWslUserSources(sources, distribution, userHome);
            }
        }
    }

    private static void AddWslUserSources(
        List<UsageSource> sources,
        string distribution,
        string userHome)
    {
        var userName = Path.GetFileName(userHome);

        foreach (var configDirectory in ConfigDirectoryScanner.FindUnder(userHome))
        {
            var directoryName = Path.GetFileName(configDirectory);
            AddUnique(sources, new UsageSource(
                Id: BuildId(WslIdPrefix, distribution, userName, directoryName),
                Kind: SourceKind.Wsl,
                DisplayName: $"WSL {distribution} ({userName}) {directoryName}",
                ConfigDirectory: configDirectory));
        }
    }

    private static void AddExtraConfigDirectories(
        List<UsageSource> sources,
        IEnumerable<string> configDirectories)
    {
        foreach (var configDirectory in configDirectories)
        {
            if (!ConfigDirectoryScanner.IsConfigDirectory(configDirectory))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(configDirectory);
            AddUnique(sources, new UsageSource(
                Id: BuildId(ConfigDirectoryIdPrefix, fullPath.ToLowerInvariant()),
                Kind: SourceKind.ConfigDirectory,
                DisplayName: fullPath,
                ConfigDirectory: fullPath));
        }
    }

    /// <summary>
    /// A config folder set with CLAUDE_CONFIG_DIR for this user or machine, which can live
    /// anywhere, not only in the home folder.
    /// </summary>
    private static IEnumerable<string> ConfiguredElsewhere() =>
        new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine }
            .Select(target => Environment.GetEnvironmentVariable(
                ClaudeConfigNames.ConfigDirectoryVariable,
                target))
            .OfType<string>()
            .Where(directory => directory.Length > 0 && Path.IsPathFullyQualified(directory));

    private static void AddUnique(List<UsageSource> sources, UsageSource candidate)
    {
        var candidatePath = Path.GetFullPath(candidate.ConfigDirectory);
        var alreadyAdded = sources.Any(existing => string.Equals(
            Path.GetFullPath(existing.ConfigDirectory),
            candidatePath,
            StringComparison.OrdinalIgnoreCase));

        if (!alreadyAdded)
        {
            sources.Add(candidate);
        }
    }

    private static string BuildId(params string[] parts) => string.Join(IdSeparator, parts);
}
