namespace Hakari.Core.Querying;

/// <summary>Reads two-part keys of project-scoped groupings, like project + branch.</summary>
public static class GroupKeys
{
    /// <summary>A unit separator: never in paths, branch names or session ids.</summary>
    public const char Separator = '\u001F';

    public static (string Project, string Item) Split(string key)
    {
        var index = key.IndexOf(Separator, StringComparison.Ordinal);
        return index < 0 ? (string.Empty, key) : (key[..index], key[(index + 1)..]);
    }
}