using Microsoft.UI.Xaml;

namespace Hakari.Surfaces.Flyout;

/// <summary>One account and its limits, as a card in the flyout that can be folded.</summary>
/// <param name="Email">Shown under a nickname; empty when the name is the email.</param>
/// <param name="Facts">Plan, spending today and freshness, each kept whole when wrapping.</param>
/// <param name="Summary">The pressing limit in a few words, shown on the folded card.</param>
public sealed record AccountLimitGroup(
    string AccountId,
    string Name,
    string Email,
    IReadOnlyList<string> Facts,
    string Summary,
    Tone SummaryTone,
    IReadOnlyList<LimitRow> Limits,
    bool IsCollapsed)
{
    public Visibility LimitsVisibility => IsCollapsed ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EmailVisibility =>
        Email.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>The summary stands in for the limits only while they are folded away.</summary>
    public Visibility SummaryVisibility => IsCollapsed ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The chevron points down when open and right when folded.</summary>
    public double ChevronAngle => IsCollapsed ? FoldedAngle : 0;

    private const double FoldedAngle = -90;
}
