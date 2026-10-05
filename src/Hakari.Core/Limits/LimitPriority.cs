namespace Hakari.Core.Limits;

/// <summary>Picks the limit a person most needs to see when there is room for only one.</summary>
public static class LimitPriority
{
    public const string WarningSeverity = "warning";
    public const string SessionGroup = "session";

    private const int NormalRank = 0;
    private const int WarningRank = 1;
    private const int StrongerRank = 2;

    /// <summary>Larger than any percentage, so severity always decides first.</summary>
    private const int SeverityWeight = 1000;
    private const int NoLimitsRank = -1;

    /// <summary>
    /// A full limit first, since it blocks work; then higher severity; at equal severity the
    /// 5-hour session, because it resets soonest; then the higher percentage.
    /// </summary>
    public static UsageLimit? MostPressing(LimitSnapshot snapshot) =>
        snapshot.Limits
            .OrderByDescending(limit => limit.Percent >= LimitForecaster.FullPercent)
            .ThenByDescending(limit => SeverityRank(limit.Severity))
            .ThenByDescending(limit => limit.Group == SessionGroup)
            .ThenByDescending(limit => limit.Percent)
            .FirstOrDefault();

    /// <summary>Orders accounts: higher severity first, then the higher percentage.</summary>
    public static int Rank(LimitSnapshot snapshot) =>
        MostPressing(snapshot) is { } limit
            ? SeverityRank(limit.Severity) * SeverityWeight + limit.Percent
            : NoLimitsRank;

    /// <summary>Anything reported above "warning" counts as the strongest level.</summary>
    public static int SeverityRank(string severity) => severity switch
    {
        UsageLimitParser.NormalSeverity => NormalRank,
        WarningSeverity => WarningRank,
        _ => StrongerRank,
    };
}
