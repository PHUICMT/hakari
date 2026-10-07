using Hakari.Core.Indexing;
using Hakari.Core.Limits;
using Hakari.Core.Presentation;
using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <param name="Source">Where the session ran, such as "Windows" or "WSL".</param>
/// <param name="TopModel">The model that cost the most in it, and its share of the cost.</param>
/// <param name="IsHigh">Costs over three times the usual session of the period.</param>
/// <param name="Active">Time in use, leaving out long pauses between replies.</param>
internal sealed record SessionRow(
    UsageSummary Usage,
    TimeSpan Active,
    string? Title,
    string? Source,
    (string Name, double Share)? TopModel,
    bool IsHigh);

internal sealed record SessionRows(IReadOnlyList<SessionRow> Rows, string Currency)
{
    private const int MostRows = 50;
    private const string ModelPrefix = "claude-";

    public static SessionRows Load(DashboardFilter filter) => DashboardData.Read(
        (query, store) => Read(query, store, filter),
        new SessionRows([], "USD"));

    /// <summary>The model's name without the family prefix every one shares.</summary>
    public static string ShortModel(string model) =>
        model.StartsWith(ModelPrefix, StringComparison.Ordinal)
            ? model[ModelPrefix.Length..]
            : model;

    private static SessionRows Read(UsageQuery query, IndexStore store, DashboardFilter filter)
    {
        var usage = filter.ToUsageFilter(DateTimeOffset.Now);
        var all = query.Summarize(usage, GroupBy.ProjectSession);
        var usual = SpendWatch.Median(all.Where(row => row.Cost > 0).Select(row => row.Cost));
        var titles = SessionTitles.Load(store);
        var sources = SourcesOf(query, store, usage);
        var topModels = TopModels(query, usage);
        var active = query.ActiveTime(usage);
        return new SessionRows(
            [
                .. all.Take(MostRows).Select(row =>
                {
                    var session = GroupKeys.Split(row.Key).Item;
                    return new SessionRow(
                        row,
                        active.GetValueOrDefault(row.Key),
                        titles.GetValueOrDefault(session),
                        sources.GetValueOrDefault(row.Key),
                        TopModel(topModels, row.Key, row.Cost),
                        all.Count >= SpendWatch.SessionsForUsual
                            && row.Cost > usual * SpendWatch.UnusualFactor);
                }),
            ],
            query.Currency);
    }

    /// <summary>Each session's source, from one summary per source rather than per row.</summary>
    private static Dictionary<string, string> SourcesOf(
        UsageQuery query,
        IndexStore store,
        UsageFilter usage)
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var activity in SourceActivity.Load(store))
        {
            var name = SourceNames.Short(activity.SourceId);
            foreach (var row in query.Summarize(
                usage with { SourceIds = [activity.SourceId] },
                GroupBy.ProjectSession))
            {
                sources.TryAdd(row.Key, name);
            }
        }

        return sources;
    }

    /// <summary>
    /// Each session's costliest model, keyed the same way as the rows (project and session),
    /// so the share is of that row's cost and never over 100%. One summary per model.
    /// </summary>
    private static Dictionary<string, (string Model, decimal Cost)> TopModels(
        UsageQuery query,
        UsageFilter usage)
    {
        var top = new Dictionary<string, (string Model, decimal Cost)>(StringComparer.Ordinal);
        foreach (var model in query.Summarize(usage, GroupBy.Model))
        {
            foreach (var row in query.Summarize(usage with { Model = model.Key },
                GroupBy.ProjectSession))
            {
                if (!top.TryGetValue(row.Key, out var best) || row.Cost > best.Cost)
                {
                    top[row.Key] = (model.Key, row.Cost);
                }
            }
        }

        return top;
    }

    private static (string, double)? TopModel(
        Dictionary<string, (string Model, decimal Cost)> topModels,
        string key,
        decimal total) =>
        total <= 0 || !topModels.TryGetValue(key, out var top)
            ? null
            : (ShortModel(top.Model), Math.Min(1, (double)(top.Cost / total)));
}
