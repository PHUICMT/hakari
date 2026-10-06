namespace Hakari.Surfaces.Dashboard;

/// <summary>How full an account's limits were over a stretch of time.</summary>
/// <param name="AccountName">Whose limits these are.</param>
internal sealed record LimitTrend(
    string AccountName,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<TrendSeries> Series)
{
    public bool HasReadings => Series.Any(series => series.Readings.Count > 0);
}
