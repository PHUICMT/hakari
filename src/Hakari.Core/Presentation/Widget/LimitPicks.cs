using Hakari.Core.Limits;

namespace Hakari.Core.Presentation.Widget;

/// <summary>Which of an account's limits a widget item means.</summary>
internal static class LimitPicks
{
    private const string WeeklyGroup = "weekly";

    public static UsageLimit? Session(LimitSnapshot snapshot) =>
        snapshot.Limits.FirstOrDefault(limit => limit.Group == LimitPriority.SessionGroup);

    /// <summary>The all-models weekly limit, else the fullest weekly one.</summary>
    public static UsageLimit? Weekly(LimitSnapshot snapshot) =>
        snapshot.Limits
            .Where(limit => limit.Group == WeeklyGroup)
            .OrderBy(limit => limit.ScopeName is null ? 0 : 1)
            .ThenByDescending(limit => limit.Percent)
            .FirstOrDefault();
}
