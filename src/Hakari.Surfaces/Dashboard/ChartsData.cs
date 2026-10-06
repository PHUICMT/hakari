using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>What the Charts page shows: when work happens and which models did it.</summary>
/// <param name="Heat">Cost per weekday and hour, Monday first: 7 rows of 24 hours.</param>
/// <param name="Labels">Under each bar of the model mix.</param>
internal sealed record ChartsData(
    string Currency,
    IReadOnlyList<decimal> Heat,
    IReadOnlyList<string> Labels,
    IReadOnlyList<MixSeries> Mix)
{
    public const int Weekdays = 7;
    public const int Hours = 24;

    private const int TopModels = 3;
    private const char KeySeparator = ' ';
    private const string OtherBrush = "HakariChart4Brush";

    private static readonly string[] ModelBrushes =
        ["HakariChart3Brush", "HakariChart2Brush", "HakariChart5Brush"];

    public static ChartsData Empty { get; } =
        new("USD", new decimal[Weekdays * Hours], [], []);

    public static ChartsData Load(DashboardFilter filter) =>
        DashboardData.Read((query, _) => Read(query, filter), Empty);

    private static ChartsData Read(UsageQuery query, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var usage = filter.ToUsageFilter(now);
        var plan = TimelinePlan.For(filter, now);
        return new ChartsData(
            query.Currency,
            ReadHeat(query, usage),
            [.. plan.Buckets.Select(bucket => bucket.Label)],
            ReadMix(query, usage with { From = plan.From }, plan));
    }

    /// <summary>The database counts Sunday as 0; the chart starts its week on Monday.</summary>
    private static decimal[] ReadHeat(UsageQuery query, UsageFilter usage)
    {
        var heat = new decimal[Weekdays * Hours];
        foreach (var cell in query.Summarize(usage, GroupBy.WeekdayHour))
        {
            var parts = cell.Key.Split(KeySeparator);
            var row = (int.Parse(parts[0]) + Weekdays - 1) % Weekdays;
            heat[row * Hours + int.Parse(parts[1])] = cell.Cost;
        }

        return heat;
    }

    /// <summary>The three costliest models, and everything else together.</summary>
    private static List<MixSeries> ReadMix(
        UsageQuery query,
        UsageFilter usage,
        TimelinePlan plan)
    {
        var series = new List<MixSeries>();
        var models = query.Summarize(usage, GroupBy.Model).Take(TopModels).ToList();
        for (var index = 0; index < models.Count; index++)
        {
            var costs = CostPerBucket(query, usage with { Model = models[index].Key }, plan);
            series.Add(new MixSeries(models[index].Key, ModelBrushes[index], costs));
        }

        var all = CostPerBucket(query, usage, plan);
        var other = all
            .Select((cost, bucket) => cost - series.Sum(model => model.Costs[bucket]))
            .Select(cost => Math.Max(0, cost))
            .ToList();
        if (other.Any(cost => cost > 0))
        {
            series.Add(new MixSeries(string.Empty, OtherBrush, other));
        }

        return series;
    }

    private static List<decimal> CostPerBucket(
        UsageQuery query,
        UsageFilter usage,
        TimelinePlan plan)
    {
        var costs = query.Summarize(usage, plan.By)
            .ToDictionary(summary => summary.Key, summary => summary.Cost);
        return [.. plan.Buckets.Select(bucket => costs.GetValueOrDefault(bucket.Key))];
    }
}
