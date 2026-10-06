using Hakari.Core.Indexing;
using Hakari.Core.Localization;
using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>A session's subagent work against everything the session cost.</summary>
internal sealed record SubagentSession(UsageSummary Subagents, decimal SessionCost);

/// <summary>What the Workflows page shows: the main thread against its subagents.</summary>
/// <param name="Largest">The costliest session of subagent work, or null if none.</param>
/// <param name="Daily">Main thread and subagents in each bar of the timeline.</param>
/// <param name="Sessions">The sessions whose subagents cost the most.</param>
/// <param name="Models">What the subagents ran on, the costliest first.</param>
/// <param name="Titles">Session titles, when the user keeps them.</param>
internal sealed record WorkflowsData(
    string Currency,
    UsageSummary MainThread,
    UsageSummary Subagents,
    UsageSummary? Largest,
    IReadOnlyList<string> Labels,
    IReadOnlyList<MixSeries> Daily,
    IReadOnlyList<SubagentSession> Sessions,
    IReadOnlyList<UsageSummary> Models,
    IReadOnlyDictionary<string, string>? Titles)
{
    private const int MostSessions = 8;
    private const string MainBrush = "HakariChart3Brush";
    private const string SubagentBrush = "HakariChart2Brush";

    public static WorkflowsData Empty { get; } =
        new("USD", UsageSummary.Empty, UsageSummary.Empty, null, [], [], [], [], null);

    public static WorkflowsData Load(DashboardFilter filter) =>
        DashboardData.Read((query, store) => Read(query, store, filter), Empty);

    private static WorkflowsData Read(UsageQuery query, IndexStore store, DashboardFilter filter)
    {
        var now = DateTimeOffset.Now;
        var usage = filter.ToUsageFilter(now);
        var mainUsage = usage with { IsSidechain = false };
        var subagentUsage = usage with { IsSidechain = true };
        var plan = TimelinePlan.For(filter, now);
        return new WorkflowsData(
            query.Currency,
            query.Total(mainUsage),
            query.Total(subagentUsage),
            query.Summarize(subagentUsage, GroupBy.Session).FirstOrDefault(),
            [.. plan.Buckets.Select(bucket => bucket.Label)],
            ReadDaily(query, mainUsage with { From = plan.From },
                subagentUsage with { From = plan.From }, plan),
            ReadSessions(query, usage, subagentUsage),
            [.. query.Summarize(subagentUsage, GroupBy.Model).Where(model => model.Cost > 0)],
            SessionTitles.Load(store));
    }

    /// <summary>The main thread at the base of each bar, subagents on top.</summary>
    private static List<MixSeries> ReadDaily(
        UsageQuery query,
        UsageFilter main,
        UsageFilter subagents,
        TimelinePlan plan) =>
    [
        new(Texts.Get("dashboard.workflows.main"), MainBrush,
            ChartsData.CostPerBucket(query, main, plan)),
        new(Texts.Get("dashboard.workflows.subagents"), SubagentBrush,
            ChartsData.CostPerBucket(query, subagents, plan)),
    ];

    private static List<SubagentSession> ReadSessions(
        UsageQuery query,
        UsageFilter usage,
        UsageFilter subagentUsage)
    {
        var top = query.Summarize(subagentUsage, GroupBy.ProjectSession)
            .Where(session => session.Cost > 0)
            .Take(MostSessions)
            .ToList();
        if (top.Count == 0)
        {
            return [];
        }

        var whole = query.Summarize(usage, GroupBy.ProjectSession)
            .ToDictionary(session => session.Key, session => session.Cost);
        return
        [
            .. top.Select(session => new SubagentSession(
                session,
                whole.GetValueOrDefault(session.Key, session.Cost))),
        ];
    }
}
