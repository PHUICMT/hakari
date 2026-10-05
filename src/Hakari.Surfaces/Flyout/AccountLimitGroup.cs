using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Flyout;

/// <summary>One account and its limits, as a card in the flyout that can be folded.</summary>
/// <param name="Detail">Email (with a nickname), plan, spending today, freshness.</param>
/// <param name="Summary">The pressing limit in a few words, shown on the folded card.</param>
public sealed record AccountLimitGroup(
    string AccountId,
    string Name,
    string Detail,
    string Summary,
    Tone SummaryTone,
    IReadOnlyList<LimitRow> Limits,
    bool IsCollapsed)
{
    public Visibility LimitsVisibility => IsCollapsed ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The summary stands in for the limits only while they are folded away.</summary>
    public Visibility SummaryVisibility => IsCollapsed ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The chevron points down when open and right when folded.</summary>
    public double ChevronAngle => IsCollapsed ? FoldedAngle : 0;

    private const double FoldedAngle = -90;
}
