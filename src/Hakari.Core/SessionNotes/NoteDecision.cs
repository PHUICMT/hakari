using Hakari.Core.Limits;

namespace Hakari.Core.SessionNotes;

/// <param name="Text">What to tell the session, or null for nothing.</param>
/// <param name="Levels">The steps the session now knows, by limit, to remember.</param>
public sealed record NoteDecision(string? Text, IReadOnlyDictionary<string, int> Levels)
{
    /// <summary>
    /// A new session, or one whose context was just cleared or compacted, hears every limit
    /// past the first step. After that only a limit that climbed a step, or one that reset
    /// after the session heard about it, is worth telling.
    /// </summary>
    /// <param name="known">The steps told to this session before, by limit.</param>
    /// <param name="from">The share at which the first note comes.</param>
    /// <param name="template">The user's own note text; null or empty for the default.</param>
    public static NoteDecision Decide(
        string eventName,
        LimitSnapshot snapshot,
        IReadOnlyDictionary<string, int> known,
        int from,
        DateTimeOffset now,
        TimeZoneInfo zone,
        string? template = null)
    {
        var fresh = eventName == HookInput.SessionStart;
        var limits = snapshot.ProjectedTo(now).Limits;
        var levels = limits
            .Select(limit => (Key: KeyOf(limit), Level: NoteLevel.Of(limit.Percent, from)))
            .Where(entry => entry.Level > NoteLevel.None)
            .GroupBy(entry => entry.Key)
            .ToDictionary(group => group.Key, group => group.Max(entry => entry.Level));

        var climbed = levels.Any(entry => entry.Value > known.GetValueOrDefault(entry.Key));
        var nearlyFull = limits.Where(limit => levels.ContainsKey(KeyOf(limit))).ToList();
        var reset = fresh
            ? []
            : limits.Where(limit => known.ContainsKey(KeyOf(limit))
                && !levels.ContainsKey(KeyOf(limit))).ToList();

        var parts = new List<string>();
        if (nearlyFull.Count > 0 && (fresh || climbed))
        {
            parts.Add(LimitNoteText.NearlyFull(
                nearlyFull, snapshot.FetchedAt, now, zone, template));
        }

        if (reset.Count > 0)
        {
            parts.Add(LimitNoteText.Reset(reset));
        }

        return new NoteDecision(parts.Count > 0 ? string.Join(" ", parts) : null, levels);
    }

    private static string KeyOf(UsageLimit limit) => $"{limit.Kind}|{limit.ScopeName}";
}
