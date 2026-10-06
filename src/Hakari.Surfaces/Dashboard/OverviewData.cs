using Hakari.Core.Pricing;
using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>Everything the Overview page shows for one filter, read in one go.</summary>
/// <param name="PreviousCost">The same length of time before, or null for all time.</param>
/// <param name="Timeline">Cost per bar, oldest first: hours for today, else days.</param>
internal sealed record OverviewData(
    string Currency,
    UsageSummary Total,
    decimal? PreviousCost,
    int DayCount,
    IReadOnlyList<CostShare> Shares,
    IReadOnlyList<(string Label, decimal Cost)> Timeline,
    IReadOnlyList<UsageSummary> ByModel,
    decimal CacheSavings = 0)
{
    private const int TopModels = 6;

    public static OverviewData Empty { get; } =
        new("USD", UsageSummary.Empty, null, 1, [], [], []);

    public static OverviewData Load(DashboardFilter filter) =>
        DashboardData.Read((query, _) => Read(query, filter), Empty);

    private static OverviewData Read(UsageQuery query, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var usage = filter.ToUsageFilter(now);
        var total = query.Total(usage);
        var previous = filter.PreviousUsageFilter(now) is { } before
            ? query.Total(before).Cost
            : (decimal?)null;
        var byModel = query.Summarize(usage, GroupBy.Model);
        var days = filter.Days ?? DaysSince(query.FirstUsageDay(), now);
        return new OverviewData(
            query.Currency,
            total,
            previous,
            days,
            CostShares.Of(byModel, PricingTableOrEmpty(), DateOnly.FromDateTime(now.UtcDateTime)),
            ReadTimeline(query, filter, now),
            [.. byModel.Take(TopModels)],
            query.CacheSavings(usage));
    }

    /// <summary>Every bar is present, even with nothing spent, so gaps read as gaps.</summary>
    private static List<(string Label, decimal Cost)> ReadTimeline(
        UsageQuery query,
        DashboardFilter filter,
        DateTimeOffset now)
    {
        var plan = TimelinePlan.For(filter, now);
        var costs = query.Summarize(filter.ToUsageFilter(now) with { From = plan.From }, plan.By)
            .ToDictionary(summary => summary.Key, summary => summary.Cost);
        return
        [
            .. plan.Buckets.Select(bucket =>
                (bucket.Label, costs.GetValueOrDefault(bucket.Key))),
        ];
    }

    private static int DaysSince(DateOnly? firstDay, DateTimeOffset now) =>
        firstDay is { } day
            ? Math.Max(1, DateOnly.FromDateTime(now.Date).DayNumber - day.DayNumber + 1)
            : 1;

    private static PricingTable PricingTableOrEmpty() => PricingSources.LoadCurrent();
}
