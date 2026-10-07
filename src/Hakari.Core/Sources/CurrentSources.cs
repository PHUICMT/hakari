using Hakari.Core.Indexing;

namespace Hakari.Core.Sources;

/// <summary>
/// The sources Hakari.exe found last time it looked, kept in the index so a window can tell a
/// WSL distribution that is not running now from one that is, without starting anything.
/// </summary>
public static class CurrentSources
{
    private const string Option = "sources.current";
    private const char Separator = '\n';

    public static void Save(IndexStore store, IEnumerable<UsageSource> sources) =>
        store.SetOption(Option, string.Join(Separator, sources.Select(source => source.Id)));

    /// <summary>Null when Hakari.exe has not said yet.</summary>
    public static IReadOnlySet<string>? Load(IndexStore store) =>
        store.GetOption(Option) is { } value
            ? value.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToHashSet()
            : null;
}