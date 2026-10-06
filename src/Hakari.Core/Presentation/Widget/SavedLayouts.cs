namespace Hakari.Core.Presentation.Widget;

/// <summary>Keeping and dropping the user's own layouts.</summary>
public static class SavedLayouts
{
    public const int MaximumCount = 8;
    public const int MaximumNameLength = 24;

    /// <summary>
    /// Keeps the layout under its name, replacing one of the same name (ignoring case), newest
    /// first. Past the limit the oldest is let go. A blank name keeps nothing.
    /// </summary>
    public static IReadOnlyList<NamedLayout> Save(
        IReadOnlyList<NamedLayout> saved,
        string name,
        WidgetLayout layout)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return saved;
        }

        if (trimmed.Length > MaximumNameLength)
        {
            trimmed = trimmed[..MaximumNameLength];
        }

        return
        [
            new NamedLayout(trimmed, layout),
            .. saved
                .Where(entry => !string.Equals(
                    entry.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                .Take(MaximumCount - 1),
        ];
    }

    public static IReadOnlyList<NamedLayout> Remove(
        IReadOnlyList<NamedLayout> saved,
        string name) =>
        [.. saved.Where(entry => entry.Name != name)];
}
