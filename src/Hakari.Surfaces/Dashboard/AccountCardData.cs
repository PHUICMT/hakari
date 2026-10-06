using Hakari.Surfaces.Flyout;

namespace Hakari.Surfaces.Dashboard;

/// <param name="Detail">Email when there is a nickname, plan, organization.</param>
/// <param name="PeriodCost">What the account spent in the chosen period, as money text.</param>
internal sealed record AccountCardData(
    string Name,
    string Detail,
    string BadgeText,
    BadgeTone BadgeTone,
    IReadOnlyList<LimitRow> Limits,
    string PeriodCost);
