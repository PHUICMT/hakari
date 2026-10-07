namespace Hakari.Core.Updates;

/// <summary>
/// The changes of the running version, from the notes the release build puts next to the
/// executables (whats-new.md, written from the commits since the last version). Read on the
/// first start of a new version, so nothing is fetched.
/// </summary>
public static class WhatsNew
{
    public const string FileName = "whats-new.md";

    private const string BulletPrefix = "- ";

    /// <summary>The notes' bullet lines, without their bullets; empty without a file.</summary>
    public static IReadOnlyList<string> Changes(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, FileName);
        try
        {
            return File.Exists(path)
                ? [.. File.ReadAllLines(path)
                    .Where(line => line.StartsWith(BulletPrefix, StringComparison.Ordinal))
                    .Select(line => line[BulletPrefix.Length..].Trim())
                    .Where(line => line.Length > 0)]
                : [];
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// True when this version has not been seen yet and was reached by updating; a first
    /// install (nothing seen before) shows nothing.
    /// </summary>
    public static bool IsNewSince(string lastSeen, Version current) =>
        lastSeen.Length > 0
        && UpdateCheck.TryParse(lastSeen, out var seen)
        && Normalize(current) > Normalize(seen);

    /// <summary>"1.2.3" for comparing and saving, whatever the fourth part.</summary>
    public static string Text(Version version) =>
        $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build));
}
