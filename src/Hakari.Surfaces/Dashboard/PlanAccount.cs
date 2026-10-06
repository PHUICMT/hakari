namespace Hakari.Surfaces.Dashboard;

/// <summary>An account's plan against its usage at per-token prices, month by month.</summary>
/// <param name="Plan">Its plan, or what the card covers when it is not one account.</param>
/// <param name="Price">The plan's monthly price in the shown currency, or null if unknown.</param>
/// <param name="Months">Newest first; the first is the month in progress.</param>
/// <param name="BreakEven">Null without a price or without any usage this month.</param>
internal sealed record PlanAccount(
    PlanCardKind Kind,
    string Name,
    string Plan,
    decimal? Price,
    IReadOnlyList<PlanMonth> Months,
    PlanBreakEven? BreakEven)
{
    public PlanMonth ThisMonth => Months[0];
}
