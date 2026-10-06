using Hakari.Core.Querying;

namespace Hakari.Surfaces.Dashboard;

/// <summary>What the Workflows page shows: the main thread against its subagents.</summary>
/// <param name="Largest">The costliest session of subagent work, or null if none.</param>
internal sealed record WorkflowsData(
    string Currency,
    UsageSummary MainThread,
    UsageSummary Subagents,
    UsageSummary? Largest)
{
    public static WorkflowsData Empty { get; } =
        new("USD", UsageSummary.Empty, UsageSummary.Empty, null);

    public static WorkflowsData Load(DashboardFilter filter) =>
        DashboardData.Read((query, _) => Read(query, filter), Empty);

    private static WorkflowsData Read(UsageQuery query, DashboardFilter filter)
    {
        var usage = filter.ToUsageFilter(DateTimeOffset.Now);
        var subagentUsage = usage with { IsSidechain = true };
        return new WorkflowsData(
            query.Currency,
            query.Total(usage with { IsSidechain = false }),
            query.Total(subagentUsage),
            query.Summarize(subagentUsage, GroupBy.Session).FirstOrDefault());
    }
}
