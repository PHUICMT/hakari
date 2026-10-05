namespace Hakari.Core.Limits;

/// <summary>Picks the limit a person most needs to see when there is room for only one.</summary>
public static class LimitPriority
{
    public const string WarningSeverity = "warning";
    public const string SessionGroup = "session";

    private const int NormalRank = 0;
    private const int WarningRank = 1;
    private const int StrongerRank = 2;

    /// <summary>
    /// Higher severity first; at equal severity the 5-hour session, because it resets soonest
    /// and is what blocks work now; then the higher percentage.
    /// </summary>
    public static UsageLimit? MostPressing(LimitSnapshot snapshot) =>
        snapshot.Limits
            .OrderByDescending(limit => SeverityRank(limit.Severity))
            .ThenByDescending(limit => limit.Group == SessionGroup)
            .ThenByDescending(limit => limit.Percent)
            .FirstOrDefault();

    /// <summary>Anything reported above "warning" counts as the strongest level.</summary>
    public static int SeverityRank(string severity) => severity switch
    {
        UsageLimitParser.NormalSeverity => NormalRank,
        WarningSeverity => WarningRank,
        _ => StrongerRank,
    };
}
