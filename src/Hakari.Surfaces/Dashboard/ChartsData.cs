using Hakari.Core.Accounts;
using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Localization;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;
using Hakari.Core.Settings;

namespace Hakari.Surfaces.Dashboard;

/// <summary>What the Charts page shows: when work happens and which models did it.</summary>
/// <param name="Heat">Cost per weekday and hour, Monday first: 7 rows of 24 hours.</param>
/// <param name="Labels">Under each bar of the model mix.</param>
internal sealed record ChartsData(
    string Currency,
    IReadOnlyList<decimal> Heat,
    IReadOnlyList<string> Labels,
    IReadOnlyList<MixSeries> Mix,
    LimitTrend? Trend = null)
{
    public const int Weekdays = 7;
    public const int Hours = 24;

    private const int TopModels = 3;
    private const int OtherNamesListed = 3;
    private const char KeySeparator = ' ';
    private const string OtherBrush = "HakariChart4Brush";

    private static readonly string[] ModelBrushes =
        ["HakariChart3Brush", "HakariChart2Brush", "HakariChart5Brush"];

    public static ChartsData Empty { get; } =
        new("USD", new decimal[Weekdays * Hours], [], []);

    public static ChartsData Load(DashboardFilter filter) =>
        DashboardData.Read((query, store) => Read(query, store, filter), Empty);

    private static ChartsData Read(UsageQuery query, IndexStore store, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var usage = filter.ToUsageFilter(now);
        var plan = TimelinePlan.For(filter, now);
        return new ChartsData(
            query.Currency,
            ReadHeat(query, usage),
            [.. plan.Buckets.Select(bucket => bucket.Label)],
            ReadMix(query, usage with { From = plan.From }, plan),
            ReadTrend(store, filter, now));
    }

    private const int TrendDays = 90;
    private const string SessionKind = "session";
    private const string WeeklyKind = "weekly_all";

    /// <summary>
    /// How full the chosen account's 5-hour and weekly limits were; with no account chosen,
    /// the one most recently seen. Null when nothing has been recorded.
    /// </summary>
    private static LimitTrend? ReadTrend(
        IndexStore store,
        DashboardFilter filter,
        DateTimeOffset now)
    {
        var settings = SettingsStore.Default.Load();
        var accounts = new AccountRepository(store).ListAccounts();
        var account = filter.AccountId is { Length: > 0 } chosen
            ? accounts.FirstOrDefault(entry => entry.AccountId == chosen)
            : accounts.FirstOrDefault();
        if (account is null)
        {
            return null;
        }

        var from = filter.Period == DashboardPeriod.Today
            ? now.AddHours(-24)
            : filter.From(now) ?? now.AddDays(-TrendDays);
        var history = new LimitHistory(store);
        List<TrendSeries> series =
        [
            new TrendSeries(
                Texts.Get("dashboard.trend.session"),
                "HakariChart3Brush",
                history.Load(account.AccountId, SessionKind, from)),
            new TrendSeries(
                Texts.Get("dashboard.trend.weekly"),
                "HakariChart5Brush",
                history.Load(account.AccountId, WeeklyKind, from),
                IsDashed: true),
        ];
        var trend = new LimitTrend(
            AccountLabels.Full(account, settings.NicknameOf(account.AccountId)),
            ChartStart(series, from, now),
            now,
            series);
        return trend.HasReadings ? trend : null;
    }

    private static readonly TimeSpan ShortestTrendSpan = TimeSpan.FromHours(6);

    /// <summary>
    /// Readings exist only from when Hakari started recording, so the chart starts at the
    /// first one rather than squeezing them all against the right edge of a long period;
    /// it still spans at least a few hours so a fresh start is not one stretched dot.
    /// </summary>
    private static DateTimeOffset ChartStart(
        IEnumerable<TrendSeries> series,
        DateTimeOffset from,
        DateTimeOffset now)
    {
        var readings = series.SelectMany(one => one.Readings).ToList();
        if (readings.Count == 0)
        {
            return from;
        }

        var first = readings.Min(reading => reading.At);
        var start = first < now - ShortestTrendSpan ? first : now - ShortestTrendSpan;
        return start > from ? start : from;
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
        var everyModel = query.Summarize(usage, GroupBy.Model).ToList();
        var models = everyModel.Take(TopModels).ToList();
        var rest = everyModel.Skip(TopModels)
            .Where(model => model.Cost > 0)
            .Select(model => model.Key)
            .ToList();
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
            series.Add(new MixSeries(OtherName(rest), OtherBrush, other));
        }

        return series;
    }

    /// <summary>
    /// Names what "other" holds: a single model by its own name, a few listed, many cut short
    /// with how many more there are.
    /// </summary>
    private static string OtherName(IReadOnlyList<string> rest)
    {
        if (rest.Count == 1)
        {
            return rest[0];
        }

        var listed = string.Join(", ", rest.Take(OtherNamesListed));
        if (rest.Count > OtherNamesListed)
        {
            listed += $" +{rest.Count - OtherNamesListed}";
        }

        return rest.Count == 0
            ? Texts.Get("dashboard.mix.other")
            : Texts.Format("dashboard.mix.otherOf", listed);
    }

    /// <summary>The cost in each bar of the plan, zero where nothing was spent.</summary>
    internal static List<decimal> CostPerBucket(
        UsageQuery query,
        UsageFilter usage,
        TimelinePlan plan)
    {
        var costs = query.Summarize(usage, plan.By)
            .ToDictionary(summary => summary.Key, summary => summary.Cost);
        return [.. plan.Buckets.Select(bucket => costs.GetValueOrDefault(bucket.Key))];
    }
}
