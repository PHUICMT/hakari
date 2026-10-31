using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <summary>Which of an account's limits a widget item means.</summary>
internal static class LimitPicks
{
    public const string WeeklyGroup = "weekly";

    public static UsageLimit? Session(LimitSnapshot snapshot) =>
        snapshot.Limits.FirstOrDefault(limit => limit.Group == LimitPriority.SessionGroup);

    /// <summary>
    /// The 5-hour limit, else the weekly one: some plans have no 5-hour limit (Ultra, from
    /// October 2026), and a slot set to it should show the limit that does apply, named as
    /// it is, rather than a dash.
    /// </summary>
    public static UsageLimit? SessionOrWeekly(LimitSnapshot snapshot) =>
        Session(snapshot) ?? Weekly(snapshot);

    /// <summary>The all-models weekly limit, else the fullest weekly one.</summary>
    public static UsageLimit? Weekly(LimitSnapshot snapshot) =>
        snapshot.Limits
            .Where(limit => limit.Group == WeeklyGroup)
            .OrderBy(limit => limit.ScopeName is null ? 0 : 1)
            .ThenByDescending(limit => limit.Percent)
            .FirstOrDefault();
}
