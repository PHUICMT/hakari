using System.Text.Json;
using Hakari.Core.Settings;
using Hakari.Core.Sources;
using Hakari.Core.Startup;

namespace Hakari.Core.SessionNotes;

/// <param name="Changed">Settings files written.</param>
/// <param name="Failed">Files that could not be read or written; they were left alone.</param>
public sealed record HookInstallResult(int Changed, int Failed);

/// <summary>
/// Puts the hook into the user settings of every Claude Code config folder on Windows, or
/// takes it out. WSL distributions run their hooks inside Linux and are left alone.
/// </summary>
public static class SessionHookInstaller
{
    private const string ExecutableName = "Hakari.exe";

    /// <summary>The Store copy's alias from its manifest; its own folder is locked.</summary>
    private const string PackagedAlias = @"Microsoft\WindowsApps\hakari.exe";

    public static IReadOnlyList<string> ConfigDirectories(HakariSettings settings) =>
    [
        .. SourceDiscovery
            .Discover(new SourceDiscoveryOptions(WslScanMode.Off, settings.ExtraConfigDirectories))
            .Where(source => source.Kind != SourceKind.Wsl)
            .Select(source => source.ConfigDirectory)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Where Claude Code should start Hakari for this install.</summary>
    public static string ExecutablePath() =>
        PackageIdentity.IsPackaged
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                PackagedAlias)
            : ResidentLocation.Read() ?? Path.Combine(AppContext.BaseDirectory, ExecutableName);

    public static HookInstallResult Install(IEnumerable<string> configDirectories) =>
        Apply(configDirectories, CommandForThisInstall(), install: true);

    public static HookInstallResult Remove(IEnumerable<string> configDirectories) =>
        Apply(configDirectories, CommandForThisInstall(), install: false);

    /// <summary>
    /// After a zip copy moved or the Store updated, an old command would point nowhere;
    /// only files whose hook differs are written.
    /// </summary>
    public static HookInstallResult Repair(HakariSettings settings) =>
        settings.TellSessionsAboutLimits
            ? Install(ConfigDirectories(settings))
            : new HookInstallResult(0, 0);

    private static string CommandForThisInstall() =>
        ClaudeHookSettings.CommandFor(ExecutablePath());

    private static HookInstallResult Apply(
        IEnumerable<string> configDirectories,
        string command,
        bool install)
    {
        var changed = 0;
        var failed = 0;
        foreach (var directory in configDirectories)
        {
            var path = Path.Combine(directory, ClaudeHookSettings.FileName);
            try
            {
                var json = File.Exists(path) ? File.ReadAllText(path) : null;
                var next = Next(json, command, install);
                if (next is null)
                {
                    continue;
                }

                Write(path, next);
                changed++;
            }
            catch (Exception exception) when (exception is IOException or JsonException
                or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return new HookInstallResult(changed, failed);
    }

    /// <returns>The new file, or null when it already says what it should.</returns>
    private static string? Next(string? json, string command, bool install) =>
        install
            ? ClaudeHookSettings.HasHook(json, command)
                ? null
                : ClaudeHookSettings.WithHook(json, command)
            : json is not null && ClaudeHookSettings.HasAnyHook(json)
                ? ClaudeHookSettings.WithoutHook(json)
                : null;

    /// <summary>Written beside and moved over, so Claude Code never reads half a file.</summary>
    private static void Write(string path, string json)
    {
        var temporary = $"{path}.hakari.tmp";
        File.WriteAllText(temporary, json + Environment.NewLine);
        File.Move(temporary, path, overwrite: true);
    }
}
