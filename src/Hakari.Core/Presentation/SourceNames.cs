namespace Hakari.Core.Presentation;

/// <summary>
/// Readable names for source ids such as "windows:alex:.claude" or
/// "wsl:Ubuntu-24.04:alex:.claude". The default config folder is not mentioned.
/// </summary>
public static class SourceNames
{
    private const char Separator = ':';
    private const string WindowsPrefix = "windows";
    private const string WslPrefix = "wsl";
    private const string DirectoryPrefix = "directory";
    private const string DefaultConfigFolder = ".claude";

    public static string Display(string sourceId)
    {
        var parts = sourceId.Split(Separator);
        return parts[0] switch
        {
            WindowsPrefix => WithFolder("Windows", parts[^1]),
            WslPrefix when parts.Length >= 2 => WithFolder($"WSL {parts[1]}", parts[^1]),
            DirectoryPrefix => FolderName(sourceId[(DirectoryPrefix.Length + 1)..]),
            _ => sourceId,
        };
    }

    /// <summary>A badge's worth: "Windows", "WSL" or the folder's name.</summary>
    public static string Short(string sourceId)
    {
        var kind = sourceId.Split(Separator)[0];
        return kind switch
        {
            WindowsPrefix => "Windows",
            WslPrefix => "WSL",
            _ => Display(sourceId),
        };
    }

    private static string FolderName(string path) => Path.GetFileName(path.TrimEnd('\\', '/'));

    private static string WithFolder(string name, string folder) =>
        folder == DefaultConfigFolder ? name : $"{name} ({folder})";
}
